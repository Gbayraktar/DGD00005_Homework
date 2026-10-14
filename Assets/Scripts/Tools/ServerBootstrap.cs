// Pong — starts the server automatically when this instance should be a server (Tools):
//  - a Multiplayer Play Mode player tagged "Server" (Step 5), or
//  - a headless / batch-mode build, or one started with -server on the command line.
// Otherwise it does nothing and you use the HUD buttons.
using System;
using System.Linq;
using System.Reflection;
using FishNet;
using FishNet.Managing;
using UnityEngine;

public class ServerBootstrap : MonoBehaviour
{
    private void Start()
    {
        NetworkManager nm = InstanceFinder.NetworkManager;
        if (nm == null) return;

        string reason = null;
        if (Application.isBatchMode) reason = "headless";
        else if (Environment.GetCommandLineArgs().Contains("-server")) reason = "-server argument";
        else if (HasPlayModeTag("Server")) reason = "Multiplayer Play Mode tag 'Server'";

        if (reason == null) return;

        Debug.Log($"[Server] ServerBootstrap: starting server ({reason})");
        nm.ServerManager.StartConnection();
    }

    // Reads Multiplayer Play Mode's CurrentPlayer tags without a hard reference,
    // so the project still compiles if the package is missing.
    private static bool HasPlayModeTag(string tag)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.GetName().Name.StartsWith("Unity.Multiplayer.Playmode", StringComparison.OrdinalIgnoreCase))
                continue;

            Type t = asm.GetType("Unity.Multiplayer.PlayMode.CurrentPlayer") ?? asm.GetType("Unity.Multiplayer.Playmode.CurrentPlayer");
            if (t == null) continue;

            MethodInfo m = t.GetMethod("ReadOnlyTags", BindingFlags.Public | BindingFlags.Static);
            object result = m?.Invoke(null, null);
            if (result is System.Collections.IEnumerable tags)
                foreach (object o in tags)
                    if (string.Equals(o?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                        return true;
        }
        return false;
    }
}
