using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Step 7 - The one real ball. Only the server moves it; clients just draw
/// what NetworkTransform (client authoritative OFF) sends them.
/// Movement is the same idea as LocalBall: walls, paddles, goal lines.
/// </summary>
public class NetworkBall : NetworkBehaviour
{
    public static NetworkBall Instance { get; private set; }

    public readonly SyncVar<bool> InPlay = new SyncVar<bool>();

    [SerializeField] private float serveSpeed = 7f;
    [SerializeField] private float speedUpPerHit = 1.05f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float paddleSpin = 4f;

    private Vector3 _velocity;
    private Renderer _renderer;

    private void Awake()
    {
        Instance = this;
        _renderer = GetComponentInChildren<Renderer>();
        InPlay.OnChange += OnInPlayChanged;
    }

    private void OnDestroy()
    {
        InPlay.OnChange -= OnInPlayChanged;
        if (Instance == this) Instance = null;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyColor(InPlay.Value); // late joiner gets the right colour at once
    }

    private void OnInPlayChanged(bool prev, bool next, bool asServer) => ApplyColor(next);

    private void ApplyColor(bool inPlay)
    {
        Court.Paint(gameObject, inPlay ? Color.white : Color.grey);
    }

    private void Update()
    {
        if (!IsServerInitialized) return;
        if (!InPlay.Value) return;

        Vector3 p = transform.position + _velocity * Time.deltaTime;

        // Top / bottom walls
        float maxZ = Court.HalfHeight - Court.BallRadius;
        if (p.z > maxZ) { p.z = maxZ; _velocity.z = -Mathf.Abs(_velocity.z); }
        else if (p.z < -maxZ) { p.z = -maxZ; _velocity.z = Mathf.Abs(_velocity.z); }

        // Paddles (the server's copies)
        foreach (PaddleController paddle in PaddleController.All)
        {
            int side = paddle.Side.Value;
            if (side == PaddleController.Spectator) continue;

            Vector3 pp = paddle.transform.position;
            const float halfThick = 0.25f; // LocalBall uses 0.5 = paddle half-thickness + ball radius
            bool movingToward = side == PaddleController.Left ? _velocity.x < 0f : _velocity.x > 0f;
            if (!movingToward) continue;

            bool overlapX = Mathf.Abs(p.x - pp.x) <= halfThick + Court.BallRadius;
            bool overlapZ = Mathf.Abs(p.z - pp.z) <= Court.PaddleHalfLength + Court.BallRadius;
            if (!overlapX || !overlapZ) continue;

            float dir = side == PaddleController.Left ? 1f : -1f;
            p.x = pp.x + dir * (halfThick + Court.BallRadius);
            _velocity.x = dir * Mathf.Abs(_velocity.x) * speedUpPerHit;
            _velocity.z += (p.z - pp.z) / Court.PaddleHalfLength * paddleSpin;
            _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);
        }

        // Goal lines
        if (p.x > Court.HalfWidth + 0.5f) { ScoreFor(PaddleController.Left); return; }
        if (p.x < -Court.HalfWidth - 0.5f) { ScoreFor(PaddleController.Right); return; }

        transform.position = p;
    }

    private void ScoreFor(int scorerSide)
    {
        ResetToCentre();
        if (GameState.Instance != null)
            GameState.Instance.Goal(scorerSide);
    }

    private void ResetToCentre()
    {
        _velocity = Vector3.zero;
        transform.position = new Vector3(0f, Court.BallY, 0f);
        InPlay.Value = false;
    }

    /// <summary>Server only. towardSide: 0 = fly to LEFT, 1 = fly to RIGHT.</summary>
    public void Serve(int towardSide)
    {
        if (!IsServerInitialized) return;

        float dirX = towardSide == PaddleController.Left ? -1f : 1f;
        Vector3 dir = new Vector3(dirX, 0f, Random.Range(-0.5f, 0.5f)).normalized;
        transform.position = new Vector3(0f, Court.BallY, 0f);
        _velocity = dir * serveSpeed;
        InPlay.Value = true;
    }
}
