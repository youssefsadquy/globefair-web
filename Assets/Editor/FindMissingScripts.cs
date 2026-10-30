using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;

// Diagnostic-only editor utility, added while investigating the "Unknown error occurred while
// loading 'sharedassets1.assets'" runtime error thrown when starting the fair in the WebGL
// build. That error's stack trace passes through PPtr<MonoScript>::operator MonoScript*(),
// which is the exact code path Unity uses to resolve a MonoBehaviour's serialized script
// reference - a classic symptom of a "Missing Script" (a component whose script asset was
// deleted/renamed after being attached) somewhere in the active scene's object graph. This
// scans every GameObject in every open scene (including inactive/nested children) and logs
// the full hierarchy path of anything with a missing script component.
public static class FindMissingScripts
{
    [MenuItem("Tools/Find Missing Scripts In Open Scenes")]
    public static void Find()
    {
        int goCount = 0;
        int componentsCount = 0;
        int missingCount = 0;
        var sb = new StringBuilder();

        for (int s = 0; s < EditorSceneManager.sceneCount; s++)
        {
            var scene = EditorSceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            sb.AppendLine("Scanning scene: " + scene.name + " (" + scene.path + ")");
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                missingCount += ScanRecursive(root.transform, sb, ref goCount, ref componentsCount);
            }
        }

        sb.AppendLine(string.Format("Scan complete. GameObjects scanned: {0}, Components scanned: {1}, Missing scripts found: {2}", goCount, componentsCount, missingCount));
        Debug.Log(sb.ToString());

        if (missingCount == 0)
        {
            Debug.Log("<color=green>No missing scripts found in any open scene.</color>");
        }
        else
        {
            Debug.LogWarning("<color=red>Found " + missingCount + " missing script reference(s) - see paths logged above.</color>");
        }
    }

    private static int ScanRecursive(Transform t, StringBuilder sb, ref int goCount, ref int componentsCount)
    {
        goCount++;
        int localMissing = 0;
        var components = t.GetComponent<GameObject>() != null ? t.gameObject.GetComponents<Component>() : null;
        components = t.gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            componentsCount++;
            if (components[i] == null)
            {
                localMissing++;
                sb.AppendLine("MISSING SCRIPT at: " + GetPath(t) + "  (component index " + i + ")");
            }
        }

        for (int i = 0; i < t.childCount; i++)
        {
            localMissing += ScanRecursive(t.GetChild(i), sb, ref goCount, ref componentsCount);
        }

        return localMissing;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        var p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }
}
