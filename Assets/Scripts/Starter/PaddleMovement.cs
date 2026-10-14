// Pong — single-player paddle (Step 1). Plain MonoBehaviour, replaced in Step 4 by PaddleController.
// Two paddles on ONE keyboard: side 0 (left) uses W/S, side 1 (right) uses the arrow keys.
// That is how Pong was played in 1972: one machine, two players, one copy of the world.
using UnityEngine;
using UnityEngine.InputSystem;

public class PaddleMovement : MonoBehaviour
{
    [SerializeField] private int   side  = 0;      // 0 = left (W/S), 1 = right (Up/Down)
    [SerializeField] private float speed = 8f;

    private void Start()
    {
        transform.position = new Vector3(side == 0 ? -Court.PaddleX : Court.PaddleX, 0.5f, 0);
        Court.Paint(gameObject, side == 0 ? new Color(0.35f, 0.65f, 1f) : new Color(1f, 0.60f, 0.25f));
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float dir = 0f;
        if (side == 0)
        {
            if (kb.wKey.isPressed) dir += 1f;
            if (kb.sKey.isPressed) dir -= 1f;
        }
        else
        {
            if (kb.upArrowKey.isPressed)   dir += 1f;
            if (kb.downArrowKey.isPressed) dir -= 1f;
        }

        transform.position += Vector3.forward * dir * speed * Time.deltaTime;
        Court.ClampPaddle(transform);
    }
}
