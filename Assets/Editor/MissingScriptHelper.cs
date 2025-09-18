using UnityEditor;
using UnityEngine;
using System.Reflection;

public static class MissingScriptHelper
{
    [MenuItem("Tools/Tutorial Tools/Find Missing Scripts In Scene")]
    public static void FindMissingInScene()
    {
        int found = 0;
        var gos = Object.FindObjectsOfType<GameObject>();
        foreach (var go in gos)
        {
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    Debug.LogWarning($"Missing script on GameObject: {GetFullPath(go)}", go);
                    found++;
                }
            }
        }
        Debug.Log($"[MissingScriptHelper] Found {found} missing script component(s) in scene.");
    }

    [MenuItem("Tools/Tutorial Tools/Remove Missing Scripts In Scene")] 
    public static void RemoveMissingInScene()
    {
        if (!EditorUtility.DisplayDialog("Remove Missing Scripts", "Remove all missing script components in the current scene?", "Remove", "Cancel")) return;
        int removed = 0;
        var gos = Object.FindObjectsOfType<GameObject>();
        // Use reflection to call RemoveMonoBehavioursWithMissingScript to handle different return types across Unity versions
        var method = typeof(GameObjectUtility).GetMethod("RemoveMonoBehavioursWithMissingScript", BindingFlags.Public | BindingFlags.Static, null, new System.Type[] { typeof(GameObject) }, null);
        foreach (var go in gos)
        {
            if (method != null)
            {
                try
                {
                    var ret = method.Invoke(null, new object[] { go });
                    if (ret is int ic && ic > 0) removed++;
                    else if (ret is bool b && b) removed++;
                }
                catch { }
            }
            else
            {
                // no suitable API found via reflection; skip
            }
        }
        Debug.Log($"[MissingScriptHelper] Removed missing script components from {removed} GameObjects.");
    }

    static string GetFullPath(GameObject go)
    {
        string path = go.name;
        var t = go.transform;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}


