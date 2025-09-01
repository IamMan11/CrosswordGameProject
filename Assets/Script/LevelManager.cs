using System.Collections;
using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Configs")]
    public LevelConfig[] levels;

    [Header("UI (ผูกใน Inspector)")]
    public TMP_Text levelText;
    public TMP_Text timerText;         // legacy: เคยใช้กับ Auto-Remove — ปิดไว้
    public TMP_Text levelTimerText;    // จับเวลารวมของด่าน (เวลาหมด = แพ้)

    public int CurrentLevel => currentLevel;

    // ----- Internal State -----
    private enum GamePhase { None, Setup, Ready, Running, Transition, GameOver }

    private GamePhase phase = GamePhase.None;
    private int currentLevel;
    private bool isGameOver = false;     // คงไว้เพื่อ compatibility กับโค้ดอื่น
    private bool isTransitioning = false;

    private float levelTimeLimit;
    private float levelTimeElapsed;
    private bool levelTimerRunning;

    // 🔒 Board-lock system (DISABLED)
    // private Coroutine boardLockCoroutine;

    // cache yield (ลด GC)
    private static readonly WaitForEndOfFrame WaitEOF = new WaitForEndOfFrame();

    // ------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SetPhase(GamePhase.None);
    }

    private void Start()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("No level configuration provided!");
            return;
        }
        SetupLevel(0);
    }

    private void OnDisable()
    {
        StopAllLoops();
    }

    public bool IsGameOver() => isGameOver;

    // ------------------------------
    private void Update()
    {
        if (phase != GamePhase.Running || levels == null || levels.Length == 0) return;

        var cfg = GetCurrentConfig();
        if (cfg == null) return;

        // 🕒 จับเวลาเลเวลหลัก
        if (levelTimerRunning && cfg.timeLimit > 0f)
        {
            levelTimeElapsed += Time.deltaTime;
            float remaining = Mathf.Max(0f, levelTimeLimit - levelTimeElapsed);
            UpdateLevelTimerText(remaining);

            if (remaining <= 0f)
            {
                StopLevelTimer();
                GameOver(false); // ❌ หมดเวลา
                return;
            }
        }

        // ✅ เช็กผ่านด่าน (รวม Triangle เฉพาะด่าน 2 ถ้าเปิดฟีเจอร์)
        if (CheckWinConditions(cfg))
        {
            AnnounceLevelComplete();
            _ = StartCoroutine(GoToNextLevel());
        }
    }

    /// <summary>
    /// ให้ TurnManager เรียกเมื่อคะแนน/จำนวนคำเปลี่ยน (ลดการพึ่งพา Update)
    /// </summary>
    public void OnScoreOrWordProgressChanged()
    {
        if (phase != GamePhase.Running) return;
        var cfg = GetCurrentConfig();
        if (cfg != null && CheckWinConditions(cfg))
        {
            AnnounceLevelComplete();
            _ = StartCoroutine(GoToNextLevel());
        }
    }

    private bool CheckWinConditions(LevelConfig cfg)
    {
        if (isGameOver) return false;
        if (TurnManager.Instance == null) return false;

        bool baseOK =
            TurnManager.Instance.Score >= cfg.requiredScore &&
            TurnManager.Instance.CheckedWordCount >= cfg.requiredWords;

        if (!baseOK) return false;

        // เงื่อนไข Triangle สำหรับด่าน 2 (เปิดได้ด้วย define)
        bool triangleOK = true;
#if LEVEL2_FEATURE
        triangleOK = (cfg.levelIndex != 2) ||
                     (Level2Controller.Instance && Level2Controller.Instance.IsTriangleComplete());
#endif
        return triangleOK;
    }

    // ------------------------------
    // Level flow
    // ------------------------------
    private void SetupLevel(int idx)
    {
        idx = Mathf.Clamp(idx, 0, levels.Length - 1);

        StopAllLoops();
        isGameOver = false;
        isTransitioning = false;
        SetPhase(GamePhase.Setup);

        currentLevel = idx;
        var cfg = levels[currentLevel];

        if (levelText) levelText.text = $"Level {cfg.levelIndex}";
        if (timerText) timerText.gameObject.SetActive(false); // ปิด UI legacy

        // ตั้งค่าเวลาเลเวล (เริ่มจริงเมื่อ OnFirstConfirm)
        levelTimeElapsed = 0f;
        levelTimeLimit = Mathf.Max(0f, cfg.timeLimit);
        levelTimerRunning = false;
        UpdateLevelTimerText(levelTimeLimit);

        // เตรียมบอร์ด/ระบบอื่น ๆ ตามโปรเจกต์ของคุณ
        if (BoardManager.Instance != null) BoardManager.Instance.GenerateBoard();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ResetForNewLevel();
            if (TileBag.Instance != null) TileBag.Instance.RefillTileBag();
            TurnManager.Instance.UpdateBagUI();
            if (BenchManager.Instance != null) BenchManager.Instance.RefillEmptySlots();
            TurnManager.Instance.UpdateBagUI();
        }

        Debug.Log($"▶ เริ่มด่าน {cfg.levelIndex} | เวลา: {cfg.timeLimit}s | Score target: {cfg.requiredScore}");

