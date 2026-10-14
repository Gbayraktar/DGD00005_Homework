// Pong — builds the court at Awake (Step 1). Plain MonoBehaviour: no networking.
// The court is a 16 × 10 rectangle in the XZ plane seen from above:
//   x runs left (-8) to right (+8), z runs bottom (-5) to top (+5).
// Paddles sit at x = ±7 and slide along z. The ball lives at y = 0.25.
//
// Step 1: Local Ball ticked -> this script also creates a LocalBall, which bounces on THIS
// machine only. Step 7 unticks it: the ball becomes a NetworkObject the SERVER spawns.
using UnityEngine;

public class Court : MonoBehaviour
{
    public const float HalfWidth        = 8f;
    public const float HalfHeight       = 5f;
    public const float PaddleX          = 7f;
    public const float PaddleHalfLength = 1f;
    public const float BallRadius       = 0.25f;
    public const float BallY            = 0.25f;

    [SerializeField] private bool localBall = true;    // Step 1 only. Untick in Step 7.

    private static Material _mat;

    private void Awake()
    {
        Make("Floor", PrimitiveType.Cube, new Vector3(0, -0.1f, 0), new Vector3(HalfWidth * 2, 0.2f, HalfHeight * 2), new Color(0.10f, 0.12f, 0.16f));
        Make("Wall Top",    PrimitiveType.Cube, new Vector3(0, 0.25f,  HalfHeight + 0.25f), new Vector3(HalfWidth * 2 + 1, 0.5f, 0.5f), new Color(0.75f, 0.78f, 0.82f));
        Make("Wall Bottom", PrimitiveType.Cube, new Vector3(0, 0.25f, -HalfHeight - 0.25f), new Vector3(HalfWidth * 2 + 1, 0.5f, 0.5f), new Color(0.75f, 0.78f, 0.82f));
        for (int i = -4; i <= 4; i++)                  // the dashed centre line, purely decorative
            Make($"Centre {i}", PrimitiveType.Cube, new Vector3(0, 0.01f, i * 1.1f), new Vector3(0.15f, 0.02f, 0.6f), new Color(0.35f, 0.38f, 0.44f));

        if (localBall)
        {
            GameObject ball = Make("Local Ball", PrimitiveType.Sphere, new Vector3(0, BallY, 0), Vector3.one * (BallRadius * 2), Color.white);
            ball.AddComponent<LocalBall>();
        }
    }

    private static GameObject Make(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        Destroy(go.GetComponent<Collider>());        // we do not use Unity physics: the ball is arithmetic
        Paint(go, color);
        return go;
    }

    /// Keeps a paddle inside the walls.
    public static void ClampPaddle(Transform t)
    {
        Vector3 p = t.position;
        float limit = HalfHeight - PaddleHalfLength;
        p.z = Mathf.Clamp(p.z, -limit, limit);
        t.position = p;
    }

    /// Colours a primitive without creating a material per object.
    public static void Paint(GameObject go, Color color)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        if (_mat == null) _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));   // flat colours: works with the 3D and the 2D URP renderer alike
        r.material = new Material(_mat) { color = color };
    }
}
