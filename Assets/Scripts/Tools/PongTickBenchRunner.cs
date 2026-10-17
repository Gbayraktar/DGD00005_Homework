// Tick-rate measurement, play-mode half (editor only). Started by PongTickBench (Editor).
// Starts server + client on this machine, keeps serving the ball, samples the server's
// outgoing bytes per second for 10 seconds, writes Logs/pong_tick_<rate>.txt, leaves Play mode.
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet;
using FishNet.Managing;
using UnityEngine;

public class PongTickBenchRunner : MonoBehaviour
{
    private int _tick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        int tick = UnityEditor.SessionState.GetInt("PongBench.Current", 0);
        if (tick <= 0 || File.Exists(ResultPath(tick))) return;
        var go = new GameObject("TickBench");
        go.AddComponent<PongTickBenchRunner>()._tick = tick;
    }

    public static string ResultPath(int tick) => $"Logs/pong_tick_{tick}.txt";

    private IEnumerator Start()
    {
        NetworkManager nm = InstanceFinder.NetworkManager;
        nm.ServerManager.StartConnection();
        yield return WaitFor(() => nm.IsServerStarted, 10f);
        nm.ClientManager.StartConnection("localhost");
        yield return WaitFor(() => nm.IsClientStarted && NetworkBall.Instance != null, 10f);
        yield return new WaitForSeconds(2f);                     // warm-up

        var samples = new List<ulong>();
        var clientIn = new List<ulong>();
        int lastSecond = ConnectionHud.CompletedSeconds;
        float end = Time.unscaledTime + 10.5f;
        while (Time.unscaledTime < end)
        {
            NetworkBall ball = NetworkBall.Instance;
            if (ball != null && !ball.InPlay.Value) ball.Serve(PaddleController.Right);   // keep a rally going
            if (ConnectionHud.CompletedSeconds != lastSecond)
            {
                lastSecond = ConnectionHud.CompletedSeconds;
                samples.Add(ConnectionHud.ServerOutPerSecond);
                clientIn.Add(ConnectionHud.ClientInPerSecond);
            }
            yield return null;
        }

        string text =
            $"tick={_tick}\n" +
            $"timeManagerTickRate={nm.TimeManager.TickRate}\n" +
            $"serverOutAvg={(samples.Count > 0 ? samples.Average(v => (double)v) : 0):F0}\n" +
            $"serverOutSamples={string.Join(",", samples)}\n" +
            $"clientInSamples={string.Join(",", clientIn)}\n";
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ResultPath(_tick), text);
        Debug.Log("[TickBench] " + text.Replace("\n", "  "));
        UnityEditor.EditorApplication.ExitPlaymode();
    }

    private static IEnumerator WaitFor(System.Func<bool> condition, float timeout)
    {
        float until = Time.unscaledTime + timeout;
        while (!condition() && Time.unscaledTime < until) yield return null;
    }
}
#endif
