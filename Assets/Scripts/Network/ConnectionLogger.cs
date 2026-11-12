using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

/// <summary>
/// Step 3 - Logs every connection event. Lives on the NetworkManager object.
/// Subscribes in Start (not Awake): in Awake the NetworkManager may not be ready yet.
/// </summary>
public class ConnectionLogger : MonoBehaviour
{
    private NetworkManager _networkManager;

    private void Start()
    {
        _networkManager = InstanceFinder.NetworkManager;
        if (_networkManager == null)
        {
            Debug.LogWarning("[ConnectionLogger] No NetworkManager found.");
            return;
        }

        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
    }

    private void OnDestroy()
    {
        if (_networkManager == null)
            return;

        _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    }

    // Server socket: Starting / Started / Stopping / Stopped
    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        Debug.Log($"[Server] {args.ConnectionState}");
    }

    // A remote client connected to / disconnected from this server.
    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        string what = args.ConnectionState == RemoteConnectionState.Started ? "connected" : "disconnected";
        Debug.Log($"[Server] client {conn.ClientId} {what}. Clients: {_networkManager.ServerManager.Clients.Count}");
    }

    // This machine's own client socket.
    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log($"[Client] {args.ConnectionState}");

        // Week 5: checked here, in the client handler, so the server has already started.
        if (args.ConnectionState == LocalConnectionState.Started && _networkManager.ServerManager.Started)
            Debug.Log("[Host] This process is the server AND a client. RTT is 0 ms.");
    }
}
