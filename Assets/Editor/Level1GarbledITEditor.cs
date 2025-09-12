#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Level1GarbledIT))]
public class Level1GarbledITEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var t = (Level1GarbledIT)target;

        GUILayout.Space(8);
        if (GUILayout.Button("Copy Letter Database from TileBag"))
        {
            var m = typeof(Level1GarbledIT).GetMethod("TryAutoFillLetterDatabaseFromTileBag",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (m != null)
            {
                Undo.RecordObject(t, "Copy Letter Database from TileBag");
                m.Invoke(t, null);
                EditorUtility.SetDirty(t);
                Debug.Log("[GarbledIT] Copied LetterData list from TileBag.");
            }
            else
            {
                Debug.LogWarning("Method TryAutoFillLetterDatabaseFromTileBag not found.");
            }
        }
    }
}
#endif
