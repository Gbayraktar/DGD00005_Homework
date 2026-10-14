# DGD0005 — Pong (FishNet)

## My own game: three things

In my online Hangman game, **each player owns** their own character: their name, look, ready flag and their own letter guesses (they ask the server through ServerRpcs, and only their own masked word is sent to them with a TargetRpc). **Nobody owns** the secret word, the turn order and the timer — those live only on the server (`PuzzleManager`) and are never sent to clients until the round ends. **Everybody shares** the round number, category, word length, whose turn it is, the time left, the scores and the last event, which the server writes into SyncVars so every player (and a late joiner) sees the same values.

## How to run

1. Open `Scenes/Pong` in Unity 6000.3.2f1 and press Play → **Start Server** → **Start Client** (this machine plays LEFT).
2. On a second laptop on the same hotspot, open the project, press Play, type the server's IP in the HUD → **Start Client** (plays RIGHT).
3. LEFT presses **Space** to serve; move with **W/S** or the arrow keys.
