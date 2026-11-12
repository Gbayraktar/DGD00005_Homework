using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// One paddle per connected player (spawned by PlayerSpawner).
/// Server picks the side. Homework 2: the owner only sends its input (axis) every tick,
/// the SERVER moves the paddle and NetworkTransform (client authoritative off) sends the result.
/// The three RPCs for serve / goal / your serve also live here.
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

    // ---- server-only memory ----
    private float _serverAxis;   // the last input the server accepted

    // Rate limit: one paddle belongs to one client, so one timestamp per paddle = one per client.
    private const double MinInputGap = 0.030;    // seconds
    private double _lastInputTime = -1;
    public int RejectedInputs;
    private System.Func<string> _rejectedPanel;

    // Week 5: shows how many paddles this process has in All (a host with one paddle must say 1).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddCountPanel()
    {
        All.Clear();
        ConnectionHud.Panels.Add(() => $"Paddles in All: {All.Count}");
    }

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

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        TimeManager.OnTick += OnTick;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        if (TimeManager != null) TimeManager.OnTick -= OnTick;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!All.Contains(this)) All.Add(this);

        int side = FirstFreeSide();
        Side.Value = side;
        Debug.Log($"[Server] Spawned paddle #{ObjectId} for client {OwnerId}: {SideName(side)}");
        GameState.Announce($"Client {Owner.ClientId} joined as {SideName(side)}");

        _rejectedPanel = () => $"Rejected inputs (paddle {OwnerId}): {RejectedInputs}";
        ConnectionHud.Panels.Add(_rejectedPanel);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        GameState.Announce($"Client {Owner.ClientId} ({SideName(Side.Value)}) left");
        All.Remove(this);
        if (_rejectedPanel != null) ConnectionHud.Panels.Remove(_rejectedPanel);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!All.Contains(this)) All.Add(this);

        if (IsOwner)
        {
            Local = this;
            Debug.Log($"[Client] I am client {OwnerId}, playing {SideName(Side.Value)}. My paddle is #{ObjectId}");
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

    // ---------- input ----------

    private void Update()
    {
        if (!IsOwner) return;

        if (ServePressed())
            RequestServe();

        // ---- Step 5: debug cheats. They stay in the project; the server must survive them. ----
        if (KeyDown(CheatKey.C))                       // spam: 20 inputs in one frame
            for (int i = 0; i < 20; i++) ServerSetAxis(1f);

        if (KeyDown(CheatKey.X))                       // speed hack
            ServerSetAxis(50f);

        if (KeyDown(CheatKey.T) && NetworkBall.Instance != null)   // teleport, client only, no RPC
            transform.position = NetworkBall.Instance.transform.position;
    }

    private enum CheatKey { C, X, T }

    private static bool KeyDown(CheatKey key)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k == null) return false;
        switch (key)
        {
            case CheatKey.C: return k.cKey.wasPressedThisFrame;
            case CheatKey.X: return k.xKey.wasPressedThisFrame;
            default: return k.tKey.wasPressedThisFrame;
        }
#else
        switch (key)
        {
            case CheatKey.C: return Input.GetKeyDown(KeyCode.C);
            case CheatKey.X: return Input.GetKeyDown(KeyCode.X);
            default: return Input.GetKeyDown(KeyCode.T);
        }
#endif
    }

    // Runs once per network tick on every machine that has this paddle.
    private void OnTick()
    {
        // 1. Owner sends its input (every tick, also 0).
        if (IsOwner && Side.Value != Spectator)
        {
            float axis = 0f;
            if (UpPressed()) axis += 1f;
            if (DownPressed()) axis -= 1f;
            ServerSetAxis(axis);
        }

        // 2. Server moves the paddle with the last accepted input.
        if (IsServerInitialized)
        {
            transform.position += Vector3.forward * _serverAxis * speed * (float)TimeManager.TickDelta;
            Court.ClampPaddle(transform);
        }
    }

    /// <summary>Client -> server: "my input is up / down / nothing".</summary>
    [ServerRpc]
    private void ServerSetAxis(float axis)
    {
        // Well-formed?
        if (float.IsNaN(axis)) return;
        axis = Mathf.Clamp(axis, -1f, 1f);            // a client that sends 50 gets 1
        if (Side.Value == Spectator) return;           // a spectator has no paddle to move

        // Too fast? One keyboard sends one input per tick (33 ms at tick 30).
        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (_lastInputTime >= 0 && now - _lastInputTime < MinInputGap)
        {
            RejectedInputs++;
            Debug.Log($"[Server] Rejected input from client {OwnerId}: too fast.");
            return;
        }
        _lastInputTime = now;

        _serverAxis = axis;
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
