using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// MediaPipeDirectHand - ???????? HandMotionManager ???
/// 
/// ????:
///   1. ? hand-only-rig.fbx ? Project ???? handPrefab ??
///   2. ?????? ? Auto Assign Bones??????????
///   3. python run_d435i_hand.py --send --port 5056
///   4. Unity Hit Play
/// 
/// ????: ??????(wrist/thumb/index?)????????
/// </summary>
public class MediaPipeDirectHand : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 5056;
    public bool enableLog = true;

    [Header("Hand Model")]
    [Tooltip("?? hand-only-rig.fbx ? ?? Auto Assign Bones")]
    public GameObject handPrefab;

    [Header("Hand Model Bones (????)")]
    public Transform wristBone;
    public Transform[] thumbBones = new Transform[4];
    public Transform[] indexBones = new Transform[4];
    public Transform[] middleBones = new Transform[4];
    public Transform[] ringBones = new Transform[4];
    public Transform[] littleBones = new Transform[4];

    [Header("Bend Limits (degrees)")]
    public float thumbBend = 80f, indexBend = 95f, middleBend = 100f, ringBend = 100f, littleBend = 90f;

    [Header("Spread Limits (degrees)")]
    public float thumbSpread = 35f, indexSpread = 20f, middleSpread = 15f, ringSpread = 15f, littleSpread = 25f;

    [Header("Joint Angle Distribution (0-1)")]
    [Tooltip("Each finger: {MCP, PIP, DIP, TIP} weight")]
    public Vector4 jointWeights = new Vector4(0.1f, 0.4f, 0.7f, 1.0f);

    [Header("Thumb")]
    [Tooltip("Thumb natural spread offset (degrees)")]
    public float thumbNaturalSpread = 30f;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float smoothAlpha = 0.3f;

    private UdpClient _udp;
    private Thread _recvThread;
    private volatile bool _running;
    private ConcurrentQueue<HamerPacket> _queue = new ConcurrentQueue<HamerPacket>();
    private HamerPacket _latest;
    private float _lastPacketTime = 99f;

    private float[] _smoothCurl = new float[5];
    private float[] _smoothSpread = new float[5];
    private Quaternion _smoothPalm = Quaternion.identity;
    private Transform[][] _fingerBones;
    private float[] _bends, _spreads;

    [Header("Manual Bone Override")]
    [Tooltip("If auto-assign fails, manually assign bones here by index. Run Diagnose Bone Hierarchy first to see the order.")]
    public Transform[] allBonesManual = new Transform[21]; // index 0=wrist, 1-4=thumb, 5-8=index, 9-12=middle, 13-16=ring, 17-20=pinky

    [System.Serializable]
    private class HamerPacket
    {
        public string type;
        public float[] hand_0_orient_q;
        public float hand_0_curl_thumb, hand_0_curl_index, hand_0_curl_middle, hand_0_curl_ring, hand_0_curl_little;
        public float hand_0_spread_thumb, hand_0_spread_index, hand_0_spread_middle, hand_0_spread_ring, hand_0_spread_little;
    }

    void Start()
    {
        // Check for manual bone assignment first
        if (allBonesManual != null && allBonesManual.Length >= 21 && allBonesManual[0] != null)
            ApplyManualBones();

        // Auto-instantiate if handPrefab set but bones not assigned
        if (handPrefab != null && wristBone == null)
        {
            var go = Instantiate(handPrefab);
            go.name = handPrefab.name;
            go.transform.SetParent(transform, false);
            AutoDiscover(go.transform);
        }
        InitArrays();
        StartUdp();
    }

    void OnDestroy() { StopUdp(); }
    void OnApplicationQuit() { StopUdp(); }

    void InitArrays()
    {
        _fingerBones = new[] { thumbBones, indexBones, middleBones, ringBones, littleBones };
        _bends = new[] { thumbBend, indexBend, middleBend, ringBend, littleBend };
        _spreads = new[] { thumbSpread, indexSpread, middleSpread, ringSpread, littleSpread };
    }

    void Update()
    {
        _lastPacketTime += Time.deltaTime;
        while (_queue.TryDequeue(out var pkt)) { _latest = pkt; _lastPacketTime = 0f; }
        if (_latest == null) return;

        float smooth = 1f - Mathf.Exp(-8f * Time.deltaTime);

        // Palm
        if (_latest.hand_0_orient_q != null && _latest.hand_0_orient_q.Length == 4 && wristBone)
        {
            Quaternion tgt = new Quaternion(_latest.hand_0_orient_q[1], _latest.hand_0_orient_q[2],
                                            _latest.hand_0_orient_q[3], _latest.hand_0_orient_q[0]);
            _smoothPalm = Quaternion.Slerp(_smoothPalm, tgt, smooth);
            wristBone.rotation = _smoothPalm;
        }

        float[] curls = { _latest.hand_0_curl_thumb, _latest.hand_0_curl_index,
                          _latest.hand_0_curl_middle, _latest.hand_0_curl_ring, _latest.hand_0_curl_little };
        float[] spreads = { _latest.hand_0_spread_thumb, _latest.hand_0_spread_index,
                            _latest.hand_0_spread_middle, _latest.hand_0_spread_ring, _latest.hand_0_spread_little };

        for (int f = 0; f < 5; f++)
        {
            _smoothCurl[f] = Mathf.Lerp(_smoothCurl[f], curls[f], smooth);
            _smoothSpread[f] = Mathf.Lerp(_smoothSpread[f], spreads[f], smooth);
            float pitch = _smoothCurl[f] * _bends[f];
            float yaw = _smoothSpread[f] * _spreads[f];
            if (f == 0) yaw += thumbNaturalSpread;

            var bones = _fingerBones[f];
            for (int j = 0; j < bones.Length && j < 4; j++)
            {
                if (bones[j] == null) continue;
                float w = j == 0 ? jointWeights.x : j == 1 ? jointWeights.y : j == 2 ? jointWeights.z : jointWeights.w;
                if (j == 0)
                    bones[j].localRotation = Quaternion.Euler(pitch * w, 0, yaw);
                else
                    bones[j].localRotation = Quaternion.Euler(pitch * w, 0, 0);
            }
        }
    }

    // ?? Auto bone discovery ??????????????????????????????????

    [ContextMenu("Auto Assign Bones")]
    void AutoAssignBones()
    {
        if (handPrefab == null) { Debug.LogError("??? hand-only-rig.fbx ?? handPrefab ??"); return; }
        var scan = Instantiate(handPrefab);
        try { AutoDiscover(scan.transform); }
        finally { DestroyImmediate(scan); }
        Debug.Log($"[Auto] wrist={wristBone?.name} thumb={NameStr(thumbBones)} index={NameStr(indexBones)}");
    }

    /// <summary>
    /// 1. ???? FBX ? handPrefab
    /// 2. Unity ?????? ??? Script ? "Diagnose Bone Hierarchy" 
    /// 3. ??? Console ???????? 20 ????
    /// 4. ???? ??? Assign ?? 0=wrist, 1-4=thumb, 5-8=index, 9-12=middle, 13-16=ring, 17-20=pinky
    /// 5. ???? Auto Assign Bones ?????
    /// 6. ?????? ?? Inspector ?????
    /// </summary>
    [ContextMenu(""Diagnose Bone Hierarchy"")]
    void DiagnoseBones()
    {
        if (handPrefab == null) { Debug.LogError("Set handPrefab first"); return; }
        var scan = Instantiate(handPrefab);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Bone Hierarchy ===");
        LogTree(scan.transform, sb, 0);
        UnityEngine.Debug.Log(sb.ToString());
        DestroyImmediate(scan);
    }

    void LogTree(Transform t, System.Text.StringBuilder sb, int depth)
    {
        if (t.name.EndsWith("_end")) return;
        if (t.GetComponent<Renderer>() != null) return;
        if (t.GetComponent<SkinnedMeshRenderer>() != null) return;
        sb.AppendLine(new string(' ', depth * 2) + t.name);
        for (int i = 0; i < t.childCount; i++)
            LogTree(t.GetChild(i), sb, depth + 1);
    }


    void AutoDiscover(Transform root)
    {
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

        // Try named search
        wristBone = FindNamed(bones, "wrist");
        string[] fingerPats = { "thumb", "index", "middle", "ring", "pinky|little" };
        var named = new Transform[5][];
        for (int f = 0; f < 5; f++)
        {
            named[f] = FindByPats(bones, fingerPats[f].Split('|'));
            if (named[f].Length == 4) continue; // found all 4
            named[f] = new Transform[4];
        }

        int total = (wristBone != null ? 1 : 0);
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 4; j++)
                if (named[f][j] != null) total++;

        if (total >= 16) // named search succeeded
        {
            thumbBones = named[0]; indexBones = named[1]; middleBones = named[2];
            ringBones = named[3]; littleBones = named[4];
            return;
        }

        // Fallback: hierarchy position (wrist, thumb?4, index?4, ...)
        int i = 0;
        if (wristBone == null && i < bones.Count) wristBone = bones[i++];
        var arrs = new[] { thumbBones, indexBones, middleBones, ringBones, littleBones };
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 4 && i < bones.Count; j++)
                arrs[f][j] = bones[i++];
    }

    // If manual bones are assigned, use them
    void ApplyManualBones()
    {
        if (allBonesManual == null || allBonesManual.Length < 21) return;
        if (allBonesManual[0] == null) return; // no wrist assigned = not set up
        wristBone = allBonesManual[0];
        thumbBones = new[] { allBonesManual[1], allBonesManual[2], allBonesManual[3], allBonesManual[4] };
        indexBones = new[] { allBonesManual[5], allBonesManual[6], allBonesManual[7], allBonesManual[8] };
        middleBones = new[] { allBonesManual[9], allBonesManual[10], allBonesManual[11], allBonesManual[12] };
        ringBones = new[] { allBonesManual[13], allBonesManual[14], allBonesManual[15], allBonesManual[16] };
        littleBones = new[] { allBonesManual[17], allBonesManual[18], allBonesManual[19], allBonesManual[20] };
    }

    Transform FindNamed(List<Transform> list, string name) =>
        list.Find(t => t.name.ToLower().Contains(name));

    Transform[] FindByPats(List<Transform> list, string[] pats)
    {
        var r = new List<Transform>();
        foreach (var t in list)
            foreach (var p in pats)
                if (t.name.ToLower().Contains(p)) { r.Add(t); break; }
        r.Sort((a, b) => string.Compare(a.name, b.name));
        while (r.Count < 4) r.Add(null);
        return r.ToArray();
    }

    string NameStr(Transform[] arr)
    {
        var s = new List<string>();
        foreach (var t in arr) s.Add(t != null ? t.name : "_");
        return string.Join(", ", s);
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
            Debug.Log($"[MediaPipeDirectHand] Listening on {listenPort}");
        }
        catch (System.Exception e) { Debug.LogWarning($"UDP start failed: {e.Message}"); }
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
