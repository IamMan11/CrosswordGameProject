using UnityEditor;
using UnityEngine;

public static class CreateGameplayTutorialAsset
{
    [MenuItem("Tools/Tutorial/Create Example Gameplay Tutorial Asset")]
    public static void CreateAsset()
    {
        var asset = ScriptableObject.CreateInstance<TutorialDialogAsset>();

        asset.steps = new TutorialDialogAsset.StepData[7];

        asset.steps[0] = new TutorialDialogAsset.StepData {
            id = "GT_DRAG_FIRST_TILE",
            message = "ลากตัวอักษรตัวแรกมาวางที่บอร์ด",
            useCharacter = true,
            speakerName = "โค้ช",
            highlightTargetName = "TileSlot1",
            blockInput = true,
            tapAnywhereToContinue = false,
            allowClickOnHighlight = true,
            waitFor = TutorialManager.WaitType.TilePlacedOnBoard
        };

        asset.steps[1] = new TutorialDialogAsset.StepData {
            id = "GT_PRESS_CONFIRM",
            message = "กดปุ่มยืนยันเมื่อพร้อม",
            useCharacter = true,
            highlightTargetName = "ConfirmButton",
            blockInput = true,
            tapAnywhereToContinue = false,
            allowClickOnHighlight = true,
            waitFor = TutorialManager.WaitType.ConfirmPressed
        };

        asset.steps[2] = new TutorialDialogAsset.StepData {
            id = "GT_SCORE_INCREASE",
            message = "คะแนนของคุณเพิ่มขึ้นแล้ว",
            useCharacter = true,
            blockInput = true,
            tapAnywhereToContinue = true,
            waitFor = TutorialManager.WaitType.ScoreIncreased
        };

        asset.steps[3] = new TutorialDialogAsset.StepData {
            id = "GT_OPEN_DICT",
            message = "เปิดพจนานุกรมเพื่อตรวจคำ",
            useCharacter = true,
            highlightTargetName = "DictionaryButton",
            blockInput = true,
            tapAnywhereToContinue = false,
            allowClickOnHighlight = true,
            waitFor = TutorialManager.WaitType.DictionaryOpened
        };

        asset.steps[4] = new TutorialDialogAsset.StepData {
            id = "GT_USE_CARD",
            message = "ลองใช้การ์ดพิเศษนี้",
            useCharacter = true,
            highlightTargetName = "CardSlot1",
            blockInput = true,
            tapAnywhereToContinue = false,
            allowClickOnHighlight = true,
            waitFor = TutorialManager.WaitType.CardUsed
        };

        asset.steps[5] = new TutorialDialogAsset.StepData {
            id = "GT_REROLL",
            message = "กด REROLL เพื่อสุ่มการ์ดใหม่",
            useCharacter = true,
            highlightTargetName = "RerollButton",
            blockInput = true,
            tapAnywhereToContinue = false,
            allowClickOnHighlight = true,
            waitFor = TutorialManager.WaitType.CardRolled
        };

        asset.steps[6] = new TutorialDialogAsset.StepData {
            id = "GT_LEVEL_GOAL",
            message = "เป้าหมายของด่านนี้คือ: ทำคะแนนให้ครบ",
            useCharacter = true,
            blockInput = true,
            tapAnywhereToContinue = true
        };

        string folder = "Assets/Tutorials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Tutorials");

        string path = folder + "/GameplayTutorial.asset";
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorApplication.delayCall += () => Selection.activeObject = asset;
        Debug.Log($"[Tutorial] Created example Gameplay tutorial asset at {path}");
    }
}


