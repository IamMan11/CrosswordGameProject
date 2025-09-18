using UnityEditor;
using UnityEngine;

public static class CreateShopTutorialAsset
{
    [MenuItem("Tools/Tutorial Tools/Create Example Shop Tutorial Asset")]
    public static void CreateAsset()
    {
        var asset = ScriptableObject.CreateInstance<TutorialDialogAsset>();

        asset.steps = new TutorialDialogAsset.StepData[4];

        asset.steps[0] = new TutorialDialogAsset.StepData {
            id = "SHOP_INTRO",
            message = "ยินดีต้อนรับสู่ร้านค้า! นี่คือที่คุณใช้เหรียญเพื่อซื้อการ์ดและอัปเกรด",
            useCharacter = true,
            speakerName = "โค้ชครอส",
            highlightTargetName = "CoinsPanel",
            blockInput = true,
            tapAnywhereToContinue = true,
            showSpotlightOnAllowed = false
        };

        asset.steps[1] = new TutorialDialogAsset.StepData {
            id = "SHOP_REROLL",
            message = "กดปุ่ม REROLL เพื่อสุ่มรายการการ์ดใหม่",
            useCharacter = true,
            highlightTargetName = "RerollButton",
            blockInput = true,
            tapAnywhereToContinue = false,
            allowClickOnHighlight = true,
            allowClickExtraNames = new string[0],
            allowClickPadding = 18f,
            waitFor = TutorialManager.WaitType.CardRolled
        };

        asset.steps[2] = new TutorialDialogAsset.StepData {
            id = "SHOP_BUY_FIRST",
            message = "เลือกซื้อการ์ดใบแรกด้วยปุ่ม BUY",
            useCharacter = true,
            highlightTargetName = "FirstCard",
            blockInput = true,
            tapAnywhereToContinue = false,
            allowClickOnHighlight = true,
            allowClickExtraNames = new string[] { "FirstBuyButton" },
            allowClickPadding = 18f,
            waitFor = TutorialManager.WaitType.ShopItemPurchased
        };

        asset.steps[3] = new TutorialDialogAsset.StepData {
            id = "SHOP_LEAVE",
            message = "เสร็จแล้ว กด NEXT เพื่อออกจากร้าน",
            useCharacter = true,
            highlightTargetName = "NextButton",
            blockInput = true,
            pressNextToContinue = true,
            waitFor = TutorialManager.WaitType.ConfirmPressed
        };

        string folder = "Assets/Tutorials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Tutorials");

        string path = folder + "/ShopTutorial.asset";
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Tutorial] Created example Shop tutorial asset at {path}");
        // Delay selection to avoid UIElements refresh race in Inspector
        EditorApplication.delayCall += () => Selection.activeObject = asset;
    }
}


