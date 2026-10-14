using System;
using System.Text;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Step 7 + 9 - Scores, whose serve and the event feed. Scene object with a NetworkObject.
/// Only the server writes; late joiners get everything from the SyncVars / SyncList.
/// </summary>
public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }

    public readonly SyncVar<int> LeftScore = new SyncVar<int>(0);
    public readonly SyncVar<int> RightScore = new SyncVar<int>(0);
    public readonly SyncVar<int> ServingSide = new SyncVar<int>(0);

    public readonly SyncList<string> Feed = new SyncList<string>();
    private const int MaxFeedLines = 6;

    private Func<string> _panel;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _panel = BuildPanel;
        ConnectionHud.Panels.Add(_panel);
    }

    private void OnDestroy()
    {
        if (_panel != null) ConnectionHud.Panels.Remove(_panel);
        if (Instance == this) Instance = null;
    }

    private string BuildPanel()
    {
        var sb = new StringBuilder();
        sb.Append($"LEFT {LeftScore.Value} : {RightScore.Value} RIGHT\n");
        sb.Append($"Serve: {PaddleController.SideName(ServingSide.Value)}");
        foreach (string line in Feed)
            sb.Append('\n').Append(line);
        return sb.ToString();
    }

    /// <summary>Server only. Called by the ball when it crosses a goal line.</summary>
    public void Goal(int scorerSide)
    {
        if (!IsServerInitialized) return;

        // 1) State first (SyncVars) ...
        if (scorerSide == PaddleController.Left) LeftScore.Value++;
        else RightScore.Value++;
        ServingSide.Value = 1 - scorerSide;
        Announce($"{PaddleController.SideName(scorerSide)} scored {LeftScore.Value} : {RightScore.Value}");

        // 2) ... then the events (RPCs).
        PaddleController scorer = PaddleController.FindBySide(scorerSide);
        PaddleController conceder = PaddleController.FindBySide(1 - scorerSide);
        if (scorer != null)
            scorer.AnnounceGoal(LeftScore.Value, RightScore.Value);
        if (conceder != null && conceder.Owner.IsValid)
            conceder.YouServe(conceder.Owner);
    }

    /// <summary>Server only. Adds a line to the feed, keeps the last six.</summary>
    public static void Announce(string line)
    {
        GameState gs = Instance;
        if (gs == null || !gs.IsServerInitialized) return;

        gs.Feed.Add(line);
        while (gs.Feed.Count > MaxFeedLines)
            gs.Feed.RemoveAt(0);
    }
}
