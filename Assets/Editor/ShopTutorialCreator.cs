using UnityEditor;
using UnityEngine;

public class ShopTutorialCreator
{
    [MenuItem("Tools/Tutorial Tools/Create Shop Tutorial Binder")]
    public static void CreateBinderInScene()
    {
        // Create GameObject
        var go = new GameObject("ShopTutorialBinder");
        Undo.RegisterCreatedObjectUndo(go, "Create ShopTutorialBinder");

        var binder = go.AddComponent<ShopTutorialBinder>();

        // try to auto-find common UI elements by name
        var coins = GameObject.Find("CoinsPanel");
        if (coins) binder.coinsPanel = coins.GetComponent<RectTransform>();

        var reroll = GameObject.Find("RerollButton");
        if (reroll) binder.rerollButton = reroll.GetComponent<UnityEngine.UI.Button>();

        var firstCard = GameObject.Find("FirstCard");
        if (firstCard) binder.firstCardRect = firstCard.GetComponent<RectTransform>();

        var firstBuy = GameObject.Find("FirstBuyButton");
        if (firstBuy) binder.firstBuyButton = firstBuy.GetComponent<UnityEngine.UI.Button>();

        var next = GameObject.Find("NextButton");
        if (next) binder.nextButton = next.GetComponent<UnityEngine.UI.Button>();

        // Try to hook TutorialManager if present
        var tm = Object.FindObjectOfType<TutorialManager>();
        if (tm) binder.tm = tm;

        Selection.activeGameObject = go;

        Debug.Log("[ShopTutorialCreator] Created ShopTutorialBinder. Please verify assigned references in the Inspector.");
    }
}


