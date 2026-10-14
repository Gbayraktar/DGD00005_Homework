using FishNet.Object;
using UnityEngine;

/// <summary>
/// Step 7 - On the Court object (which has a NetworkObject).
/// The server creates the one ball when it starts; clients receive it by Spawn.
/// </summary>
public class BallSpawner : NetworkBehaviour
{
    public NetworkObject BallPrefab;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (BallPrefab == null)
        {
            Debug.LogError("[Server] BallSpawner: BallPrefab is empty.");
            return;
        }

        NetworkObject ball = Instantiate(BallPrefab, new Vector3(0f, Court.BallY, 0f), Quaternion.identity);
        Spawn(ball);
        Debug.Log("[Server] Ball spawned");
    }
}
