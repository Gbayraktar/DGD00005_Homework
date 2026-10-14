using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Step 4 + Step 8 - One paddle per connected player (spawned by PlayerSpawner).
/// Server picks the side, owner moves it (NetworkTransform, client authoritative),
/// and the three RPCs for serve / goal / your serve live here.
/// </summary>
public class PaddleController : NetworkBehaviour
{
    public const int Left = 0, Right = 1, Spectator = 2;

    /// <summary>Every paddle on this machine (server and client copies).</summary>
    public static readonly List<PaddleController> All = new List<PaddleController>();
    /// <summary>The paddle this machine owns.</summary>
    public static PaddleController Local { get; private set; }

    /// <summary>0 = LEFT, 1 = RIGHT, 2 = spectator. Only the server writes it.</summary>
    public readonly SyncVar<int> Side = new SyncVar<int>(Spectator);

    [SerializeField] private float speed = 8f;

    // Same colours as the Starter PaddleMovement.
    private static readonly Color Blue = new Color(0.35f, 0.65f, 1f);
    private static readonly Color Orange = new Color(1f, 0.60f, 0.25f);

    private Renderer _renderer;

    public static string SideName(int side) => side == Left ? "LEFT" : side == Right ? "RIGHT" : "SPECTATOR";

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        Side.OnChange += OnSideChanged;
    }

    private void OnDestroy()
    {
        Side.OnChange -= OnSideChanged;
        All.Remove(this);
        if (Local == this) Local = null;
    }

    // ---------- lifecycle ----------

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!All.Contains(this)) All.Add(this);

        int side = FirstFreeSide();
        Side.Value = side;
        Debug.Log($"[Server] client {Owner.ClientId} -> {SideName(side)}");
        GameState.Announce($"Client {Owner.ClientId} joined as {SideName(side)}");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        GameState.Announce($"Client {Owner.ClientId} ({SideName(Side.Value)}) left");
        All.Remove(this);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!All.Contains(this)) All.Add(this);

        if (IsOwner)
        {
            Local = this;
            Debug.Log($"[Client] I am client {Owner.ClientId}, side {SideName(Side.Value)}");
        }
        Place();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        All.Remove(this);
        if (Local == this) Local = null;
    }

    /// <summary>Server only: LEFT if free, else RIGHT if free, else spectator.</summary>
    private int FirstFreeSide()
    {
        bool leftTaken = false, rightTaken = false;
        foreach (PaddleController p in All)
        {
            if (p == this) continue;
            if (p.Side.Value == Left) leftTaken = true;
            if (p.Side.Value == Right) rightTaken = true;
        }
        if (!leftTaken) return Left;
        if (!rightTaken) return Right;
        return Spectator;
    }

    // ---------- placement ----------

    private void OnSideChanged(int prev, int next, bool asServer)
    {
        Place();
    }

    private void Place()
    {
        int side = Side.Value;
        float x = side == Left ? -Court.PaddleX : Court.PaddleX;
        transform.position = new Vector3(x, 0.5f, transform.position.z);

        if (_renderer == null) return;
        _renderer.enabled = side != Spectator;
        if (side != Spectator)
            Court.Paint(gameObject, side == Left ? Blue : Orange);
    }

    // ---------- input (owner only) ----------

    private void Update()
    {
        if (!IsOwner) return;

        float input = 0f;
        if (UpPressed()) input += 1f;
        if (DownPressed()) input -= 1f;

        if (input != 0f)
        {
            transform.position += Vector3.forward * input * speed * Time.deltaTime;
            Court.ClampPaddle(transform);
        }

        if (ServePressed())
            RequestServe();
    }

    // One keyboard per machine: W/S or arrows both work.
    private static bool UpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && (k.wKey.isPressed || k.upArrowKey.isPressed);
#else
        return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
#endif
    }

    private static bool DownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && (k.sKey.isPressed || k.downArrowKey.isPressed);
#else
        return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
#endif
    }

    private static bool ServePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && k.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    // ---------- RPCs (Step 8) ----------

    /// <summary>Client -> server: "let me serve". The server checks the rules.</summary>
    [ServerRpc]
    private void RequestServe()
    {
        GameState gs = GameState.Instance;
        NetworkBall ball = NetworkBall.Instance;
        if (gs == null || ball == null) return;
        if (Side.Value != gs.ServingSide.Value) return;
        if (ball.InPlay.Value) return;

        ball.Serve(1 - Side.Value);
        GameState.Announce($"{SideName(Side.Value)} serves");
    }

    /// <summary>Server -> all clients. Called on the scorer's paddle.</summary>
    [ObserversRpc]
    public void AnnounceGoal(int left, int right)
    {
        ConnectionHud.Flash($"{SideName(Side.Value)} scored! {left} : {right}");
    }

    /// <summary>Server -> one client (the conceder).</summary>
    [TargetRpc]
    public void YouServe(NetworkConnection target)
    {
        ConnectionHud.Flash("YOU SERVE");
    }

    /// <summary>Server helper: the paddle playing a side, or null.</summary>
    public static PaddleController FindBySide(int side)
    {
        foreach (PaddleController p in All)
            if (p.IsServerInitialized && p.Side.Value == side)
                return p;
        return null;
    }
}
