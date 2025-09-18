using UnityEditor;
using UnityEngine;

public static class TutorialSceneValidator
{
    [MenuItem("Tools/Tutorial/Validate Tutorial Scene")]
    public static void ValidateScene()
    {
        var neededTypes = new System.Type[] {
            typeof(BoardManager), typeof(BenchManager), typeof(TileBag), typeof(TurnManager), typeof(CurrencyManager),
            typeof(CardManager), typeof(LevelManager), typeof(UIManager), typeof(TutorialManager), typeof(TutorialGameMode)
        };

        bool allOk = true;
        string report = "Tutorial Scene Validation:\n";

        foreach (var t in neededTypes)
        {
            var inst = Object.FindObjectOfType(t);
            if (inst == null)
            {
                report += $"MISSING: {t.Name}\n";
                allOk = false;
            }
            else report += $"OK: {t.Name} found\n";
        }

        // Board checks
        var bm = Object.FindObjectOfType<BoardManager>();
        if (bm != null)
        {
            if (bm.boardSlotPrefab == null) { report += "BoardManager: boardSlotPrefab NOT assigned\n"; allOk = false; }
            else report += "BoardManager: boardSlotPrefab assigned\n";
            if (bm.grid == null) { report += "BoardManager: grid not generated (call GenerateBoard)\n"; allOk = false; }
            else report += "BoardManager: grid generated\n";
        }

        report += allOk ? "\nScene looks good." : "\nScene missing items - you can try auto-fix.";

        if (!allOk)
        {
            if (EditorUtility.DisplayDialog("Validate Tutorial Scene", report, "Auto-Fix Missing", "Show Report"))
            {
                AutoFixMissing(neededTypes);
            }
            else
            {
                Debug.Log(report);
            }
        }
        else
        {
            Debug.Log(report);
            EditorUtility.DisplayDialog("Validate Tutorial Scene", report, "OK");
        }
    }

    static void AutoFixMissing(System.Type[] typesToFind)
    {
        string[] guids = AssetDatabase.FindAssets("t:prefab");
        foreach (var t in typesToFind)
        {
            if (Object.FindObjectOfType(t) != null) continue;

            bool created = false;
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null) continue;
                if (prefab.GetComponentInChildren(t) != null || prefab.GetComponent(t) != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    inst.name = prefab.name;
                    Debug.Log($"[TutorialSceneValidator] Instantiated {prefab.name} for {t.Name}");
                    created = true;
                    break;
                }
            }
            if (!created) Debug.LogWarning($"[TutorialSceneValidator] Could not find prefab for type {t.Name}");
        }

        // After attempting to instantiate, try to auto-assign boardSlotPrefab if missing
        var bm = Object.FindObjectOfType<BoardManager>();
        if (bm != null && bm.boardSlotPrefab == null)
        {
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null) continue;
                if (prefab.GetComponentInChildren<BoardSlot>() != null || prefab.GetComponent<BoardSlot>() != null)
                {
                    bm.boardSlotPrefab = prefab;
                    Debug.Log($"[TutorialSceneValidator] Assigned boardSlotPrefab from {p}");
                    bm.GenerateBoard();
                    break;
                }
            }
            if (bm.boardSlotPrefab == null) Debug.LogWarning("[TutorialSceneValidator] boardSlotPrefab still not found.");
        }

        EditorUtility.DisplayDialog("Auto-Fix Complete", "Auto-fix attempted. Check Console for details.", "OK");
    }
}


