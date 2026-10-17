// Pong — on-screen HUD (Tools). Buttons to start server / client, connection info,
// RTT, tick, bytes per second, extra panels (GameState adds the score) and a big flash text.
using System;
using System.Collections.Generic;
using System.Reflection;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Statistic;
using UnityEngine;

public class ConnectionHud : MonoBehaviour
{
    /// <summary>Other scripts add text panels here, e.g. the score.</summary>
    public static readonly List<Func<string>> Panels = new List<Func<string>>();

    private static string _flashText;
    private static float _flashUntil;

    /// <summary>Shows a big message in the middle of the screen for two seconds.</summary>
    public static void Flash(string text)
    {
        _flashText = text;
        _flashUntil = Time.time + 2f;
        Debug.Log($"[HUD] {text}");
    }

    [SerializeField] private string address = "localhost";

    private NetworkManager _nm;
    private NetworkTrafficStatistics _traffic;

    // bytes counted during the current second, and the last full second
    private ulong _srvIn, _srvOut, _cliIn, _cliOut;
    private ulong _srvInPs, _srvOutPs, _cliInPs, _cliOutPs;
    private float _secondStart;

    private static FieldInfo _inField, _outField, _bytesField;

    private void Start()
    {
        _nm = InstanceFinder.NetworkManager;
        if (_nm == null) return;

        if (_nm.StatisticsManager != null && _nm.StatisticsManager.TryGetNetworkTrafficStatistics(out _traffic))
            _traffic.OnNetworkTraffic += OnNetworkTraffic;
    }

    private void OnDestroy()
    {
        if (_traffic != null) _traffic.OnNetworkTraffic -= OnNetworkTraffic;
    }

    // ---------- bytes per second ----------

    private void OnNetworkTraffic(uint tick, FishNet.Editing.NetworkProfiler.BidirectionalNetworkTraffic server,
                                 FishNet.Editing.NetworkProfiler.BidirectionalNetworkTraffic client)
    {
        _srvIn += Bytes(server, true);
        _srvOut += Bytes(server, false);
        _cliIn += Bytes(client, true);
        _cliOut += Bytes(client, false);
    }

    // FishNet keeps the byte counters internal, so we read them by reflection.
    private static ulong Bytes(object bidirectional, bool inbound)
    {
        if (bidirectional == null) return 0;
        const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type t = bidirectional.GetType();
        if (_inField == null) _inField = t.GetField("InboundTraffic", f);
        if (_outField == null) _outField = t.GetField("OutboundTraffic", f);
        object traffic = (inbound ? _inField : _outField)?.GetValue(bidirectional);
        if (traffic == null) return 0;
        if (_bytesField == null) _bytesField = traffic.GetType().GetField("Bytes", f);
        return _bytesField != null ? (ulong)_bytesField.GetValue(traffic) : 0;
    }

    private void Update()
    {
        FitCameraRightOfPanel();

        if (Time.unscaledTime - _secondStart < 1f) return;
        _srvInPs = _srvIn; _srvOutPs = _srvOut; _cliInPs = _cliIn; _cliOutPs = _cliOut;
        _srvIn = _srvOut = _cliIn = _cliOut = 0;
        _secondStart = Time.unscaledTime;
    }

    // Keeps the whole court visible to the right of the HUD panel.
    private static void FitCameraRightOfPanel()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic || Screen.width <= 0) return;
        float scale = Mathf.Max(1f, Screen.height / 720f);
        float left = Mathf.Clamp01(320f * scale / Screen.width);
        cam.rect = new Rect(left, 0f, 1f - left, 1f);
        float needForWidth = (Court.HalfWidth + 0.7f) / Mathf.Max(0.1f, cam.aspect);
        cam.orthographicSize = Mathf.Max(Court.HalfHeight + 1.5f, needForWidth);
    }

    // ---------- drawing ----------

    private void OnGUI()
    {
        if (_nm == null) _nm = InstanceFinder.NetworkManager;
        if (_nm == null) return;

        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        GUILayout.BeginArea(new Rect(10, 10, 300, 600), GUI.skin.box);

        bool server = _nm.IsServerStarted;
        bool client = _nm.IsClientStarted;

        if (!server && GUILayout.Button("Start Server")) _nm.ServerManager.StartConnection();
        if (server && GUILayout.Button("Stop Server")) _nm.ServerManager.StopConnection(true);

        if (!client)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP", GUILayout.Width(20));
            address = GUILayout.TextField(address);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Start Client")) _nm.ClientManager.StartConnection(address);
        }
        else if (GUILayout.Button("Stop Client")) _nm.ClientManager.StopConnection();

        GUILayout.Space(6);
        GUILayout.Label($"Server: {(server ? "ON" : "off")}   Client: {(client ? "ON" : "off")}");
        if (client)
            GUILayout.Label($"ClientId {_nm.ClientManager.Connection.ClientId}   RTT {_nm.TimeManager.RoundTripTime} ms");
        if (server)
            GUILayout.Label($"Clients connected: {_nm.ServerManager.Clients.Count}");
        if (server || client)
            GUILayout.Label($"Tick {_nm.TimeManager.Tick}   ({_nm.TimeManager.TickRate} / s)");

        if (_traffic != null && (server || client))
        {
            if (server) GUILayout.Label($"Server  in {_srvInPs} B/s   out {_srvOutPs} B/s");
            if (client) GUILayout.Label($"Client  in {_cliInPs} B/s   out {_cliOutPs} B/s");
        }

        foreach (Func<string> panel in Panels)
        {
            string text = null;
            try { text = panel(); } catch { /* panel's object may be gone */ }
            if (string.IsNullOrEmpty(text)) continue;
            GUILayout.Space(6);
            GUILayout.Label(text);
        }

        GUILayout.EndArea();

        if (Time.time < _flashUntil && !string.IsNullOrEmpty(_flashText))
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            float w = Screen.width / scale, h = Screen.height / scale;
            GUI.Label(new Rect(0, h * 0.35f, w, 80), _flashText, style);
        }
    }
}
