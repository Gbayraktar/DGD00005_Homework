// Pong — builds the single-player start scene (replaces Pong-Start.unitypackage).
// Menu: Pong -> Build Pong scene. Creates Assets/Scenes/Pong.unity with
// a top-down camera, the Court, and two paddles (W/S left, arrows right).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PongSceneBuilder
{
    [MenuItem("Pong/Build Pong scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera looking straight down at the court
        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        var c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
        c.orthographic = true;
        c.orthographicSize = 6.5f;
        cam.transform.position = new Vector3(0, 20, 0);
        cam.transform.rotation = Quaternion.Euler(90, 0, 0);

        // Court (builds floor, walls and the local ball at runtime)
        new GameObject("Court").AddComponent<Court>();

        MakePaddle("Paddle Left", 0);
        MakePaddle("Paddle Right", 1);

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Pong.unity");
        Debug.Log("[Pong] Scene saved to Assets/Scenes/Pong.unity. Press Play.");
    }

    private static void MakePaddle(string name, int side)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = new Vector3(side == 0 ? -Court.PaddleX : Court.PaddleX, 0.5f, 0);
        go.transform.localScale = new Vector3(0.3f, 0.5f, Court.PaddleHalfLength * 2);
        var mv = go.AddComponent<PaddleMovement>();
        var so = new SerializedObject(mv);
        so.FindProperty("side").intValue = side;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
