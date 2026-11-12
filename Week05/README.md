# Week 5 — Pong on a host

One click starts the server and a client in one process (Start Host). A guest joins, the host leaves, a partner joins over the LAN.

Changes this week:
- `ConnectionHud`: **Start Host** (`ServerManager.StartConnection()` + `ClientManager.StartConnection("127.0.0.1", port)`, shown when both are stopped) and **Stop Host** (client first, then server, shown when both are started).
- `ConnectionLogger`: in the client handler, when the client is `Started` and `ServerManager.Started` is true → `[Host] This process is the server AND a client. RTT is 0 ms.`
- `PaddleController`: both `All.Add` calls guarded with `if (!All.Contains(this))`; the HUD shows `Paddles in All: N`.

## Step 3 — both callbacks, one paddle

- `All.Count` on the host with one paddle: **1** (the guard works).
- Console after one Start Host (tick 30):
  ```
  [Server] Starting
  [Client] Starting
  [Server] Started
  [Server] client 0 connected. Clients: 1
  [Client] Started
  [Host] This process is the server AND a client. RTT is 0 ms.
  [Server] Spawned paddle #3 for client 0: LEFT
  [Client] I am client 0, playing LEFT. My paddle is #3
  ```
- Note: my HUD showed **RTT 33 ms** on the host, not 0. Our host client still goes through Tugboat on 127.0.0.1, and FishNet measures RTT in whole ticks, so the smallest value it can show at tick 30 is one tick (33 ms). No message leaves the machine.
- Which callback ran first, and which half of the host ran it: **`OnStartServer` ran first, on the server half** (it picked LEFT and logged `[Server] Spawned paddle #N …`); then `OnStartClient` ran on the client half of the same object (`[Client] I am client 0, playing LEFT …`). Without the guard both would have added the same paddle to `All`.

## Step 5 — the host wins

- Guest's RTT: **___ ms**
- What I saw: **___**
- Tick rate 10, one rally, the ball on the guest: **___**  (then back to 30)

## Step 7 — connection card

| | Me as host (Görkem) | Me as guest |
|---|---|---|
| Partner | Onur Karakoyun | Onur Karakoyun |
| Network | Radmin VPN | Radmin VPN |
| Host's address | 26.15.38.23 (me) | 26.98.35.18 (Onur) |
| My ClientId | 0 (host) · Onur: 1, RIGHT | 1, RIGHT · Onur: 0, LEFT |
| My RTT | host — tells nothing (one tick on my HUD) · Onur's: 53 ms | 29 ms |
| Connected first try / fix line | No — "Same, only from other machines": the host's firewall; allowed Pong on Private + Public, then it connected | Yes |

## Step 8 — the same scripts, three ways

1. **Start Server only:** the server halves run — `ConnectionLogger`, `ServerBootstrap`, `BallSpawner.OnStartServer`, `GameState`, the ball's `Update`, `PaddleController.OnStartServer` and the server part of `OnTick` (for remote players). Nothing client-side runs on this process: no `OnStartClient`, no keyboard, no own paddle.
2. **Start Host:** every networked object runs both halves in the same process — `OnStartServer` and then `OnStartClient`, and each SyncVar's `OnChange` fires twice (as server and as client), so feed/flash logs appear twice.
3. **Start Client:** the guest only knows an IP, a port and the ClientId it was given; it cannot tell whether the server is a dedicated server or another player's game.

## Self-review

1. **Start Host: which two calls? Why is the host's RTT 0?**
   `ServerManager.StartConnection()` and `ClientManager.StartConnection("127.0.0.1", port)`. The host's client talks to a server in the same process, so its messages never cross a network — there is no trip to measure.

2. **The guest's paddle on the host, the guest and a spectator:**

   | | Host | Guest (owner) | Spectator |
   |---|---|---|---|
   | `IsOwner` | false | true | false |
   | `IsServerInitialized` | true | false | false |
   | callbacks | `OnStartServer` + `OnStartClient` | `OnStartClient` | `OnStartClient` |
   | reads the keyboard | no | yes | no |

3. **Both serves, traced.**
   Host: Space → `RequestServe` (ServerRpc, but the server is in the same process, so it runs at once) → server checks the side and `InPlay` → `ball.Serve` → ball moves.
   Guest: Space → `RequestServe` travels to the host (half an RTT) → server checks → `ball.Serve` → the new ball position travels back (another half RTT, plus interpolation) → ball moves on the guest's screen. **The guest's serve waits** about one RTT.

4. **Step 6:** the guest's paddle is a NetworkObject that only exists because the server spawned it; when the host (the server) stopped, the guest's connection ended and everything the server had spawned was removed. When the guest stopped, only its connection ended: the server despawned that one paddle and kept running, so the host played on.

5. **State or event:** the score — state · "RIGHT scored!" — event · whose serve — state · "YOU SERVE" — event · the ball's position — state · the feed — state.

6. **A guest writes `GameState.Instance.LeftScore.Value = 9`.** Only the guest's local copy changes (FishNet warns that a client cannot set a SyncVar); the server never hears about it, nobody else sees 9, and the next server write overwrites it. On the host the same line runs in the process that is the server, so it is a server write and is sent to everyone — which is why "it works on the host" proves nothing; test from a guest.

7. **The feed logs twice on the host.** A goal sound must be played behind `if (!asServer)` in the `OnChange` (or only in client code such as the ObserversRpc), so the host plays it once, as a client. The score log does not double because the goal is logged once, by the server in `GameState.Goal`; the SyncVar only carries the value.
