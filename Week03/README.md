# Week 3 — Pong connects and replicates

## Checkpoint D (Step 4)

1. **Which component gives the `ObjectId`?** The `NetworkObject` component. When the server spawns the paddle, its NetworkObject is given an ObjectId that is the same on every machine.
2. **Which machine chose LEFT, and how did the client learn it?** The server chose it, in `PaddleController.OnStartServer` (first free side → `Side.Value`). The client learned it because `Side` is a `SyncVar`: its value travels with the spawn message, so it is already correct in `OnStartClient`, and later changes arrive through `Side.OnChange`.
3. **Which line stops other clients moving your paddle?** `if (!IsOwner) return;` — the first line of `PaddleController.Update`. Only the machine that owns the paddle reads the keyboard for it.

## Predictions (before Step 6)

1. **You press W, nothing moves elsewhere. Who must tell whom, in what order?** My client (the owner) must tell the server where my paddle is, then the server must tell every other client.
2. **Three balls. Which machine should own the one real ball? What must a client receive to draw it?** The server should own and move the only real ball; a client only needs to receive its position (and whether it is in play).
3. **If the server believes "my paddle is at z = 3.2" from a client, what is the first cheat?** A client can send a position it never really reached — teleport the paddle in front of the ball or move faster than allowed.

**Grades (after Step 10):** 1. right · 2. right · 3. right

## Connection card (Step 11)

| | Direction 1 (Görkem = server) | Direction 2 (Onur = server) |
|---|---|---|
| Partner | Onur Karakoyun | Onur Karakoyun |
| Network | Radmin VPN (different home internets) | Radmin VPN |
| Server IP | 26.15.38.23 (Görkem) | 26.98.35.18 (Onur) |
| My ClientId + side | Görkem: client 0, LEFT · Onur: client 1, RIGHT | Onur: client 0, LEFT · Görkem: client 1, RIGHT |
| RTT (ms) | Onur: 53 ms | Görkem: 29 ms |
| Worked first try? | No — the server's firewall blocked it; after allowing Pong it connected | Yes |

*We were not in the same place, so we used Radmin VPN instead of a phone hotspot. On demo day we connect over a hotspot.*

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
| 10 | 168 | smooth (host window) | not observed (one client) |
| 30 | 405 | smooth (host window) | not observed (one client) |
| 60 | 767 | smooth (host window) | not observed (one client) |

*Server out B/s: average of 10 one-second samples during a rally, 1 server + 1 client on one laptop, measured with `Pong → Measure tick rates`.*

*Ball column: I watched it in the host window (server + client in one process). There the client's ball is the server's own object, moved every frame, so it looked the same at 10 and 60. A separate client window would only get 10 snapshots per second at tick 10 and should show the ball stepping.*

- **What did 60 cost?** Bandwidth: 767 B/s instead of 405 B/s at 30 (about 1.9×) and 168 B/s at 10 — the server sends roughly a fixed amount per tick, so bytes grow with the tick rate.
- **What did it buy?** A smoother ball and paddle with less delay, because positions are sent more often.
- **Why did the ball step even for the player moving a paddle?** The ball is moved only on the server and sent at the tick rate; my own paddle is moved locally every frame, but the ball still arrives only once per tick, so it steps.

## Answers to notes Section 14

**1. Why can two machines never share a "now"?**
A message needs time to travel: light in fibre covers about 200 km per millisecond, and every router on the way adds queueing. Istanbul → Frankfurt is roughly 1,900 km, so at least ~10 ms one way and ~20 ms there and back; real connections give 35–50 ms RTT, which is two to three frames at 60 fps (16.7 ms each). Whatever a machine receives from another one is already in the past, so each machine only ever knows the others' past.

**2. Topology grid**

| | Dedicated server | Listen server (host) | Peer-to-peer |
|---|---|---|---|
| Trust | a separate server process that no player controls | the hosting player's game | nobody in charge; every peer must follow the rules |
| Cost | someone pays for the server machine | free; the host's PC and upload carry it | free |
| Latency | every player has a ping to the server | host has 0 ms, the others ping the host | players talk directly (if NAT lets them) |

