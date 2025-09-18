#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class TutorialSceneFixer
{
    [MenuItem("Tools/Tutorial/Fix Duplicates & Wire UI")]
    public static void Fix()
    {
        // หา TutorialManager ทั้งหมด
        var all = Object.FindObjectsByType<TutorialManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length == 0) { Debug.LogWarning("No TutorialManager found."); return; }

        // เลือกตัวที่จะเก็บ: ตัวที่อยู่บน GameObject ชื่อ "TutorialUI" ก่อน
        TutorialManager keep = null;
        foreach (var tm in all) if (tm.name == "TutorialUI") { keep = tm; break; }
        if (keep == null) keep = all[0];

        // ลบทิ้งตัวอื่น
        foreach (var tm in all) if (tm != keep)
        {
            Debug.Log($"[Fix] Remove duplicate TutorialManager on {tm.gameObject.name}");
            Object.DestroyImmediate(tm);
        }

        // พยายามผูกฟิลด์ให้ครบจากลูกๆ ตามชื่อที่ Builder สร้าง
        var root = keep.transform;
        var overlay = root.Find("Overlay")?.GetComponent<CanvasGroup>();
        var body    = root.Find("DialogPanel/BodyText")?.GetComponent<TextMeshProUGUI>();
        var frameRT = root.Find("HighlightFrame")?.GetComponent<RectTransform>();
        var handRT  = root.Find("HandPointer")?.GetComponent<RectTransform>();
        var contBtn = root.Find("Overlay/ContinueAnywhereBtn")?.GetComponent<Button>();
        var panel   = root.Find("DialogPanel")?.gameObject;
        var portrait= root.Find("DialogPanel/Portrait")?.GetComponent<Image>();
        var nameTMP = root.Find("DialogPanel/NameTag/Label")?.GetComponent<TextMeshProUGUI>();
        var audio   = root.Find("DialogPanel")?.GetComponent<AudioSource>();
        var nextBtn = root.Find("DialogPanel/NextButton")?.GetComponent<Button>();

        Set(keep, "overlay", overlay);
        Set(keep, "bodyText", body);
        Set(keep, "highlightFrame", frameRT);
        Set(keep, "handPointer", handRT);
        Set(keep, "continueAnywhereBtn", contBtn);
        Set(keep, "characterPanel", panel);
        Set(keep, "portraitImage", portrait);
        Set(keep, "nameText", nameTMP);
        Set(keep, "voiceSource", audio);
        Set(keep, "nextButton", nextBtn);

        // เอา TutorialUI ไปไว้ท้ายสุด (บนสุดของจอ)
        keep.transform.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Fix] Done. Keep TutorialManager on: " + keep.gameObject.name);
    }

    static void Set(object obj, string field, object value)
    {
        if (value == null) return;
        var f = obj.GetType().GetField(field,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(obj, value);
    }
}
#endif
