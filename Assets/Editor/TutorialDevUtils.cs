// Assets/Editor/TutorialTools.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TutorialTools
{
    // 1) เคลียร์คีย์เก่าแบบเดี่ยว
    [MenuItem("Tools/Tutorial/Clear Legacy Seen Flag (TUTORIAL_SEEN)", priority = 10)]
    public static void ClearLegacySeenFlag()
    {
        PlayerPrefs.DeleteKey("TUTORIAL_SEEN");
        PlayerPrefs.Save();
        Debug.Log("✅ Deleted PlayerPrefs key: TUTORIAL_SEEN");
    }

    // 2) เคลียร์คีย์ของคอนฟิกปัจจุบันในโปรเจกต์ (เลือก Asset อยู่ใน Project view)
    [MenuItem("Tools/Tutorial/Clear Seen Flag of Selected Config", priority = 11)]
    public static void ClearSelectedConfigSeenFlag()
    {
        var obj = Selection.activeObject as TutorialConfigSO;
        if (obj == null)
        {
            Debug.LogWarning("⚠ เลือก TutorialConfigSO ใน Project ก่อน (คลิกที่ asset แล้วค่อยกดเมนูนี้)");
            return;
        }

        string key = ComputeSeenKey(obj);
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log($"✅ Deleted PlayerPrefs key: {key} (for {obj.name})");
    }

    // 3) เคลียร์คีย์ของ "ทุก" TutorialConfigSO ทั้งโปรเจกต์
    [MenuItem("Tools/Tutorial/Clear All Tutorial Seen Flags", priority = 12)]
    public static void ClearAllTutorialSeenFlags()
    {
        string[] guids = AssetDatabase.FindAssets("t:TutorialConfigSO");
        int count = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var cfg  = AssetDatabase.LoadAssetAtPath<TutorialConfigSO>(path);
            if (cfg == null) continue;

            string key = ComputeSeenKey(cfg);
            PlayerPrefs.DeleteKey(key);
            count++;
            Debug.Log($"• Deleted: {key}  (config: {cfg.name})");
        }

        // เผื่อยังมีคีย์เก่า
        PlayerPrefs.DeleteKey("TUTORIAL_SEEN");

        PlayerPrefs.Save();
        Debug.Log($"✅ Cleared {count} tutorial seen key(s) + legacy key if existed.");
    }

    // 4) (ทางเลือก) เคลียร์คีย์ของคอนฟิกที่กำลังใช้งานในซีน (อาศัย TutorialManager ในซีน)
    [MenuItem("Tools/Tutorial/Clear Seen Flag of Current Scene Config", priority = 13)]
    public static void ClearCurrentSceneConfigSeenFlag()
    {
        var mgr = Object.FindObjectOfType<TutorialManager>();
        if (mgr == null || GetConfigViaReflection(mgr) == null)
        {
            Debug.LogWarning("⚠ หา TutorialManager หรือ config ในซีนไม่เจอ");
            return;
        }

        var cfg = GetConfigViaReflection(mgr);
        string key = ComputeSeenKey(cfg);
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log($"✅ Deleted PlayerPrefs key: {key} (current scene config: {cfg.name})");
    }

    // ---- helpers ----
    private static string ComputeSeenKey(TutorialConfigSO cfg)
    {
        // ตรงกับ CurrentSeenKey() ใน TutorialManager:
        if (cfg == null) return "TUTSEEN_NULL";
        return string.IsNullOrEmpty(cfg.seenKey) ? $"TUTSEEN_{cfg.name}" : cfg.seenKey;
    }

    private static TutorialConfigSO GetConfigViaReflection(TutorialManager mgr)
    {
        // ถ้า field เป็น private serialized: ใช้ SerializedObject หรือรีเฟล็คชัน
        var fi = typeof(TutorialManager).GetField("config",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        return fi?.GetValue(mgr) as TutorialConfigSO;
    }
}
#endif
