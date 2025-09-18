using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateTutorialScene
{
    [MenuItem("Tools/Tutorial/Create Tutorial Practice Scene")]
    public static void CreateScene()
    {
        // Create new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Build tutorial UI
        TutorialUIBuilder_RPG.CreateRpgDialogUI();

        // Try to ensure Tutorials folder exists
        string folder = "Assets/Tutorials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Tutorials");

        // Ensure gameplay tutorial asset exists
        string assetPath = folder + "/GameplayTutorial.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TutorialDialogAsset>(assetPath);
        if (asset == null)
        {
            // Call the creator to generate an example asset
            CreateGameplayTutorialAsset.CreateAsset();
            asset = AssetDatabase.LoadAssetAtPath<TutorialDialogAsset>(assetPath);
        }

        // Add TutorialGameMode
        var go = new GameObject("TutorialGameMode");
        var gm = go.AddComponent<TutorialGameMode>();
        if (asset != null) gm.tutorialAsset = asset;

        // --- Try to create a BoardManager and board parent, and assign a BoardSlot prefab if found ---
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            // create Board parent under Canvas
            var boardParentGO = new GameObject("BoardParent", typeof(RectTransform));
            boardParentGO.transform.SetParent(canvas.transform, false);
            var boardRT = boardParentGO.GetComponent<RectTransform>();
            boardRT.anchorMin = boardRT.anchorMax = new Vector2(0.5f, 0.5f);
            boardRT.anchoredPosition = new Vector2(0, 0);

            // create BoardManager
            var bmGO = new GameObject("BoardManager");
            var bm = bmGO.AddComponent<BoardManager>();
            bm.boardParent = boardRT;

            // attempt to find a prefab that contains BoardSlot component
            string[] guids = AssetDatabase.FindAssets("t:prefab");
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null) continue;
                if (prefab.GetComponentInChildren<BoardSlot>() != null || prefab.GetComponent<BoardSlot>() != null)
                {
                    bm.boardSlotPrefab = prefab;
                    Debug.Log($"[CreateTutorialScene] Assigned boardSlotPrefab from {p}");
                    break;
                }
            }

            // generate board if prefab assigned
            if (bm.boardSlotPrefab != null)
                bm.GenerateBoard();

            // Try to instantiate common manager prefabs (BenchManager, TileBag, TurnManager, CurrencyManager, CardManager, LevelManager, UIManager)
            var managerTypes = new System.Type[] {
                typeof(BenchManager), typeof(TileBag), typeof(TurnManager), typeof(CurrencyManager),
                typeof(CardManager), typeof(LevelManager), typeof(UIManager)
            };

            foreach (var type in managerTypes)
            {
                // skip if already present in scene
                if (Object.FindObjectOfType(type) != null) continue;

                // search prefabs for one that contains this component
                bool instantiated = false;
                foreach (var g in guids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (prefab == null) continue;
                    if (prefab.GetComponentInChildren(type) != null || prefab.GetComponent(type) != null)
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        inst.name = prefab.name;
                        Debug.Log($"[CreateTutorialScene] Instantiated manager prefab {p} for type {type.Name}");
                        instantiated = true;
                        break;
                    }
                }
                if (!instantiated)
                    Debug.LogWarning($"[CreateTutorialScene] No prefab found for manager type {type.Name} (scene may miss manager)");
            }
        }

        // Save scene
        string sceneFolder = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(sceneFolder)) AssetDatabase.CreateFolder("Assets", "Scenes");
        string scenePath = sceneFolder + "/Tutorial_Practice.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log("[CreateTutorialScene] Created scene: " + scenePath);
        EditorSceneManager.OpenScene(scenePath);
    }
}


