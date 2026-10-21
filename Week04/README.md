# Week 4 — Homework 2: The server decides

Homework 1 Pong, one change: the client no longer says "my paddle is here". It sends only its input (up, down, nothing) every tick, the server moves the paddle, and the server ignores a second input from the same client inside 30 ms.

## Step 1 — Client Authoritative off

Paddle prefab ▸ NetworkTransform ▸ Client Authoritative **off** (the Ball was already off). The position now goes server → everyone, the owner included.

## Step 2 — Input up, position down

`PaddleController.OnTick` (subscribed in `OnStartNetwork`): the owner reads W/S or the arrows into `axis` and calls `ServerSetAxis(axis)` every tick; the server moves the paddle by `_serverAxis × speed × TickDelta` and clamps it. `Update` no longer writes `transform`.

RTT shown on the HUD (my delay): **33 ms**.

## Step 3 — Check the input

The server receives `axis = 50`. The line that makes the paddle move at normal speed is **`axis = Mathf.Clamp(axis, -1f, 1f);`** in `ServerSetAxis`. It runs **on the server**, because `ServerSetAxis` is a `[ServerRpc]`: the client only sends the number, the server's code decides what to do with it.

## Step 4 — Rate limit 30 ms

- **Why 30 ms?** At tick rate 30 one tick lasts 33 ms and one keyboard sends one input per tick. 30 ms is a little shorter than one tick, so a normal player is never blocked, while anything faster cannot come from a keyboard — it is a program.
- **What happens with 5 ms?** The limit would let up to ~200 inputs per second through, far more than a keyboard can send, so it would hardly stop a spam program. (Our time comes from the tick, so inputs inside the same tick still count as 0 ms apart, but the rule would no longer say "one per tick" and would break as soon as the tick rate rises.)
- **What happens with 500 ms?** Only two inputs per second would be accepted. Most of a real player's inputs would be rejected, so their paddle would react late and stutter — the server would be punishing honest players.

## Step 5 — The cheating client

| KEY | SERVER CONSOLE | PADDLE |
|---|---|---|
| C once | 19 lines `[Server] Rejected input … too fast.`, counter +19 | moves at normal speed |
| X once | nothing | moves at normal speed, never faster |
| T once | nothing | jumps, then snaps back within a tick |

Screenshot of the server Console after one C:

![Server console after C](screenshots/step5_c_spam.png)

1. **C sends 20 inputs and the server accepts one. Why is the paddle not 20 times faster?** The server moves the paddle only in its own tick, by one `_serverAxis × speed × TickDelta`; an input only sets `_serverAxis` (at most 1) and never moves the paddle itself, so more inputs cannot mean more speed.
2. **T moved the paddle on your screen. Why did the ball on the server not notice?** T only changed the client's own copy of the transform and sent nothing; the server bounces the ball off its own copy of the paddle, which never moved, and its NetworkTransform puts my paddle back at once.
3. **Which of the three cheats would also fail without the rate limit, and why?** All three: X is stopped by the clamp, T by server authority (the client's transform is ignored), and even C would not speed the paddle up, because extra inputs only overwrite `_serverAxis`; the rate limit is what lets the server notice and count the spam (and saves it the work).
