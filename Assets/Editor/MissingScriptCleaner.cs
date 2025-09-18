#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MissingScriptCleaner
{
    [MenuItem("Tools/Tutorial/Remove Missing Scripts In Scene")]
    public static void RemoveAll()
    {
        int totalRemoved = 0;

        // ✅ ใช้ API ใหม่
        var all = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (var go in all)
        {
            var so = new SerializedObject(go);
            var comps = so.FindProperty("m_Component");
            int removedHere = 0;

            for (int i = comps.arraySize - 1; i >= 0; i--)
            {
                var compProp = comps.GetArrayElementAtIndex(i).FindPropertyRelative("component");
                if (compProp.objectReferenceValue == null)
                {
                    comps.DeleteArrayElementAtIndex(i);
                    removedHere++;
                }
            }

            if (removedHere > 0)
            {
                so.ApplyModifiedProperties();
                totalRemoved += removedHere;
            }
        }

        Debug.Log($"[Cleaner] Removed {totalRemoved} missing script components.");
    }
}
#endif
