using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// MediaPipeFingerSolverBridge - MediaPipe ???? HandMotionManager + FingerSolver ??
///
/// ????:
///   1. ? hand-only-rig.fbx ?? handPrefab ??
///   2. ???? ? Auto Assign Bones???????? FingerSolver?
///   3. python run_d435i_hand.py --send --port 5057
///   4. Unity Hit Play
///
/// ????: FBX ??4? ? FingerSolver 3? (index 0=MCP, 2=DIP, 3=TIP)
/// </summary>
[RequireComponent(typeof(HandMotionManager))]
public class MediaPipeFingerSolverBridge : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 5057;
    public bool showGUI = true;

    [Header("Hand Model")]
    [Tooltip("?? hand-only-rig.fbx ? ?? Auto Assign Bones")]
    public GameObject handPrefab;

    [Header("Hand Motion Manager")]
    public HandMotionManager handMotion;

    [Header("Palm/Wrist")]
    public Transform wristBone;
    public Transform palmBone;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float smoothAlpha = 0.3f;

    [Header("Per-Finger Bend (0=use FingerSolver default)")]
    public float thumbBend = 0f, indexBend = 0f, middleBend = 0f, ringBend = 0f, littleBend = 0f;
    public float thumbYaw = 0f, indexYaw = 0f, middleYaw = 0f, ringYaw = 0f, littleYaw = 0f;

    [System.Serializable]
    private class HamerPacket
    {
        public string type;
        public float[] hand_0_orient_q;
        public float hand_0_curl_thumb, hand_0_curl_index, hand_0_curl_middle, hand_0_curl_ring, hand_0_curl_little;
        public float hand_0_spread_thumb, hand_0_spread_index, hand_0_spread_middle, hand_0_spread_ring, hand_0_spread_little;
    }

    private UdpClient _udp;
    private Thread _recvThread;
    private volatile bool _running;
    private ConcurrentQueue<HamerPacket> _queue = new ConcurrentQueue<HamerPacket>();
    private HamerPacket _latest;
    private float _lastPacketTime = 99f;
    private Quaternion _smoothPalm = Quaternion.identity;

    void Start()
    {
        if (handMotion == null) handMotion = GetComponent<HandMotionManager>();

        // Auto-instantiate hand model if prefab set but bones not assigned
        if (handPrefab != null && NeedBoneAssignment())
        {
            var go = Instantiate(handPrefab);
            go.name = handPrefab.name;
            go.transform.SetParent(transform, false);
            AutoDiscover(go.transform);
        }

        if (wristBone != null) _smoothPalm = wristBone.rotation;
        StartUdp();
        Debug.Log($"[FingerSolverBridge] Port {listenPort} -> HandMotionManager");
    }

    void OnDestroy() { StopUdp(); }
    void OnApplicationQuit() { StopUdp(); }

    bool NeedBoneAssignment() =>
        handMotion == null ||
        (handMotion.thumb.rootBone == null && handMotion.index.rootBone == null);

    void Update()
    {
        _lastPacketTime += Time.deltaTime;
        while (_queue.TryDequeue(out var pkt)) { _latest = pkt; _lastPacketTime = 0f; }
        if (_latest == null || handMotion == null) return;

        float smooth = 1f - Mathf.Exp(-8f * Time.deltaTime);

        // Palm
        if (_latest.hand_0_orient_q != null && _latest.hand_0_orient_q.Length == 4)
        {
            Quaternion tgt = new Quaternion(_latest.hand_0_orient_q[1], _latest.hand_0_orient_q[2],
                                            _latest.hand_0_orient_q[3], _latest.hand_0_orient_q[0]);
            _smoothPalm = Quaternion.Slerp(_smoothPalm, tgt, smooth);
            if (wristBone) wristBone.rotation = _smoothPalm;
            if (palmBone) palmBone.rotation = _smoothPalm;
        }

        // Fingers -> FingerSolver
        float[] curls = { _latest.hand_0_curl_thumb, _latest.hand_0_curl_index,
                          _latest.hand_0_curl_middle, _latest.hand_0_curl_ring, _latest.hand_0_curl_little };
        float[] spreads = { _latest.hand_0_spread_thumb, _latest.hand_0_spread_index,
                            _latest.hand_0_spread_middle, _latest.hand_0_spread_ring, _latest.hand_0_spread_little };

        FingerSolver[] fingers = { handMotion.thumb, handMotion.index, handMotion.middle,
                                   handMotion.ring, handMotion.little };
        float[] maxPitch = { Pick(thumbBend, 80f), Pick(indexBend, 95f), Pick(middleBend, 100f),
                             Pick(ringBend, 100f), Pick(littleBend, 90f) };
        float[] maxYaw = { Pick(thumbYaw, 35f), Pick(indexYaw, 20f), Pick(middleYaw, 15f),
                           Pick(ringYaw, 15f), Pick(littleYaw, 25f) };
        Vector3 bendAxis = handMotion.fingerBendAxis;
        Vector3 spreadAxis = handMotion.fingerSpreadAxis;

        for (int i = 0; i < 5; i++)
        {
            if (fingers[i].rootBone == null) continue;
            float pitch = Mathf.Clamp(curls[i] * maxPitch[i], fingers[i].minPitch, maxPitch[i]);
            float yaw = Mathf.Clamp(spreads[i] * maxYaw[i], -maxYaw[i], maxYaw[i]);
            fingers[i].ForceVisionAngleAnchor(pitch, yaw, bendAxis, spreadAxis);
        }
    }

    float Pick(float v, float def) => v > 0f ? v : def;

    // ?? Auto bone discovery ??????????????????????????????????

    [ContextMenu("Auto Assign Bones")]
    void AutoAssignBones()
    {
        if (handPrefab == null) { Debug.LogError("??? FBX ?? handPrefab ??"); return; }
        if (handMotion == null) handMotion = GetComponent<HandMotionManager>();

        var scan = Instantiate(handPrefab);
        try { AutoDiscover(scan.transform); }
        finally { DestroyImmediate(scan); }

        Debug.Log($"[Auto] FingerSolver bones assigned: thumb={handMotion.thumb.rootBone?.name} ...");

    [ContextMenu("Diagnose Bone Hierarchy")]
    void DiagnoseBones()
    {
        if (handPrefab == null) { Debug.LogError("Set handPrefab first"); return; }
        var scan = Instantiate(handPrefab);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== MediaPipeFingerSolverBridge Bone Hierarchy ===");
        LogBoneTree(scan.transform, sb, 0);
        Debug.Log(sb.ToString());
        DestroyImmediate(scan);
    }

    void LogBoneTree(Transform t, System.Text.StringBuilder sb, int depth)
    {
        if (t.name.EndsWith("_end")) return;
        if (t.GetComponent<Renderer>() != null) return;
        if (t.GetComponent<SkinnedMeshRenderer>() != null) return;
        sb.AppendLine(new string(" ", depth * 2) + t.name);
        for (int i = 0; i < t.childCount; i++)
            LogBoneTree(t.GetChild(i), sb, depth + 1);
    }

    [ContextMenu("Diagnose FingerSolver Assignment")]
    void DiagnoseFingerSolvers()
    {
        if (handMotion == null) handMotion = GetComponent<HandMotionManager>();
        if (handMotion == null) { Debug.LogError("No HandMotionManager"); return; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== FingerSolver Bones ===");
        FingerSolver[] fs = {handMotion.thumb, handMotion.index, handMotion.middle, handMotion.ring, handMotion.little};
        foreach (var f in fs)
            sb.AppendLine($"{f.fingerName}: root={f.rootBone?.name}, mid={f.midBone?.name}, tip={f.tipBone?.name}");
        sb.AppendLine($"IsCalibrated: {handMotion.IsCalibrated}");
        Debug.Log(sb.ToString());
    }

    }

    void AutoDiscover(Transform root)
    {
        // Collect bone transforms
        var all = root.GetComponentsInChildren<Transform>(true);
        var bones = new List<Transform>();
        foreach (var t in all)
        {
            if (t == root) continue;
            if (t.name.EndsWith("_end")) continue;
            if (t.GetComponent<Renderer>() != null) continue;
            if (t.GetComponent<SkinnedMeshRenderer>() != null) continue;
            bones.Add(t);
        }

        // Try named search first
        wristBone = FindNamed(bones, "wrist");
        palmBone = FindNamed(bones, "palm");

        FingerSolver[] fingers = { handMotion.thumb, handMotion.index, handMotion.middle,
                                   handMotion.ring, handMotion.little };
        string[] pats = { "thumb", "index", "middle", "ring", "pinky|little" };

        bool namedOk = true;
        for (int f = 0; f < 5; f++)
        {
            var fb = FindByPats(bones, pats[f].Split('|'));
            if (fb.Length >= 3)
            {
                fingers[f].rootBone = fb[0];  // MCP
                fingers[f].midBone = fb[fb.Length >= 3 ? 2 : 1];  // DIP or PIP
                fingers[f].tipBone = fb[fb.Length - 1];  // TIP
            }
            else namedOk = false;
        }

        if (namedOk) return;

        // Fallback: hierarchy position
        // Expected: wrist, thumb(4), index(4), middle(4), ring(4), pinky(4)
        int i = 0;
        if (wristBone == null && i < bones.Count) wristBone = bones[i++];
        for (int f = 0; f < 5; f++)
        {
            // Collect 4 bones for this finger
            var fbs = new List<Transform>();
            for (int j = 0; j < 4 && i < bones.Count; j++)
                fbs.Add(bones[i++]);
            if (fbs.Count >= 3)
            {
                fingers[f].rootBone = fbs[0];
                fingers[f].midBone = fbs[2];
                fingers[f].tipBone = fbs[3];
            }
        }
    }

    Transform FindNamed(List<Transform> list, string name) =>
        list.Find(t => t.name.ToLower().Contains(name));

    Transform[] FindByPats(List<Transform> list, string[] pats)
    {
        var r = new List<Transform>();
        foreach (var t in list)
            foreach (var p in pats)
                if (t.name.ToLower().Contains(p.Trim())) { r.Add(t); break; }
        r.Sort((a, b) => string.Compare(a.name, b.name));
        return r.ToArray();
    }

    // ?? UDP ??????????????????????????????????????????????????

    void StartUdp()
    {
        if (_running) return;
        try
        {
            _udp = new UdpClient(listenPort);
            _udp.Client.ReceiveTimeout = 200;
            _running = true;
            _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
            _recvThread.Start();
            Debug.Log($"[FingerSolverBridge] Listening on {listenPort}");
        }
        catch (System.Exception e) { Debug.LogWarning($"UDP failed: {e.Message}"); }
    }

    void StopUdp()
    {
        _running = false;
        if (_udp != null) { _udp.Close(); _udp = null; }
        if (_recvThread != null && _recvThread.IsAlive) _recvThread.Join(300);
        _recvThread = null;
    }

    void ReceiveLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running && _udp != null)
        {
            try
            {
                byte[] data = _udp.Receive(ref remote);
                string json = Encoding.UTF8.GetString(data);
                var pkt = JsonUtility.FromJson<HamerPacket>(json);
                if (pkt != null && pkt.type == "hamer_hand") _queue.Enqueue(pkt);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }
        }
    }
}