In Step 5 our Pong was a dedicated server: Multiplayer Play Mode Player 2, tagged `Server`, started only the server through `ServerBootstrap`. In Step 11 the server was the Pong build on one laptop (Görkem's, then Onur's) with Start Server and Start Client pressed, so that laptop was a host (listen server) and the other one a client, connected over Radmin VPN.

**3. Why UDP?**
TCP delivers everything in order, so one lost packet holds back all the packets after it until it is resent. For a game that is the wrong trade: a ball position from 100 ms ago is useless, the next one is already on its way. So games use UDP and add reliability only where it is needed. In our Pong, reliable: the score SyncVars, `AnnounceGoal`, `YouServe`, `RequestServe` and the event feed. Unreliable: the ball's and paddles' positions that NetworkTransform sends every tick.

**4. NetworkObject, NetworkBehaviour, and why the Hud has neither**
`NetworkObject` makes a GameObject exist on the network: it gets an ObjectId that is the same on every machine, an owner, and it can be spawned and despawned for everyone. Deriving from `NetworkBehaviour` lets a script on such an object ask network questions (`IsOwner`, `IsServerInitialized`), receive `OnStartServer` / `OnStartClient`, and hold SyncVars and RPCs. `ConnectionHud` needs none of this: it only draws this machine's own buttons and numbers, and nothing about it has to exist or match on another machine, so it is a plain `MonoBehaviour` on a plain GameObject.

**5. One Paddle prefab owned by client 0**

| | Server | Client 0 (owner) | Client 1 |
|---|---|---|---|
| `IsOwner` | false | true | false |
| `IsServerInitialized` | true | false | false |
| start callback | `OnStartServer` (picks the side) | `OnStartClient` (sets `Local`, logs "I am client 0") | `OnStartClient` |
| reads the keyboard? | no | yes | no — stopped by `if (!IsOwner) return;` |

**6. What sends the paddle's position, along which path, with which setting?**
The `NetworkTransform` on the Paddle prefab. The path is owner client → server → all other clients, because Client Authoritative is on: this week the owner is trusted to say where its paddle is. On the Ball, Client Authoritative is off, because nobody owns the ball: the server moves it and every client, including the players, only receives the server's position.

**7. Three balls in Step 5, one ball after Step 7**
In Step 5 every window ran `Court.Awake` with Local Ball ticked, so each window built its own `LocalBall` — a plain, unsynchronised object — and played its own private match: three windows, three balls. After Step 7 Local Ball is off and `BallSpawner.OnStartServer` runs once, only on the server: it instantiates the Ball prefab and calls `Spawn`, which tells every client (also a late one) to create a copy of that one NetworkObject. A client is not allowed to call `Spawn`; FishNet refuses it, so the most a client could make is a local object that nobody else sees.

**8. State or event?**

| Item | State / event | FishNet piece |
|---|---|---|
| the score | state | SyncVar (`LeftScore`, `RightScore`) |
| "LEFT scored" | event | ObserversRpc (`AnnounceGoal`) |
| whose serve it is | state | SyncVar (`ServingSide`) |
| "you serve" | event | TargetRpc (`YouServe`) |
| ball-hits-paddle sound | event | ObserversRpc |
| whether the ball is in play | state | SyncVar (`InPlay`) |
| the ball's position | state | NetworkTransform (sent every tick) |

**9. Who decides whether the ball hit the paddle?**
The server, in `NetworkBall.Update`, using its own copies of the paddles, i.e. the positions their owners last sent, one or two ticks old. Its answer is final. A player who saw their paddle "clearly touch" the ball can see it pass through, because their screen showed their own paddle now and the ball a little in the past. If a client wrote `InPlay.Value` itself, only its own copy would change: the server would not know, no serve would happen, and the value would be overwritten at the server's next change. If clients were allowed to write it, anyone could serve whenever they wanted — a cheat.

**10. Bandwidth**
One client receives 8 × 30 × 40 = 9,600 B/s ≈ 9.6 kB/s; with 4 clients the server sends 4 × 9,600 = 38,400 B/s ≈ 38 kB/s. In our own Step 10 measurement (1 server + 1 client), going from tick 30 to tick 10 cut the server's output from 405 to 168 B/s, less than half. My own paddle did not change at tick 10 because my keyboard moves it locally every frame; the tick rate only changes how often the others hear about it, and how often the ball arrives from the server.
