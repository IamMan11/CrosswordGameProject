using UnityEngine;

[System.Serializable]
public class LevelConfig
{
    [Header("Display / Index")]
    [Tooltip("Show number for this level (1-based).")]
    public int levelIndex = 1;

    [Header("Win Conditions")]
    [Min(0)] public int requiredScore = 100;
    [Min(0)] public int requiredWords = 5;

    [Header("Level Timer (0 = no limit)")]
    [Tooltip("Seconds allowed for this level. Use 0 for no time limit.")]
    [Min(0f)] public float timeLimit = 120f;

    // ===== Themed-word requirement (optional) =====
    [Header("Themed Word Requirement (optional)")]
    [Tooltip("Turn on if this level also requires themed words (e.g. IT words).")]
    public bool requireThemedWords = false;

    [Tooltip("If your dictionary supports tags, use this tag (e.g. \"IT\"). (ยังไม่ใช้ในโค้ดนี้ แต่เผื่ออนาคต)")]
    public string requiredThemeTag = "IT";

    [Min(0)]
    [Tooltip("Minimum number of themed words required (e.g. 5).")]
    public int requiredThemeCount = 0;

    [Tooltip("Fallback whitelist if your dictionary has no tags (case-insensitive). " +
             "If empty, LevelManager จะใช้ default คีย์เวิร์ดภายใน")]
    public string[] manualThemeWords;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (levelIndex < 1) levelIndex = 1;
        if (requiredScore < 0) requiredScore = 0;
        if (requiredWords < 0) requiredWords = 0;
        if (timeLimit < 0f) timeLimit = 0f;

        if (requiredThemeCount < 0) requiredThemeCount = 0;
        // ไม่บังคับรีเซ็ตค่า เมื่อ requireThemedWords=false เพื่อให้จัด preset ไว้ล่วงหน้าได้
    }
#endif
}
