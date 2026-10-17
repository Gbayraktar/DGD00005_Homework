// Pong — player settings for a networked build + one-click Windows build.
// Menu: Pong -> Build Windows (Builds/Pong/Pong.exe). Builds/ is ignored by git.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class PongBuild
{
    private const string ScenePath = "Assets/Scenes/Pong.unity";
    private const string OutPath = "Builds/Pong/Pong.exe";

    [MenuItem("Pong/Build Windows")]
    public static void BuildFromMenu()
    {
        string result = BuildWindows();
        EditorUtility.DisplayDialog("Pong build", result, "OK");
    }

    public static void ConfigurePlayer()
    {
        PlayerSettings.productName = "Pong";
        PlayerSettings.runInBackground = true;               // an unfocused window must keep networking
        PlayerSettings.visibleInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.forceSingleInstance = false;          // allow two copies on one laptop

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        // Court.Paint uses Shader.Find("Universal Render Pipeline/Unlit"): make sure the build keeps it.
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit != null)
        {
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty list = gs.FindProperty("m_AlwaysIncludedShaders");
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == unlit) present = true;
            if (!present)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = unlit;
                gs.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        AssetDatabase.SaveAssets();
    }

    public static string BuildWindows()
    {
        ConfigurePlayer();
        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        string result = report.summary.result == BuildResult.Succeeded
            ? $"Succeeded: {Path.GetFullPath(OutPath)} ({report.summary.totalSize / (1024 * 1024)} MB)"
            : $"{report.summary.result}: {report.summary.totalErrors} errors\n" +
              string.Join("\n", report.steps.SelectMany(s => s.messages)
                  .Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(20));
        Debug.Log("[Pong build] " + result);
        return result;
    }
}
