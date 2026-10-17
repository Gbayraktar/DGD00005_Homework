// Tick-rate measurement, editor half. Runs Play mode once per tick rate (10, 30, 60),
// then puts the tick rate back to 30. Results: Logs/pong_tick_<rate>.txt
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PongTickBench
{
    private const string QueueKey = "PongBench.Queue";
    private const string CurrentKey = "PongBench.Current";

    static PongTickBench()
    {
        EditorApplication.update += Poll;
    }

    [MenuItem("Pong/Measure tick rates (10, 30, 60)")]
    public static void StartMeasurement()
    {
        foreach (int t in new[] { 10, 30, 60 })
            if (File.Exists(PongTickBenchRunner.ResultPath(t))) File.Delete(PongTickBenchRunner.ResultPath(t));
        foreach (int t in new[] { 10, 30, 60 }) SessionState.EraseBool("PongBench.Started_" + t);
        SessionState.SetString(QueueKey, "10,30,60");
        Debug.Log("[TickBench] Measuring tick rates 10, 30, 60 (about 15 s each). Do not touch Play.");
    }

    private static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        string queue = SessionState.GetString(QueueKey, "");
        if (string.IsNullOrEmpty(queue)) return;

        string[] items = queue.Split(',');
        int tick = int.Parse(items[0]);

        if (File.Exists(PongTickBenchRunner.ResultPath(tick)))
        {
            SessionState.SetString(QueueKey, string.Join(",", items.Skip(1)));
            if (items.Length == 1) Finish();
            return;
        }

        string startedKey = "PongBench.Started_" + tick;
        if (SessionState.GetBool(startedKey, false))
        {
            Debug.LogError($"[TickBench] Play mode for tick {tick} ended without a result. Stopping.");
            Finish();
            return;
        }

        PongSetup.SetStatsAndTick(tick);
        SessionState.SetInt(CurrentKey, tick);
        SessionState.SetBool(startedKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Finish()
    {
        SessionState.EraseString(QueueKey);
        SessionState.EraseInt(CurrentKey);
        PongSetup.SetStatsAndTick(30);
        Debug.Log("[TickBench] Done. Tick rate back to 30. Results in Logs/pong_tick_*.txt");
    }
}
