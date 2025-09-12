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

    // ===== Level 1 – Garbled IT obstacle (ควบคุมโดย Level1GarbledIT เท่านั้น) =====
    [Header("Level 1 - Garbled IT Obstacle")]
    public bool  level1_enableGarbledIT = true;
    [Min(1)] public int level1_garbledCount        = 3;    // จำนวนคำ IT ที่จะสุ่มวาง
    [Min(1)] public int level1_minGapBetweenSets   = 2;    // ระยะห่างขั้นต่ำระหว่างชุด
    [Min(1)] public int level1_placeMaxRetries     = 200;  // ความพยายามในการวาง
    public string[] level1_itWords = new string[] {
        "code","dev","server","client","api","cloud","sql","python","java","unity","debug","kernel","socket","router"
    };
    public Color level1_garbledSlotBg       = Color.black;   // BG ช่องในชุด
    public Color level1_outlineDefaultColor = Color.black;   // กรอบปกติ
    public Color level1_outlineTouchedColor = Color.yellow;  // กรอบเมื่อเคยแตะ

    // ===== Themed-word requirement (optional) =====
    [Header("Themed Word Requirement (optional)")]
    public bool   requireThemedWords = false;
    public string requiredThemeTag   = "IT";
    [Min(0)] public int requiredThemeCount = 0;
    [Tooltip("Fallback whitelist ถ้า dictionary ไม่มี tag (ไม่สนตัวพิมพ์)")]
    public string[] manualThemeWords;

    // ===== Economy / Score formula =====
    [Header("Economy (ต่อด่าน)")]
    public int   baseCoins      = 50;
    public int   targetTimeSec  = 900;

    [Header("Score Formula (ต่อด่าน)")]
    public int   maxWordsForScore   = 10;
    public float coefTimeScore      = 0.2f;
    public int   coefWordScore      = 50;
    public int   coefTurnScore      = -10;
    public int   coefTilesLeftScore = 2;

    [Header("Money Formula (ต่อด่าน)")]
    public int   maxWordsForMoney   = 10;
    public float coefTimeMoney      = 0.05f;
    public int   coefWordMoney      = 2;
    public int   coefTurnMoney      = -1;
    public int   coefTilesLeftMoney = 1;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (levelIndex < 1) levelIndex = 1;
        if (requiredScore < 0) requiredScore = 0;
        if (requiredWords < 0) requiredWords = 0;
        if (timeLimit < 0f) timeLimit = 0f;
        if (requiredThemeCount < 0) requiredThemeCount = 0;
    }
#endif
}
