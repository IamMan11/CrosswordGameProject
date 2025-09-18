#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class TutorialLayoutTools
{
    static RectTransform FindRT(Transform root, string path) =>
        root.Find(path)?.GetComponent<RectTransform>();

    [MenuItem("Tools/Tutorial/Layout/Place DialogPanel ▸ Bottom (RPG)")]
    public static void PlaceBottom() => ApplyLayout(LayoutPreset.Bottom);

    [MenuItem("Tools/Tutorial/Layout/Place DialogPanel ▸ Top")]
    public static void PlaceTop() => ApplyLayout(LayoutPreset.Top);

    [MenuItem("Tools/Tutorial/Layout/Place DialogPanel ▸ Center")]
    public static void PlaceCenter() => ApplyLayout(LayoutPreset.Center);

    enum LayoutPreset { Bottom, Top, Center }

    static void ApplyLayout(LayoutPreset preset)
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tm == null) { Debug.LogWarning("TutorialManager not found"); return; }

        var root = tm.transform;
        var panel = FindRT(root, "DialogPanel");
        if (panel == null) { Debug.LogWarning("DialogPanel not found under TutorialUI"); return; }

        Undo.RegisterFullObjectHierarchyUndo(panel, "Place DialogPanel");

        Vector2 aMin, aMax;
        switch (preset)
        {
            case LayoutPreset.Top:    aMin = new(0.05f, 0.72f); aMax = new(0.95f, 0.96f); break;
            case LayoutPreset.Center: aMin = new(0.10f, 0.35f); aMax = new(0.90f, 0.65f); break;
            default:                  aMin = new(0.05f, 0.02f); aMax = new(0.95f, 0.26f); break;
        }

        panel.anchorMin = aMin;
        panel.anchorMax = aMax;
        panel.pivot = new(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = Vector2.zero;

        // Portrait
        var portrait = FindRT(panel, "Portrait");
        if (portrait != null)
        {
            portrait.anchorMin = new(0, 0);
            portrait.anchorMax = new(0, 1);
            portrait.pivot = new(0, 0.5f);
            portrait.anchoredPosition = new(20, 0);
            portrait.sizeDelta = new(220, 0);
        }

        // NameTag + Label
        var nameTag = FindRT(panel, "NameTag");
        if (nameTag != null)
        {
            nameTag.anchorMin = new(0, 1);
            nameTag.anchorMax = new(0, 1);
            nameTag.pivot = new(0, 1);
            nameTag.anchoredPosition = new(20, 24);
            nameTag.sizeDelta = new(360, 56);
        }
        var nameLabel = panel.Find("NameTag/Label")?.GetComponent<TextMeshProUGUI>();
        if (nameLabel != null)
        {
            // ✅ API ใหม่
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
        }

        // BodyText
        var bodyRT = FindRT(panel, "BodyText");
        var bodyTMP = panel.Find("BodyText")?.GetComponent<TextMeshProUGUI>();
        if (bodyRT != null)
        {
            bodyRT.anchorMin = new(0, 0);
            bodyRT.anchorMax = new(1, 1);
            bodyRT.offsetMin = new(260, 24);
            bodyRT.offsetMax = new(-24, -64);
        }
        if (bodyTMP != null)
        {
            // ✅ API ใหม่
            bodyTMP.textWrappingMode = TextWrappingModes.Normal;
            bodyTMP.alignment = TextAlignmentOptions.TopLeft;
            bodyTMP.overflowMode = TextOverflowModes.Ellipsis;
            bodyTMP.enableAutoSizing = false;
        }

        // Prompt
        var prompt = FindRT(panel, "Prompt");
        if (prompt != null)
        {
            prompt.anchorMin = new(0, 0);
            prompt.anchorMax = new(0, 0);
            prompt.pivot = new(0, 0);
            prompt.anchoredPosition = new(260, 16);
        }

        // Next Button
        var nextBtn = FindRT(panel, "NextButton");
        if (nextBtn != null)
        {
            nextBtn.anchorMin = new(1, 0);
            nextBtn.anchorMax = new(1, 0);
            nextBtn.pivot = new(1, 0);
            nextBtn.anchoredPosition = new(-24, 18);
            nextBtn.sizeDelta = new(180, 56);
        }

        tm.transform.SetAsLastSibling(); // ให้อยู่บนสุดของ Canvas
        EditorUtility.SetDirty(panel);
        Debug.Log($"[TutorialLayout] Applied {preset} preset to DialogPanel.");
    }
}
#endif
