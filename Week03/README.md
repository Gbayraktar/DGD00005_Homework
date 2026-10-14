# Week 3 — Pong connects and replicates

## Checkpoint D (Step 4)

1. **Which component gives the `ObjectId`?** The `NetworkObject` component. When the server spawns the paddle, its NetworkObject is given an ObjectId that is the same on every machine.
2. **Which machine chose LEFT, and how did the client learn it?** The server chose it, in `PaddleController.OnStartServer` (first free side → `Side.Value`). The client learned it because `Side` is a `SyncVar`: its value travels with the spawn message, so it is already correct in `OnStartClient`, and later changes arrive through `Side.OnChange`.
3. **Which line stops other clients moving your paddle?** `if (!IsOwner) return;` — the first line of `PaddleController.Update`. Only the machine that owns the paddle reads the keyboard for it.

## Predictions (before Step 6)

1. **You press W, nothing moves elsewhere. Who must tell whom, in what order?** My client (the owner) must tell the server where my paddle is, then the server must tell every other client.
2. **Three balls. Which machine should own the one real ball? What must a client receive to draw it?** The server should own and move the only real ball; a client only needs to receive its position (and whether it is in play).
3. **If the server believes "my paddle is at z = 3.2" from a client, what is the first cheat?** A client can send a position it never really reached — teleport the paddle in front of the ball or move faster than allowed.

**Grades (after Step 10):** 1. ___ · 2. ___ · 3. ___  *(right / wrong / half)*

## Connection card (Step 11)

| | Direction 1 (I am server) | Direction 2 (partner is server) |
|---|---|---|
| Partner | | |
| Network (hotspot name) | | |
| Server IP | | |
| My ClientId + side | | |
| RTT (ms) | | |
| Worked first try? | | |

## Design sheet (before Step 7)

| THING | STATE OR EVENT? | FISHNET PIECE | ON WHICH OBJECT? | WHO WRITES / CALLS? |
|---|---|---|---|---|
| Ball position | State | NetworkTransform | Ball | Server |
| Ball in play? | State | SyncVar\<bool\> | Ball | Server |
| Two scores | State | 2 × SyncVar\<int\> | GameState | Server |
| Whose serve | State | SyncVar\<int\> | GameState | Server |
| "Let me serve" | Event | ServerRpc | Paddle | Owner client |
| "LEFT scored!" to all | Event | ObserversRpc | Scorer paddle | Server |
| "YOU SERVE" to one | Event | TargetRpc | Conceder paddle | Server |
| Event feed | State | SyncList\<string\> | GameState | Server |

The one ball is created by `BallSpawner` (on the Court object), on the server, in `OnStartServer`: it `Instantiate`s the prefab at `(0, Court.BallY, 0)` and calls `Spawn(...)`.
The first line of the ball's `Update` is `if (!IsServerInitialized) return;` — only the server moves the ball.

## Step 8

1. **Hit sound on all clients?** `ObserversRpc`. **"Not your serve" to one player?** `TargetRpc`.
2. **Where is the score, who may change it?** In the `LeftScore` / `RightScore` SyncVars on `GameState`; only the server may change them (in `GameState.Goal`).
3. **Ball bounced on the server but passed through on my screen — which paddle copy did the server use, why different?** The server used *its own* copy of the paddle, which is where the owner's NetworkTransform last told it the paddle was. My screen shows a different moment: the other paddle is interpolated (drawn a little in the past) and the ball arrives late from the server, so the two copies disagree for a few ticks.

## Step 10 — Bytes and tick rate

| TICK RATE | SERVER OUT B/S (RALLY) | BALL IN A CLIENT WINDOW | OTHER PADDLE |
|---|---|---|---|
| 10 | | | |
| 30 | | | |
| 60 | | | |

- **What did 60 cost?** ___ *(about twice the server out B/s of 30 — fill in your numbers)*
- **What did it buy?** A smoother ball and paddle with less delay, because positions are sent more often.
- **Why did the ball step even for the player moving a paddle?** The ball is moved only on the server and sent at the tick rate; my own paddle is moved locally every frame, but the ball still arrives only once per tick, so it steps.

## Answers to notes Section 14

*(Write your answers here — the questions are in the Week 3 lecture notes, Section 14.)*
