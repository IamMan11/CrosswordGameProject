#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class TutorialUIBuilder_RPG
{
    const string RootName = "TutorialUI";

    [MenuItem("Tools/Tutorial/Create RPG Dialog UI")]
    public static void CreateRpgDialogUI()
    {
        // Canvas + EventSystem
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (!canvas)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            if (!Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        // กันซ้ำ
        var existed = canvas.transform.Find(RootName);
        if (existed) { Selection.activeGameObject = existed.gameObject; return; }

        // Root + TutorialManager (Character Edition ที่คุณใช้อยู่)
        var root = new GameObject(RootName);
        root.transform.SetParent(canvas.transform, false);
        var tm = root.AddComponent<TutorialManager>();

        // Overlay + CanvasGroup
        var overlay = CreateImage("Overlay", root.transform, stretch:true, col:new Color(0,0,0,0.6f));
        var cg = overlay.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0; cg.blocksRaycasts = false;
        SetPrivate(tm, "overlay", cg);

        // Skip button will be created after DialogPanel is constructed (so panel exists)

        // Continue-anywhere (โปร่งใส)
        var cont = CreateImage("ContinueAnywhereBtn", overlay.transform, stretch:true, col:new Color(1,1,1,0));
        var contBtn = cont.gameObject.AddComponent<Button>();
        SetPrivate(tm, "continueAnywhereBtn", contBtn);

        // Highlight frame
        var frame = CreateImage("HighlightFrame", root.transform, false, new Color(1f,0.9f,0.4f,0.95f));
        frame.raycastTarget = false; frame.rectTransform.sizeDelta = new Vector2(300,120);
        frame.gameObject.SetActive(false);
        SetPrivate(tm, "highlightFrame", frame.rectTransform);

        // Hand pointer
        var hand = CreateImage("HandPointer", root.transform, false, Color.white);
        hand.raycastTarget = false; hand.rectTransform.sizeDelta = new Vector2(64,64);
        hand.gameObject.AddComponent<HandPulse>();
        hand.gameObject.SetActive(false);
        SetPrivate(tm, "handPointer", hand.rectTransform);

        // Dialog panel (ล่างจอ)
        var panel = CreateImage("DialogPanel", root.transform, false, new Color(0.09f,0.12f,0.20f,0.88f));
        var pRT = panel.rectTransform;
        pRT.anchorMin = new Vector2(.05f,.05f); pRT.anchorMax = new Vector2(.95f,.33f);
        pRT.offsetMin = pRT.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<Outline>().effectColor = new Color(0,0,0,0.7f);
        panel.gameObject.AddComponent<Shadow>().effectColor  = new Color(0,0,0,0.5f);

        // Name tag (มุมบนซ้ายของ panel)
        var nameTag = CreateImage("NameTag", panel.transform, false, new Color(0.23f,0.42f,0.86f,0.95f));
        var nrt = nameTag.rectTransform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(0,1); nrt.pivot = new Vector2(0,1);
        nrt.anchoredPosition = new Vector2(20,24); nrt.sizeDelta = new Vector2(360,56);
        nameTag.gameObject.AddComponent<Outline>().effectColor = new Color(0,0,0,0.7f);

        var nameTMP = CreateTMP("Label", nameTag.transform, "Village Elder", 28, FontStyles.Bold);
        var nameTMPrt = nameTMP.rectTransform; nameTMPrt.anchorMin = Vector2.zero; nameTMPrt.anchorMax = Vector2.one;
        nameTMPrt.offsetMin = new Vector2(16,6); nameTMPrt.offsetMax = new Vector2(-16,-6);
        nameTMP.textWrappingMode = TextWrappingModes.NoWrap;

        // Portrait (ซ้าย)
        var portrait = CreateImage("Portrait", panel.transform, false, Color.white);
        var portRT = portrait.rectTransform;
        portRT.anchorMin = new Vector2(0,0); portRT.anchorMax = new Vector2(0,1); portRT.pivot = new Vector2(0,.5f);
        portRT.sizeDelta = new Vector2(220,0); portRT.anchoredPosition = new Vector2(20,0);

        // BodyText (ขวา)
        var body = CreateTMP("BodyText", panel.transform,
            "Greetings, hero! (กด Spacebar หรือปุ่ม 'ต่อไป')", 30, FontStyles.Normal);
        var bodyRT = body.rectTransform;
        bodyRT.anchorMin = new Vector2(0,0); bodyRT.anchorMax = new Vector2(1,1);
        bodyRT.offsetMin = new Vector2(260,24); bodyRT.offsetMax = new Vector2(-24,-64);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.alignment = TextAlignmentOptions.TopLeft;
        SetPrivate(tm, "bodyText", body);

        // ปุ่ม Next
        var nextGO = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button));
        nextGO.transform.SetParent(panel.transform, false);
        var nextRT = (RectTransform)nextGO.transform;
        nextRT.anchorMin = nextRT.anchorMax = new Vector2(1,0); nextRT.pivot = new Vector2(1,0);
        nextRT.anchoredPosition = new Vector2(-24,18); nextRT.sizeDelta = new Vector2(180,56);
        var nextBG = nextGO.GetComponent<Image>(); nextBG.color = new Color(0.95f,0.88f,0.35f,0.95f);
        var nextBtn = nextGO.GetComponent<Button>();
        var nextLbl = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        nextLbl.transform.SetParent(nextGO.transform, false);
        var nextTMP = nextLbl.GetComponent<TextMeshProUGUI>();
        nextTMP.text = "ต่อไป ▶"; nextTMP.fontSize = 28; nextTMP.color = Color.black;
        nextTMP.alignment = TextAlignmentOptions.Center;
        var nextLblRT = (RectTransform)nextLbl.transform;
        nextLblRT.anchorMin = Vector2.zero; nextLblRT.anchorMax = Vector2.one;
        nextLblRT.offsetMin = Vector2.zero; nextLblRT.offsetMax = Vector2.zero;
        nextGO.SetActive(false);
        SetPrivate(tm, "nextButton", nextBtn);

        // Prompt spacebar + caret เด้ง
        var prompt = CreateTMP("Prompt", panel.transform, "กด Spacebar เพื่อไปต่อ", 24, FontStyles.Italic);
        var prt = prompt.rectTransform; prt.anchorMin = prt.anchorMax = new Vector2(0,0); prt.pivot = new Vector2(0,0);
        prt.anchoredPosition = new Vector2(260,16); prt.sizeDelta = new Vector2(420,32); prompt.alpha = .85f;

        var caret = CreateTMP("Caret", panel.transform, "▼", 28, FontStyles.Bold);
        var crt = caret.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(.5f,0); crt.pivot = new Vector2(.5f,0);
        crt.anchoredPosition = new Vector2(0,8); caret.alpha = .9f; caret.gameObject.AddComponent<BounceY>();

        // AudioSource สำหรับเสียงพิมพ์
        var audio = panel.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 0; audio.volume = .4f;

        // ผูกเข้ากับ TutorialManager
        SetPrivate(tm, "characterPanel", panel.gameObject);
        SetPrivate(tm, "portraitImage", portrait);
        SetPrivate(tm, "nameText", nameTMP);
        SetPrivate(tm, "voiceSource", audio);
        SetPrivate(tm, "overlay", cg);

        // Skip button (create after panel so parent exists)
        var skipGO = new GameObject("SkipTutorialButton", typeof(RectTransform), typeof(Image), typeof(Button));
        skipGO.transform.SetParent(panel.transform, false);
        var skipRT = (RectTransform)skipGO.transform; skipRT.anchorMin = skipRT.anchorMax = new Vector2(1,1); skipRT.sizeDelta = new Vector2(120,40); skipRT.anchoredPosition = new Vector2(-140,-12);
        var skipImg = skipGO.GetComponent<Image>(); skipImg.color = new Color(0.8f,0.2f,0.2f,0.9f);
        var skipBtn = skipGO.GetComponent<Button>();
        var skipLbl = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); skipLbl.transform.SetParent(skipGO.transform, false);
        var skipTMP = skipLbl.GetComponent<TextMeshProUGUI>(); skipTMP.text = "ข้าม"; skipTMP.fontSize = 20; skipTMP.alignment = TextAlignmentOptions.Center; skipTMP.color = Color.white;
        SetPrivate(tm, "skipTutorialButton", skipBtn);

        // Spacebar → Next
        var space = root.AddComponent<SpaceToNext>();
        space.tutorial = tm;
        space.promptTMP = prompt;

        // เลือก & mark dirty
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TutorialUI RPG] Created.");
    }

    [MenuItem("Tools/Tutorial/Create Shop Tutorial Binder")]
    public static void CreateShopTutorialBinder()
    {
        var go = new GameObject("ShopTutorialBinder");
        go.transform.SetParent(null);
        Undo.RegisterCreatedObjectUndo(go, "Create ShopTutorialBinder");

        var binder = go.AddComponent<ShopTutorialBinder>();

        // try to auto-find common UI elements by name
        var coins = GameObject.Find("CoinsPanel");
        if (coins) binder.coinsPanel = coins.GetComponent<RectTransform>();

        var reroll = GameObject.Find("RerollButton");
        if (reroll) binder.rerollButton = reroll.GetComponent<Button>();

        var firstCard = GameObject.Find("FirstCard");
        if (firstCard) binder.firstCardRect = firstCard.GetComponent<RectTransform>();

        var firstBuy = GameObject.Find("FirstBuyButton");
        if (firstBuy) binder.firstBuyButton = firstBuy.GetComponent<Button>();

        var next = GameObject.Find("NextButton");
        if (next) binder.nextButton = next.GetComponent<Button>();

        var tm = Object.FindObjectOfType<TutorialManager>();
        if (tm) binder.tm = tm;

        Selection.activeGameObject = go;
        Debug.Log("[TutorialUIBuilder] Created ShopTutorialBinder. Verify references in Inspector.");
    }

    [MenuItem("Tools/Tutorial/Create Example Shop Tutorial Asset")]
    public static void CreateExampleShopTutorialAsset()
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
        Selection.activeObject = asset;
    }

    // ---------- helpers ----------
    static Image CreateImage(string name, Transform parent, bool stretch=false, Color? col=null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (stretch){ rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        else { rt.sizeDelta = new Vector2(400,150); }
        var img = go.GetComponent<Image>();
        img.color = col ?? Color.white;
        return img;
    }

    static TextMeshProUGUI CreateTMP(string name, Transform parent, string text, int size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = Color.white;
        tmp.raycastTarget = false; tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    static void SetPrivate(object obj, string field, object value)
    {
        var f = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(obj, value);
    }
}

// ลูกศรเด้ง ๆ
public class BounceY : MonoBehaviour
{
    public float amplitude = 6f, speed = 6f;
    Vector3 basePos;
    void OnEnable(){ basePos = ((RectTransform)transform).anchoredPosition3D; }
    void Update(){ var rt=(RectTransform)transform;
        rt.anchoredPosition3D = basePos + new Vector3(0, Mathf.Sin(Time.unscaledTime*speed)*amplitude, 0); }
}

// นิ้วชี้เด้ง ๆ
public class HandPulse : MonoBehaviour
{
    public float amplitude = 8f, speed = 3f;
    Vector3 basePos;
    void OnEnable(){ basePos = ((RectTransform)transform).anchoredPosition3D; }
    void Update(){ var rt=(RectTransform)transform;
        rt.anchoredPosition3D = basePos + new Vector3(0, Mathf.Sin(Time.unscaledTime*speed)*amplitude, 0); }
}
#endif
