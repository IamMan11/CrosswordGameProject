#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TutorialTools
{
    [MenuItem("Tools/Tutorial/Clear Tutorial Keys")]
    public static void ClearKeys()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] Cleared all PlayerPrefs.");
    }

    [MenuItem("Tools/Tutorial/Add Tutorial Game Mode to Scene")]
    public static void AddTutorialGameMode()
    {
        var go = new GameObject("TutorialGameMode");
        Undo.RegisterCreatedObjectUndo(go, "Add TutorialGameMode");
        var gm = go.AddComponent<TutorialGameMode>();
        Selection.activeGameObject = go;
        Debug.Log("[TutorialTools] Added TutorialGameMode. Assign tutorialAsset in Inspector.");
    }

    [MenuItem("Tools/Tutorial/Run Tutorial Mode (Play)")]
    public static void RunTutorialModePlay()
    {
        EditorApplication.isPlaying = true;
        Debug.Log("[TutorialTools] Entering Play Mode to run Tutorial Mode.");
    }
}
#endif