#if LEVEL2_FEATURE
        // ด่าน 2: เปิดระบบพิเศษ (Triangle/x2/BugLock/Bench Jam) ถ้ามีคอนโทรลเลอร์
        if (cfg.levelIndex == 2 && Level2Controller.Instance)
        {
            Level2Controller.Instance.OnLevel2Start();
        }
#endif

        // 🔒 Board-lock (disabled example)
        // if (currentLevel == levels.Length - 1 && BoardManager.Instance != null)
        //     boardLockCoroutine = StartCoroutine(BoardLockRoutine(30f));

        SetPhase(GamePhase.Ready);
    }

    private IEnumerator GoToNextLevel()
    {
        if (isTransitioning || phase == GamePhase.Transition || phase == GamePhase.GameOver) yield break;
        isTransitioning = true;
        SetPhase(GamePhase.Transition);

        StopAllLoops();             // หยุดตัวจับเวลา/คอร์รุตีนทั้งหมด
        yield return WaitEOF;       // รอ 1 เฟรมให้ HUD/คะแนนนิ่ง

        if (currentLevel + 1 < levels.Length)
        {
            SetupLevel(currentLevel + 1);
        }
        else
        {
            GameOver(true); // 🎉 จบด่านทั้งหมด
        }

        isTransitioning = false;
    }

    /// <summary>
    /// เรียกจาก input/UI ครั้งแรกเพื่อเริ่มด่าน (เริ่มจับเวลา)
    /// </summary>
    public void OnFirstConfirm()
    {
        if (phase != GamePhase.Ready)  // กันกดซ้ำหรือกดขณะไม่พร้อม
        {
            Debug.LogWarning($"OnFirstConfirm ignored. Phase={phase}");
            return;
        }

        if (levelTimeLimit > 0f)
            StartLevelTimer();

        SetPhase(GamePhase.Running);
        Debug.Log("Level started");
    }

    // ------------------------------
    // Timer control
    // ------------------------------
    private void StartLevelTimer()
    {
        if (levelTimerRunning) return;
        levelTimerRunning = true;
        levelTimeElapsed = 0f;
    }

    private void StopLevelTimer()
    {
        if (!levelTimerRunning) return;
        levelTimerRunning = false;
    }

    // ------------------------------
    // Level end
    // ------------------------------
    private void GameOver(bool win)
    {
        if (isGameOver || phase == GamePhase.GameOver) return;

        isGameOver = true;
        StopLevelTimer();
        StopAllLoops();
        SetPhase(GamePhase.GameOver);

        if (timerText) timerText.gameObject.SetActive(false);
        if (levelTimerText) levelTimerText.color = win ? Color.green : Color.red;

        Debug.Log(win ? "🎉 ชนะทุกด่าน" : "💀 แพ้เพราะหมดเวลา");

        // TODO: เปิด GameOverPanel
    }

    private void StopAllLoops()
    {
        // หยุดคอร์รุตีนต่าง ๆ ที่ LevelManager เป็นคนเริ่ม
        // if (boardLockCoroutine != null) { StopCoroutine(boardLockCoroutine); boardLockCoroutine = null; }
        // ถ้าคุณมีคอร์รุตีนอื่น ๆ เพิ่มมาในอนาคต ให้หยุดที่นี่ทั้งหมด
    }

    // ------------------------------
    // Helpers
    // ------------------------------
    private void UpdateLevelTimerText(float remaining)
    {
        if (!levelTimerText) return;
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        levelTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void AnnounceLevelComplete()
    {
        var cfg = GetCurrentConfig();
        Debug.Log($"✅ ผ่านด่าน {cfg.levelIndex}!");
    }

    private LevelConfig GetCurrentConfig()
    {
        if (levels == null || levels.Length == 0) return null;
        int idx = Mathf.Clamp(currentLevel, 0, levels.Length - 1);
        return levels[idx];
    }

    private void SetPhase(GamePhase next)
    {
        phase = next;
        // Debug.Log($"[LevelManager] Phase => {next}");
    }

#if UNITY_EDITOR
    // Utility: ตรวจ/จัดระเบียบคอนฟิกด่านจาก Inspector (คลิก ⋮ → Validate Levels)
    [ContextMenu("Validate Levels")]
    private void ValidateLevels()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogWarning("[LevelManager] levels is empty.");
            return;
        }

        var seen = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < levels.Length; i++)
        {
            var L = levels[i];
            if (L == null) { Debug.LogWarning($"Level {i} is null"); continue; }

            if (L.levelIndex < 1) L.levelIndex = 1;
            if (L.requiredScore < 0) L.requiredScore = 0;
            if (L.requiredWords < 0) L.requiredWords = 0;
            if (L.timeLimit < 0f) L.timeLimit = 0f;

            if (!seen.Add(L.levelIndex))
                Debug.LogWarning($"Duplicate levelIndex detected: {L.levelIndex}. Consider making them unique.");
        }

        var cfg = GetCurrentConfig();
        if (cfg != null) UpdateLevelTimerText(Mathf.Max(0f, cfg.timeLimit));

        Debug.Log("[LevelManager] ValidateLevels done.");
    }
#endif
}
