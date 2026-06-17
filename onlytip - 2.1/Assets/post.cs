using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class post : MonoBehaviour
{
    [Header("UDP Receiver")]
    public int listenPort = 5055;
    public bool enableLog = true;

    [Header("Hand Model Bones")]
    public Transform wristBone;
    public Transform[] thumbBones = new Transform[4];
    public Transform[] indexBones = new Transform[4];
    public Transform[] middleBones = new Transform[4];
    public Transform[] ringBones = new Transform[4];
    public Transform[] littleBones = new Transform[4];

    [Header("Smoothing")]
    [Range(0, 1)] public float curlSmooth = 0.3f;
    private float[] _curls = new float[5];
    private float[] _targetCurls = new float[5];
    private float[] _spreads = new float[5];
    private Quaternion _wristTarget = Quaternion.identity;
    private Quaternion _wristCurrent = Quaternion.identity;

    private UdpClient _udp;
    private Thread _recvThread;
    private volatile bool _running;
    private readonly Queue<string> _packetQueue = new Queue<string>(32);
    private readonly object _lock = new object();

    [Serializable]
    private class HamerPacket
    {
        public string type;
        public int seq;
        public long ts;
        public int num_hands;
        public string hand_0_label;
        public float hand_0_conf;
        public float[] hand_0_wrist;
        public float[] hand_0_kp3d;
        public float[] hand_0_orient_q;
        public float hand_0_curl_thumb;
        public float hand_0_curl_index;
        public float hand_0_curl_middle;
        public float hand_0_curl_ring;
        public float hand_0_curl_little;
        public float hand_0_spread_thumb;
        public float hand_0_spread_index;
        public float hand_0_spread_middle;
        public float hand_0_spread_ring;
        public float hand_0_spread_little;
    }

    void Start()
    {
        StartReceiver();
        _wristCurrent = wristBone ? wristBone.rotation : Quaternion.identity;
    }

    void OnDestroy() { StopReceiver(); }

    void Update()
    {
        List<string> packets = null;
        lock (_lock)
        {
            if (_packetQueue.Count > 0)
            {
                packets = new List<string>(_packetQueue.Count);
                while (_packetQueue.Count > 0)
                    packets.Add(_packetQueue.Dequeue());
            }
        }

        if (packets == null || packets.Count == 0) return;

        foreach (string json in packets)
        {
            try
            {
                var pkt = JsonUtility.FromJson<HamerPacket>(json);
                if (pkt == null || pkt.type != "hamer_hand") continue;

                _targetCurls[0] = pkt.hand_0_curl_thumb;
                _targetCurls[1] = pkt.hand_0_curl_index;
                _targetCurls[2] = pkt.hand_0_curl_middle;
                _targetCurls[3] = pkt.hand_0_curl_ring;
                _targetCurls[4] = pkt.hand_0_curl_little;
                _spreads[0] = pkt.hand_0_spread_thumb;
                _spreads[1] = pkt.hand_0_spread_index;
                _spreads[2] = pkt.hand_0_spread_middle;
                _spreads[3] = pkt.hand_0_spread_ring;
                _spreads[4] = pkt.hand_0_spread_little;

                if (pkt.hand_0_orient_q != null && pkt.hand_0_orient_q.Length == 4)
                {
                    _wristTarget = new Quaternion(
                        pkt.hand_0_orient_q[1], pkt.hand_0_orient_q[2],
                        pkt.hand_0_orient_q[3], pkt.hand_0_orient_q[0]
                    );
                }
            }
            catch (Exception e) { }
        }

        ApplyPose();
    }

    void ApplyPose()
    {
        float t = Time.deltaTime;
        float smoothFactor = 1f - Mathf.Exp(-curlSmooth * 60f * t);

        _wristCurrent = Quaternion.Slerp(_wristCurrent, _wristTarget, smoothFactor);
        if (wristBone) wristBone.rotation = _wristCurrent;

        Transform[][] fingerBones = { thumbBones, indexBones, middleBones, ringBones, littleBones };
        for (int f = 0; f < 5; f++)
        {
            _curls[f] = Mathf.Lerp(_curls[f], _targetCurls[f], smoothFactor);
            float mcpAngle = _curls[f] * 15f + _spreads[f] * 10f;
            float pipAngle = _curls[f] * 60f;
            float dipAngle = _curls[f] * 80f;
            float tipAngle = _curls[f] * 90f;

            if (fingerBones[f].Length >= 1 && fingerBones[f][0])
                fingerBones[f][0].localRotation = Quaternion.Euler(mcpAngle, 0, 0);
            if (fingerBones[f].Length >= 2 && fingerBones[f][1])
                fingerBones[f][1].localRotation = Quaternion.Euler(pipAngle, 0, 0);
            if (fingerBones[f].Length >= 3 && fingerBones[f][2])
                fingerBones[f][2].localRotation = Quaternion.Euler(dipAngle, 0, 0);
            if (fingerBones[f].Length >= 4 && fingerBones[f][3])
                fingerBones[f][3].localRotation = Quaternion.Euler(tipAngle, 0, 0);
        }
    }

    void StartReceiver()
    {
        if (_udp != null) return;
        try
        {
            _udp = new UdpClient(listenPort);
            _udp.Client.ReceiveTimeout = 200;
            _running = true;
            _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
            _recvThread.Start();
            Debug.Log($"[post] UDP receiver started on port {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[post] UDP start failed: {e.Message}");
            StopReceiver();
        }
    }

    void StopReceiver()
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
                lock (_lock) { _packetQueue.Enqueue(json); }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }
        }
    }
}
