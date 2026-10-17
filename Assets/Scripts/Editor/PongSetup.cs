// Pong — one-click editor setup for the homework steps that are pure Inspector work.
// Menu: Pong -> Step 4 / Step 6 / Step 7 / Step 10. Each item is safe to run again.
using System.IO;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PongSetup
{
    private const string ScenePath = "Assets/Scenes/Pong.unity";
    private const string PaddlePath = "Assets/Prefabs/Paddle.prefab";
    private const string BallPath = "Assets/Prefabs/Ball.prefab";

    // ---------------- Step 4 ----------------
    [MenuItem("Pong/Step 4 - Paddle becomes a NetworkObject")]
    public static void Step4()
    {
        if (!OpenPongScene()) return;
        Directory.CreateDirectory("Assets/Prefabs");

        GameObject paddle = GameObject.Find("Paddle Left") ?? GameObject.Find("Paddle");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PaddlePath);

        if (paddle != null)
        {
            // If the scene paddle is already an instance of some other prefab, cut that link,
            // otherwise the saved Paddle prefab would be a variant that depends on it.
            if (PrefabUtility.IsPartOfPrefabInstance(paddle))
                PrefabUtility.UnpackPrefabInstance(paddle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Remove<PaddleMovement>(paddle);
            Remove<BoxCollider>(paddle);
            GetOrAdd<NetworkObject>(paddle);
            GetOrAdd<PaddleController>(paddle);
            paddle.name = "Paddle";
            paddle.transform.position = new Vector3(0f, 0.5f, 0f);
            prefab = PrefabUtility.SaveAsPrefabAsset(paddle, PaddlePath);
            Object.DestroyImmediate(paddle);           // no copy may stay in the scene
        }
        if (prefab == null) { Debug.LogError("[Pong] No 'Paddle Left' in the scene and no Paddle prefab."); return; }

        NetworkManager nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null) { Debug.LogError("[Pong] No NetworkManager in the scene (Step 2)."); return; }
        var spawner = GetOrAdd<PlayerSpawner>(nm.gameObject);
        var so = new SerializedObject(spawner);
        so.FindProperty("_playerPrefab").objectReferenceValue = prefab.GetComponent<NetworkObject>();
        so.ApplyModifiedPropertiesWithoutUndo();

        Finish("Step 4 done: Paddle prefab in Assets/Prefabs, PlayerSpawner set. Play -> Start Server -> Start Client.");
    }

    // Rebuilds the Paddle prefab from scratch (standalone, not a variant) and re-links the spawner.
    [MenuItem("Pong/Repair - Rebuild Paddle prefab")]
    public static void RebuildPaddle()
    {
        if (!OpenPongScene()) return;
        Directory.CreateDirectory("Assets/Prefabs");

        GameObject paddle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paddle.name = "Paddle";
        paddle.transform.position = new Vector3(0f, 0.5f, 0f);
        paddle.transform.localScale = new Vector3(0.3f, 0.5f, Court.PaddleHalfLength * 2);
        Object.DestroyImmediate(paddle.GetComponent<Collider>());
        paddle.AddComponent<NetworkObject>();
        paddle.AddComponent<PaddleController>();
        ConfigureNetworkTransform(paddle.AddComponent<NetworkTransform>(), clientAuthoritative: true, teleport: false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(paddle, PaddlePath);
        Object.DestroyImmediate(paddle);

        NetworkManager nm = Object.FindFirstObjectByType<NetworkManager>();
        var spawner = GetOrAdd<PlayerSpawner>(nm.gameObject);
        var so = new SerializedObject(spawner);
        so.FindProperty("_playerPrefab").objectReferenceValue = prefab.GetComponent<NetworkObject>();
        so.ApplyModifiedPropertiesWithoutUndo();

        Finish("Paddle prefab rebuilt (NetworkObject + PaddleController + NetworkTransform) and set in PlayerSpawner.");
    }

    // ---------------- Step 6 ----------------
    [MenuItem("Pong/Step 6 - NetworkTransform on the Paddle prefab")]
    public static void Step6()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PaddlePath) == null) { Debug.LogError("[Pong] Run Step 4 first."); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(PaddlePath);
        ConfigureNetworkTransform(GetOrAdd<NetworkTransform>(root), clientAuthoritative: true, teleport: false);
        PrefabUtility.SaveAsPrefabAsset(root, PaddlePath);
        PrefabUtility.UnloadPrefabContents(root);
        Finish("Step 6 done: NetworkTransform (client authoritative, interpolation 2, no rotation/scale) on the Paddle prefab.");
    }

    // ---------------- Step 7 ----------------
    [MenuItem("Pong/Step 7 - Ball prefab, BallSpawner, GameState")]
    public static void Step7()
    {
        if (!OpenPongScene()) return;
        Directory.CreateDirectory("Assets/Prefabs");

        // Ball prefab
        GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPath);
        if (ballPrefab == null)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.position = new Vector3(0f, Court.BallY, 0f);
            ball.transform.localScale = Vector3.one * 0.5f;
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.AddComponent<NetworkObject>();
            ball.AddComponent<NetworkBall>();
            ConfigureNetworkTransform(ball.AddComponent<NetworkTransform>(), clientAuthoritative: false, teleport: true);
            ballPrefab = PrefabUtility.SaveAsPrefabAsset(ball, BallPath);
            Object.DestroyImmediate(ball);
        }

        // Court: NetworkObject + BallSpawner, Local Ball unticked
        Court court = Object.FindFirstObjectByType<Court>();
        if (court == null) { Debug.LogError("[Pong] No Court in the scene."); return; }
        GetOrAdd<NetworkObject>(court.gameObject);
        var courtSo = new SerializedObject(court);
        courtSo.FindProperty("localBall").boolValue = false;
        courtSo.ApplyModifiedPropertiesWithoutUndo();
        var spawner = GetOrAdd<BallSpawner>(court.gameObject);
        spawner.BallPrefab = ballPrefab.GetComponent<NetworkObject>();
        EditorUtility.SetDirty(spawner);

        // GameState
        GameState gs = Object.FindFirstObjectByType<GameState>();
        if (gs == null)
        {
            var go = new GameObject("GameState");
            go.AddComponent<NetworkObject>();
            go.AddComponent<GameState>();
        }

        Finish("Step 7 done: Ball prefab, BallSpawner on Court (Local Ball off), GameState object.");
    }

    // ---------------- Step 10 ----------------
    [MenuItem("Pong/Step 10 - Statistics on, tick rate 30")]
    public static void Step10() => SetStatsAndTick(30);

    [MenuItem("Pong/Tick rate 10 (measure)")]
    public static void Tick10() => SetStatsAndTick(10);

    [MenuItem("Pong/Tick rate 60 (measure)")]
    public static void Tick60() => SetStatsAndTick(60);

    public static void SetStatsAndTick(int tickRate)
    {
        if (!OpenPongScene()) return;
        NetworkManager nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null) { Debug.LogError("[Pong] No NetworkManager in the scene."); return; }

        var stats = new SerializedObject(GetOrAdd<StatisticsManager>(nm.gameObject));
        stats.FindProperty("_networkTraffic._enableMode").enumValueIndex = 1; // Development
        stats.FindProperty("_networkTraffic._updateClient").boolValue = true;
        stats.FindProperty("_networkTraffic._updateServer").boolValue = true;
        stats.ApplyModifiedPropertiesWithoutUndo();

        var time = new SerializedObject(GetOrAdd<TimeManager>(nm.gameObject));
        time.FindProperty("_tickRate").intValue = tickRate;
        time.ApplyModifiedPropertiesWithoutUndo();

        Finish($"Statistics on (Development, client + server), tick rate {tickRate}.");
    }

    // ---------------- helpers ----------------
    private static void ConfigureNetworkTransform(NetworkTransform nt, bool clientAuthoritative, bool teleport)
    {
        var so = new SerializedObject(nt);
        so.FindProperty("_clientAuthoritative").boolValue = clientAuthoritative;
        so.FindProperty("_interpolation").intValue = 2;
        so.FindProperty("_synchronizeRotation").boolValue = false;
        so.FindProperty("_synchronizeScale").boolValue = false;
        so.FindProperty("_enableTeleport").boolValue = teleport;   // ball snaps back to the centre after a goal
        so.FindProperty("_teleportThreshold").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool OpenPongScene()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Pong] Stop Play mode first."); return false; }
        if (EditorSceneManager.GetActiveScene().path == ScenePath) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(ScenePath);
        return true;
    }

    private static void Finish(string message)
    {
        EditorApplication.ExecuteMenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs");
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Pong] " + message);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void Remove<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c != null) Object.DestroyImmediate(c);
    }
}
