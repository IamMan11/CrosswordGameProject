using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialGameMode))]
public class TutorialGameModeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var t = target as TutorialGameMode;
        GUILayout.Space(8);
        GUILayout.Label("Runtime Controls", EditorStyles.boldLabel);
        if (GUILayout.Button("Start Runtime Tutorial"))
        {
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            // Delay call until play mode
            EditorApplication.delayCall += () => { if (t) t.StartRuntimeTutorial(); };
        }
        if (GUILayout.Button("Stop Runtime Tutorial"))
        {
            if (EditorApplication.isPlaying) EditorApplication.delayCall += () => { if (t) t.StopRuntimeTutorial(); };
        }

        GUILayout.Space(6);
        if (GUILayout.Button("Apply Fixture Now")) { if (t) t.ApplyFixturePublic(); }
        if (GUILayout.Button("Restore Disabled Objects")) { if (t) t.RestoreDisabledPublic(); }

        GUILayout.Space(6);
        if (GUILayout.Button("Generate Runtime Asset"))
        {
            var asset = t.GenerateRuntimeAsset();
            string path = "Assets/Tutorials/TempRuntimeTutorial.asset";
            AssetDatabase.CreateAsset(Object.Instantiate(asset), path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TutorialDialogAsset>(path);
            Debug.Log("[TutorialGameModeEditor] Generated temp runtime asset at " + path);
        }
    }
}


