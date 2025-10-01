#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CreateTutorialUIPrefab
{
    [MenuItem("Tools/Crossword/Generate Tutorial UI Prefab")]
    public static void Generate()
    {
        // Root Canvas
        var root = new GameObject("TutorialUI");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        // Attach behaviour
        var tui = root.AddComponent<TutorialUI>();

        // === Overlay (พื้นมืด) ===
        var overlayGO = CreateUIObject("Overlay", root.transform);
        Stretch(overlayGO.GetComponent<RectTransform>());
        var overlayImg = overlayGO.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.8f);
        overlayImg.raycastTarget = true;
        var overlayCG = overlayGO.AddComponent<CanvasGroup>();
        overlayCG.alpha = 0f;
        overlayCG.blocksRaycasts = true;
        overlayCG.interactable = true;

        // === Focus Frame ===
        var focusGO = CreateUIObject("FocusFrame", root.transform);
        var focusRT = focusGO.GetComponent<RectTransform>();
        focusRT.sizeDelta = new Vector2(500, 220);
        var focusImg = focusGO.AddComponent<Image>();
        focusImg.color = new Color(1f, 0.95f, 0.4f, 0.25f);
        focusImg.raycastTarget = false;
        var outline = focusGO.AddComponent<Outline>();
        outline.effectDistance = new Vector2(2f, -2f);
        outline.effectColor = new Color(1f, 0.9f, 0.2f, 0.95f);

        // === Stack (Bubble → Card → Buttons) ===
        var stack = CreateUIObject("Stack", root.transform);
        var stackRT = stack.GetComponent<RectTransform>();
        stackRT.sizeDelta = new Vector2(1, 1);
        var v = stack.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(0, 0, 0, 0);
        v.spacing = 16;
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlHeight = false; v.childControlWidth = false;

        // --- Speech Bubble ---
        var bubble = CreateUIObject("Bubble", stack.transform);
        var bubbleRT = bubble.GetComponent<RectTransform>();
        bubbleRT.sizeDelta = new Vector2(520, 140);
        var bubbleBg = bubble.AddComponent<Image>();
        bubbleBg.color = new Color(1f, 1f, 1f, 0.97f);
        var bubbleShadow = bubble.AddComponent<Shadow>();
        bubbleShadow.effectDistance = new Vector2(0, -4);
        bubbleShadow.effectColor = new Color(0, 0, 0, 0.35f);
        var bubbleVL = bubble.AddComponent<VerticalLayoutGroup>();
        bubbleVL.padding = new RectOffset(20, 20, 14, 18);
        bubbleVL.childAlignment = TextAnchor.UpperLeft;
        bubbleVL.spacing = 6;

        var bubbleTitleGO = CreateUIObject("BubbleTitle", bubble.transform);
        var bubbleTitleTMP = bubbleTitleGO.AddComponent<TextMeshProUGUI>();
        bubbleTitleTMP.text = "TITLE";
        bubbleTitleTMP.enableAutoSizing = true; bubbleTitleTMP.fontSizeMin = 26; bubbleTitleTMP.fontSizeMax = 42;
        bubbleTitleTMP.alignment = TextAlignmentOptions.Left;

        var bubbleBodyGO = CreateUIObject("BubbleBody", bubble.transform);
        var bubbleBodyTMP = bubbleBodyGO.AddComponent<TextMeshProUGUI>();
        bubbleBodyTMP.text = "This is the body text...";
        bubbleBodyTMP.enableAutoSizing = true; bubbleBodyTMP.fontSizeMin = 20; bubbleBodyTMP.fontSizeMax = 32;
        bubbleBodyTMP.alignment = TextAlignmentOptions.Left;

        // tail
        var tail = CreateUIObject("BubbleTail", bubble.transform);
        var tailRT = tail.GetComponent<RectTransform>();
        tailRT.anchorMin = new Vector2(0.5f, 0f); tailRT.anchorMax = new Vector2(0.5f, 0f); tailRT.pivot = new Vector2(0.5f, 0.5f);
        tailRT.anchoredPosition = new Vector2(0, -10);
        tailRT.sizeDelta = new Vector2(22, 22);
        var tailImg = tail.AddComponent<Image>();
        tailImg.color = bubbleBg.color;
        tail.AddComponent<LayoutElement>().ignoreLayout = true;
        tail.transform.rotation = Quaternion.Euler(0, 0, 45f);

        // --- Card Group ---
        var cardGroup = CreateUIObject("CardGroup", stack.transform);
        var cardGroupRT = cardGroup.GetComponent<RectTransform>();
        cardGroupRT.sizeDelta = new Vector2(260, 330);

        // Card panel
        var card = CreateUIObject("Card", cardGroup.transform);
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(220, 300);
        var cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(1f, 1f, 1f, 0.98f);
        var cardShadow = card.AddComponent<Shadow>();
        cardShadow.effectDistance = new Vector2(0, -6);
        cardShadow.effectColor = new Color(0, 0, 0, 0.45f);

        // Joker vertical texts
        var leftText = CreateUIObject("Left", card.transform);
        var leftRT = leftText.GetComponent<RectTransform>();
        leftRT.anchorMin = new Vector2(0f, 0f); leftRT.anchorMax = new Vector2(0f, 1f); leftRT.pivot = new Vector2(0f, 0.5f); leftRT.anchoredPosition = new Vector2(10, 0); leftRT.sizeDelta = new Vector2(36, 0);
        var leftTMP = leftText.AddComponent<TextMeshProUGUI>();
        leftTMP.text = "J\nO\nK\nE\nR"; leftTMP.fontSize = 20; leftTMP.alignment = TextAlignmentOptions.Center;

        var rightText = CreateUIObject("Right", card.transform);
        var rightRT = rightText.GetComponent<RectTransform>();
        rightRT.anchorMin = new Vector2(1f, 0f); rightRT.anchorMax = new Vector2(1f, 1f); rightRT.pivot = new Vector2(1f, 0.5f); rightRT.anchoredPosition = new Vector2(-10, 0); rightRT.sizeDelta = new Vector2(36, 0);
        var rightTMP = rightText.AddComponent<TextMeshProUGUI>();
        rightTMP.text = "J\nO\nK\nE\nR"; rightTMP.fontSize = 20; rightTMP.alignment = TextAlignmentOptions.Center;

        // icon
        var iconGO = CreateUIObject("Icon", card.transform);
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(120, 120);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.raycastTarget = false;

        // --- Buttons Row (Back + Next) ---
        var row = CreateUIObject("ButtonsRow", stack.transform);
        var rowHL = row.AddComponent<HorizontalLayoutGroup>();
        rowHL.spacing = 14; rowHL.childAlignment = TextAnchor.MiddleCenter; rowHL.childControlHeight = true; rowHL.childForceExpandWidth = false;

        var backBtnGO = CreateGhostButton("Back", "Back", row.transform);
        var nextBtnGO = CreateBigButton("Next", "Next", row.transform);

        // --- Skip Button (ขวาล่าง) ---
        var skipBtnGO = CreateBigPill("Skip", "Skip  ›", root.transform);
        var skipRT = skipBtnGO.GetComponent<RectTransform>();
        skipRT.anchorMin = new Vector2(1f, 0.5f); skipRT.anchorMax = new Vector2(1f, 0.5f); skipRT.pivot = new Vector2(1f, 0.5f);
        skipRT.anchoredPosition = new Vector2(-48, 0); skipRT.sizeDelta = new Vector2(180, 72);

        // --- Page Text (x/y) ---
        var pageGO = CreateUIObject("PageText", root.transform);
        var pageRT = pageGO.GetComponent<RectTransform>();
        pageRT.anchorMin = new Vector2(1, 1); pageRT.anchorMax = new Vector2(1, 1); pageRT.pivot = new Vector2(1, 1);
        pageRT.anchoredPosition = new Vector2(-24, -18);
        var pageTMP = pageGO.AddComponent<TextMeshProUGUI>();
        pageTMP.text = "1/5"; pageTMP.fontSize = 20; pageTMP.alignment = TextAlignmentOptions.TopRight;
        pageGO.AddComponent<LayoutElement>().ignoreLayout = true;

        // Sibling order
        overlayGO.transform.SetSiblingIndex(0);
        focusGO.transform.SetSiblingIndex(1);
        stack.transform.SetSiblingIndex(2);
        skipBtnGO.transform.SetSiblingIndex(3);
        pageGO.transform.SetSiblingIndex(4);

        // === Wire TutorialUI ===
        SetPrivate(tui, "mainCanvas", canvas);
        SetPrivate(tui, "overlay", overlayCG);
        SetPrivate(tui, "dimImage", overlayImg);
        SetPrivate(tui, "cardRoot", cardRT);
        SetPrivate(tui, "cardBg", cardBg);
        SetPrivate(tui, "iconImage", iconImg);
        SetPrivate(tui, "titleText", bubbleTitleTMP);
        SetPrivate(tui, "bodyText", bubbleBodyTMP);
        SetPrivate(tui, "pageText", pageTMP);
        SetPrivate(tui, "focusFrame", focusRT);
        SetPrivate(tui, "focusImage", focusImg);
        SetPrivate(tui, "nextBtn", nextBtnGO.GetComponent<Button>());
        SetPrivate(tui, "backBtn", backBtnGO.GetComponent<Button>());
        SetPrivate(tui, "skipBtn", skipBtnGO.GetComponent<Button>());

        // === Assign Thai TMP Font if available ===
        var thaiFont = FindThaiTMPFontAsset();
        if (thaiFont != null)
        {
            bubbleTitleTMP.font = thaiFont;
            bubbleBodyTMP.font = thaiFont;
            pageTMP.font = thaiFont;
            AssignButtonLabelFont(backBtnGO, thaiFont);
            AssignButtonLabelFont(nextBtnGO, thaiFont);
            AssignButtonLabelFont(skipBtnGO, thaiFont);
            leftTMP.font = thaiFont; rightTMP.font = thaiFont;
        }
        else
        {
            Debug.LogWarning("[TutorialUI] ไม่พบ TMP Font ที่รองรับภาษาไทย กรุณานำเข้าฟอนต์ไทย (เช่น Noto Sans Thai / Sarabun) แล้วสร้าง Font Asset ของ TextMeshPro");
        }

        // === Save Prefab ===
        var folder = "Assets/Prefabs";
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/TutorialUI.prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Create TutorialUI");
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Tutorial UI prefab created at: {path}");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateGhostButton(string internalName, string labelText, Transform parent)
    {
        var go = CreateUIObject(internalName, parent);
        var img = go.AddComponent<Image>();
        img.color = new Color(1f,1f,1f,0.0f);
        var btn = go.AddComponent<Button>();
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 120; le.minHeight = 56;

        var label = CreateUIObject("Label", go.transform);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText; tmp.alignment = TextAlignmentOptions.Center; tmp.enableAutoSizing = true; tmp.fontSizeMin = 20; tmp.fontSizeMax = 36;
        var labelRT = label.GetComponent<RectTransform>();
        Stretch(labelRT);
        var colors = btn.colors; colors.normalColor = img.color; colors.highlightedColor = new Color(1f,1f,1f,0.08f); colors.pressedColor = new Color(1f,1f,1f,0.16f); colors.selectedColor = colors.normalColor; colors.disabledColor = new Color(1f,1f,1f,0.04f); btn.colors = colors;
        return go;
    }

    private static GameObject CreateBigButton(string internalName, string labelText, Transform parent)
    {
        var go = CreateUIObject(internalName, parent);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.97f, 0.35f, 0.28f, 1f); // แดงส้มแบบ Balatro
        var btn = go.AddComponent<Button>();
        var sh = go.AddComponent<Shadow>(); sh.effectDistance = new Vector2(0, -6); sh.effectColor = new Color(0,0,0,0.5f);
        var le = go.AddComponent<LayoutElement>(); le.minWidth = 220; le.minHeight = 96;

        var label = CreateUIObject("Label", go.transform);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText; tmp.alignment = TextAlignmentOptions.Center; tmp.enableAutoSizing = true; tmp.fontSizeMin = 26; tmp.fontSizeMax = 44;
        var labelRT = label.GetComponent<RectTransform>(); Stretch(labelRT);

        var colors = btn.colors; colors.normalColor = img.color; colors.highlightedColor = new Color(0.98f,0.45f,0.38f,1f); colors.pressedColor = new Color(0.85f,0.25f,0.20f,1f); colors.selectedColor = colors.normalColor; colors.disabledColor = new Color(0.6f,0.6f,0.6f,0.6f); btn.colors = colors;
        return go;
    }

    private static GameObject CreateBigPill(string internalName, string labelText, Transform parent)
    {
        var go = CreateUIObject(internalName, parent);
        var img = go.AddComponent<Image>(); img.color = new Color(0.82f, 0.88f, 1f, 1f);
        var btn = go.AddComponent<Button>(); var sh = go.AddComponent<Shadow>(); sh.effectDistance = new Vector2(0,-4); sh.effectColor = new Color(0,0,0,0.35f);
        var le = go.AddComponent<LayoutElement>(); le.minWidth = 160; le.minHeight = 64;
        var label = CreateUIObject("Label", go.transform); var tmp = label.AddComponent<TextMeshProUGUI>(); tmp.text = labelText; tmp.alignment = TextAlignmentOptions.Center; tmp.enableAutoSizing = true; tmp.fontSizeMin = 22; tmp.fontSizeMax = 36; var labelRT = label.GetComponent<RectTransform>(); Stretch(labelRT);
        var colors = btn.colors; colors.normalColor = img.color; colors.highlightedColor = new Color(0.88f,0.92f,1f,1f); colors.pressedColor = new Color(0.72f,0.78f,0.95f,1f); colors.selectedColor = colors.normalColor; colors.disabledColor = new Color(0.7f,0.7f,0.7f,0.6f); btn.colors = colors;
        return go;
    }

    private static void SetPrivate(Object target, string fieldName, object value)
    {
        var f = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(target, value);
    }

    private static void AssignButtonLabelFont(GameObject buttonGO, TMP_FontAsset font)
    {
        var label = buttonGO.transform.Find("Label"); if (label == null) return; var tmp = label.GetComponent<TextMeshProUGUI>(); if (tmp != null) tmp.font = font;
    }

    private static TMP_FontAsset FindThaiTMPFontAsset()
    {
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa == null) continue;
            var n = fa.name.ToLowerInvariant();
            if (n.Contains("thai") || n.Contains("sarabun") || n.Contains("kanit") || n.Contains("prompt") || n.Contains("noto")) return fa;
        }
        return null;
    }

    [MenuItem("Tools/Crossword/Remove Missing Scripts In Scene")] 
    private static void RemoveMissingScriptsInScene()
    {
        var all = GameObject.FindObjectsOfType<GameObject>(true);
        int removed = 0;
        foreach (var go in all) removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        Debug.Log($"Removed {removed} missing script components from scene.");
    }
}
#endif