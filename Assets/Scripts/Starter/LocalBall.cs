// Pong — the single-player ball (Step 1). Created by Court when Local Ball is ticked.
// It moves, bounces off the walls and the paddles, and counts goals — on THIS machine.
// Run three copies of the game and you get three balls playing three different games,
// which is the Step 5 lesson. Step 7 replaces it with NetworkBall, moved on the server only.
using UnityEngine;

public class LocalBall : MonoBehaviour
{
    [SerializeField] private float speed = 7f;

    private Vector3 _velocity;
    private int _left, _right;
    private float _serveAt;

    private void Start() { Reset(1); }

    private void Update()
    {
        if (_velocity == Vector3.zero)
        {
            if (Time.time >= _serveAt) Serve();
            return;
        }

        Vector3 p = transform.position + _velocity * Time.deltaTime;

        // walls
        float zLimit = Court.HalfHeight - Court.BallRadius;
        if (Mathf.Abs(p.z) > zLimit) { p.z = Mathf.Sign(p.z) * zLimit; _velocity.z = -_velocity.z; }

        // paddles: anything with a PaddleMovement on it
        foreach (PaddleMovement paddle in FindObjectsByType<PaddleMovement>(FindObjectsSortMode.None))
        {
            Vector3 pp = paddle.transform.position;
            bool towards = (pp.x < 0 && _velocity.x < 0) || (pp.x > 0 && _velocity.x > 0);
            if (!towards) continue;
            if (Mathf.Abs(p.x - pp.x) < 0.5f && Mathf.Abs(p.z - pp.z) < Court.PaddleHalfLength + Court.BallRadius)
            {
                _velocity.x = -_velocity.x;
                _velocity.z += (p.z - pp.z) * 3f;              // hit near the end: more angle
                _velocity = _velocity.normalized * speed;
                p.x = pp.x + (pp.x < 0 ? 0.5f : -0.5f);
            }
        }

        transform.position = p;

        // goals
        if (p.x < -Court.HalfWidth - 0.5f)      { _right++; Debug.Log($"[Local] RIGHT scored. {_left} : {_right}"); Reset(0); }
        else if (p.x > Court.HalfWidth + 0.5f)  { _left++;  Debug.Log($"[Local] LEFT scored. {_left} : {_right}");  Reset(1); }
    }

    private int _nextTowards;

    private void Reset(int towards)
    {
        transform.position = new Vector3(0, Court.BallY, 0);
        _velocity = Vector3.zero;
        _nextTowards = towards;
        _serveAt = Time.time + 1f;
    }

    private void Serve()
    {
        float dx = _nextTowards == 0 ? -1f : 1f;
        float dz = Random.Range(-0.6f, 0.6f);
        _velocity = new Vector3(dx, 0, dz).normalized * speed;
    }
}
