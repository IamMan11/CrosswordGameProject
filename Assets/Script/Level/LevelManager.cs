// ========== LevelManager.cs (wired with UIManager) ==========
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    [Header("Level 1 – Word Request")]
    public int wordRequestTargetLevel1 = 0;   // ตั้ง > 0 ถ้ามีเป้าหมาย
    private int wordRequestsDone = 0;
    [Header("Configs")]
    public LevelConfig[] levels;
    [HideInInspector] public LevelConfig currentLevelConfig;
    [Header("UI")]
    public TMP_Text levelText;
    public TMP_Text levelTimerText;
    [Tooltip("Progress คำ IT ของด่าน 1 (ไม่ผูกก็ได้)")]
    public TMP_Text itProgressText;
    [Header("Stage Clear UI")]
    public StageClearPanel stageClearPanel;
    public string shopSceneName = "Shop";
    // ===== Internal state =====
    private enum GamePhase { None, Setup, Ready, Running, Transition, GameOver }
    private GamePhase phase = GamePhase.None;
    private int currentLevel;
    private bool isGameOver;
    private float levelTimeLimit;
    private float levelTimeElapsed;
    private bool levelTimerRunning;
    private bool timerStarted, timerPaused;
    [SerializeField] float panicTimeThresholdSec = 180f; // 3 นาที
    bool prepareFailActive = false;       // โหมดเตรียมแพ้ (ถุง = 0)
    bool pendingPrepareFailCheck = false; // จะเช็กแพ้หลังคอนเฟิร์มหนึ่งครั้ง
    bool panicBgmActive = false;          // โหมด BGM เวลาใกล้หมด
    // ===== Level 1 – IT words objective (progress) =====
    [Header("Level 1 – IT Words")]
    public int itWordsTargetLevel1 = 5;
    public string[] itKeywordsLevel1 = new string[] {
        "it","code","bug","dev","server","client","api","database","db","sql",
        "data","cloud","ai","ml","python","java","c#","csharp","unity","scene",
        "asset","compile","build","network","socket","array","stack","cache","login","token"
    };
    private readonly HashSet<string> itWordsFound = new HashSet<string>();
    // ===== Level 2 systems (คงเดิม) =====
    [Header("Level 2 – Triangle Objective")]
    public Vector2Int[] level2_triangleTargets = new Vector2Int[] {
        new Vector2Int(2,2),
        new Vector2Int(2,12),
        new Vector2Int(12,7)
    };
    private bool level2_triangleComplete;
    private float level2_triangleCheckTimer;
    private int level2_triangleLinkedCount = 0; // 0..3 ที่ "เชื่อมถึงกัน" ณ ตอนนี้
    [Header("Level 2 – Periodic X2 Zones (3x3)")]
    private Coroutine level2_x2Routine;
    private readonly List<(Vector2Int pos, SlotType prevType, int prevMana)> level2_activeZoneChanges
        = new List<(Vector2Int, SlotType, int)>();
    [Header("Level 2 – Locked Board (ปลดด้วยความยาวคำหลัก)")]
    public bool level2_enableLockedBoard = true;
    public int level2_lockedCount = 7;
    public Vector2Int level2_requiredLenRange = new Vector2Int(3, 7);
    private readonly Dictionary<BoardSlot, int> level2_lockedSlots = new Dictionary<BoardSlot, int>();
    [Header("Level 2 – Bench Issue")]
    public bool level2_enableBenchIssue = true;
    public float level2_benchIssueIntervalSec = 60f;
    public float level2_benchIssueDurationSec = 20f;
    public int level2_benchZeroPerMove = 2;
    public int level2_benchPenaltyPerMove = 0;
    private bool level2_benchIssueActive;
    private float level2_benchIssueEndTime;
    private Coroutine level2_benchIssueRoutine;
    private string level2_lastPenalizedWord = "";
    [Header("Level 2 – Theme & Rewards")]
    public bool level2_applyThemeOnStart = true;
    public bool level2_grantWinRewards = true;
    public int level2_winCogCoin = 1;
    public string level2_nextFloorClue = "เลขชั้นถัดไป";
    [Header("Level 2 – Triangle Objective")]
    public bool level2_useTriangleObjective = true;
    [Min(1)] public int level2_triangleNodeSize = 1;         // ขนาดโหนด (เช่น 2 = 2×2)
    [Min(2)] public int level2_triangleMinManhattanGap = 6;  // ระยะห่างระหว่างโหนด
    public float level2_triangleCheckPeriod = 0.5f;
    public Color level2_triangleIdleColor = new Color32(40, 40, 40, 200);
    public Color level2_triangleLinkedColor = new Color32(30, 180, 60, 200);
    [Header("Level 2 – Periodic X2 Zones (3×3)")]
    public bool level2_enablePeriodicX2Zones = true;
    public float level2_x2IntervalSec = 180f;
    public int level2_x2ZonesPerWave = 2;
    [Header("Level 2 – Zone spacing")]
    [Min(3)] public int level2_zoneMinCenterCheby = 4; // 3=ไม่แตะกัน, 4+=ห่างขึ้น
    public float level2_x2ZoneDurationSec = 30f;
    public SlotType level2_multiplierSlotType = SlotType.DoubleWord;
    public Color level2_zoneOverlayColor = new Color(0.2f, 0.9f, 0.2f, 0.28f);
    [Header("Level 2 – Bench Issue (Per-Confirm)")]
    [Min(0)] public int level2_benchIssueCount = 2; // x ตัวที่จะสุ่ม
    public Color level2_benchIssueOverlayColor = new Color(0f, 0f, 0f, 0.55f);
    private Coroutine level2_benchIssueAfterRefillCo;
    [Header("Level 2 – Locked Segments")]
    public bool level2_enableLockedSegments = true;
    [Min(1)] public int level2_lockedSegmentLength = 4;
    [Min(1)] public int level2_lockedSegmentCount = 3;
    public Color level2_lockedOverlayColor = new Color(0f, 0f, 0f, 0.55f);
    private readonly List<BoardSlot> level2_lockedSegmentSlots = new();
    // =============== Level 3 – The Black Hydra (Boss) ===============
    [Header("Level 3 – Black Hydra (Boss)")]
    public bool  level3_enableBoss = true;
    [Tooltip("HP สูงสุดของบอส")]
    public int   level3_bossMaxHP = 1200;
    [Tooltip("ความยาวคำที่นับ Critical")]
    public int   level3_criticalLength = 6;
    [Tooltip("โบนัสคริติคอล (+50% = 0.5)")]
    [Range(0f, 2f)] public float level3_criticalBonus = 0.5f;
    [Header("L3 – Conveyor Shuffle")]
    public float level3_conveyorIntervalSec = 120f;
    [Tooltip("จำนวนตำแหน่งที่จะเลื่อนแบบ conveyor (อย่างน้อย 1)")]
    public int   level3_conveyorShift = 1;
    [Header("L3 – Lock Board Wave")]
    public float level3_lockWaveIntervalSec = 90f;
    public int   level3_lockCountPerWave = 6;
    public float level3_lockDurationSec = 25f;
    [Header("L3 – Field Effects")]
    public float level3_fieldEffectIntervalSec = 75f;
    public float level3_fieldEffectDurationSec = 30f;
    [Tooltip("ขนาดโซน (4x4 ตามสเป็ค)")]
    public int   level3_zoneSize = 4; // 4x4
    [Tooltip("สปอว์นพร้อมกันรอบละกี่โซนต่อประเภท")]
    public int   level3_zonesPerType = 1;
    [Header("L3 – Random Deletions")]
    public float level3_deleteActionIntervalSec = 50f;
    public int   level3_deleteBoardCount = 2;
    public int   level3_deleteBenchCount = 2;
    public float level3_deleteLettersCooldownSec = 20f;
    public float level3_deleteCardsCooldownSec   = 35f;
    public float level3_cardSlotLockDurationSec  = 20f;
    [Header("L3 – Phase Change")]
    [Tooltip("บอสหายตัวเมื่อ HP ≤ 50%")]
    [Range(0f,1f)] public float level3_phaseChangeHPPercent = 0.5f;
    [Tooltip("ช่วง vanish จะ +เวลา 7:30")]
    public float level3_phaseTimeBonusSec = 450f; // 7m30s
    [Tooltip("เมื่อ HP ≤ 25% บีบเวลาเหลือ 3 นาทีทันที")]
    [Range(0f,1f)] public float level3_sprintHPPercent = 0.25f;
    public float level3_sprintRemainingSec = 180f;
    [Header("L3 – UI (optional)")]
    public TMP_Text bossHpText; // ผูกในอินสเปกเตอร์ถ้ามี
    // --- internals ---
    private int   level3_bossHP;
    private bool  level3_phaseChangeActive = false;
    private bool  level3_phaseTriggered = false;
    private bool  level3_sprintTriggered = false;
    private Coroutine level3_conveyorRoutine;
    private Coroutine level3_lockRoutine;
    private Coroutine level3_fieldRoutine;
    private Coroutine level3_deleteRoutine;
    private readonly List<BoardSlot> level3_lockedByBoss = new List<BoardSlot>();
    private struct L3Zone { public RectInt rect; public bool isBuff; public float end; }
    private readonly List<L3Zone> level3_activeZones = new List<L3Zone>();
    // puzzle ระหว่าง vanish (1/3 → 2/3 → 3/3)
    private int        level3_puzzleStage = 0; // 0=off, 1..3 active
    private Vector2Int level3_puzzleA, level3_puzzleB;
    public  float      level3_puzzleCheckPeriod = 0.5f;
    private float      level3_puzzleCheckTimer  = 0f;
    // คูลดาวน์สำหรับสุ่มลบ
    private float level3_nextLetterDeleteTime = 0f;
    private float level3_nextCardDeleteTime   = 0f;
    // === Events (เผื่อระบบภายนอกฟัง) ===
    public event Action<int> OnBossDeleteBenchRequest;
    public event Action      OnBossDeleteRandomCardRequest;
    public event Action<float> OnBossLockCardSlotRequest;
    // =====================================================
    // ----------------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        phase = GamePhase.None;
    }
    private void Start()
    {
        if (levels == null || levels.Length == 0) { Debug.LogError("No level configuration provided!"); return; }
        int startIndex = 0;
        if (StageResultBus.HasPendingNextLevel)
        {
            startIndex = Mathf.Clamp(StageResultBus.NextLevelIndex, 0, levels.Length - 1);
            StageResultBus.ClearNextLevelFlag();
        }
        SetupLevel(startIndex);
    }
    public void IncrementWordRequest(int delta = 1)
    {
        wordRequestsDone = Mathf.Max(0, wordRequestsDone + delta);
        LevelTaskUI.I?.Refresh();
        OnScoreOrWordProgressChanged();
    }
    private bool IsWordMatchingTheme(string w)
    {
        var cfg = currentLevelConfig;
        if (cfg == null || string.IsNullOrEmpty(w)) return false;
        string n = Normalize(w);
        // 1) ถ้าใช้แท็ก "IT" ให้รีไซเคิลเช็กเดิม
        if (!string.IsNullOrEmpty(cfg.requiredThemeTag) &&
            cfg.requiredThemeTag.Trim().ToLowerInvariant() == "it")
            return IsITWord(n);
        // 2) manual whitelist
        if (cfg.manualThemeWords != null && cfg.manualThemeWords.Length > 0)
        {
            for (int i = 0; i < cfg.manualThemeWords.Length; i++)
            {
                var mw = cfg.manualThemeWords[i];
                if (string.IsNullOrWhiteSpace(mw)) continue;
                if (n == Normalize(mw)) return true;
            }
        }
        return false;
    }
    public (int done, int target) GetWordRequestProgress()
    {
        var cfg = currentLevelConfig;
        int target = (cfg != null && cfg.requireThemedWords) ? Mathf.Max(0, cfg.requiredThemeCount) : 0;
        return (Mathf.Max(0, wordRequestsDone), target);
    }
    public void EnterPrepareFailMode()
    {
        if (prepareFailActive) return;
        prepareFailActive = true;
        UIManager.Instance?.ShowFloatingToast("TileBag = 0 • เทิร์นถัดไปถ้ายังไม่ถึงเป้าจะแพ้", Color.yellow, 2f);
    }
    public void CancelPrepareFailMode()
    {
        if (!prepareFailActive) return;
        prepareFailActive = false;
        pendingPrepareFailCheck = false; // เคลียร์เงื่อนไขรอเช็ก
        UIManager.Instance?.ShowFloatingToast("ได้ตัวอักษรเพิ่ม — ยกเลิกโหมดเตรียมแพ้", Color.cyan, 1.4f);
    }
    // เรียกตอนกด Confirm (ถ้า ณ ขณะนั้นอยู่โหมดเตรียมแพ้ → จะเช็กหลังจบอนิเมชัน)
    public void MarkPrepareFailCheckIfActive()
    {
        if (prepareFailActive) pendingPrepareFailCheck = true;
    }
    // เรียกตอนคอร์รุตีนคิดคะแนนจบ
    public void TryFailAfterConfirm()
    {
        if (!pendingPrepareFailCheck) return;
        pendingPrepareFailCheck = false;
        if (!HasMetWinConditions())
            TriggerStageFail("Tiles ran out");    // สรุปแพ้
    }
    public bool IsPrepareFailActive() => prepareFailActive;
    // ให้ TurnManager ใช้ถาม “ถึงเงื่อนไขชนะแล้วหรือยัง?”
    public bool HasMetWinConditions() => CheckWinConditions(currentLevelConfig);
    // เวลาใกล้หมด: เปิด/ปิดโหมด BGM Panic
    void SetPanicBgm(bool on)
    {
        if (panicBgmActive == on) return;
        panicBgmActive = on;
        BgmPlayer.I?.SetPanicMode(on);  // จะบล็อกสลับ tier จาก streak ขณะ on
    }
    public bool IsWordRequestObjectiveActive()
    {
        var cfg = currentLevelConfig;
        return cfg != null && cfg.levelIndex == 1 && cfg.requireThemedWords && cfg.requiredThemeCount > 0;
    }
    // ใช้โดย UI Task
    public int GetITWordsFoundCount() => itWordsFound.Count;
    public int GetITWordsTargetLevel1() => itWordsTargetLevel1;
    private void OnDisable() => StopAllLoops();
    public bool IsGameOver() => isGameOver;
    public int GetCurrentLevelIndex() => currentLevelConfig != null ? currentLevelConfig.levelIndex : 0;
    private void UpdateLevelTimerText(float seconds)
    {
        var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int mm = total / 60, ss = total % 60;
        if (levelTimerText) levelTimerText.text = $"{mm:00}:{ss:00}";
    }
    private void Update()
    {
        if (PauseManager.IsPaused) return;  // << หยุดทั้งเลเวลขณะ Pause
        if (phase != GamePhase.Running || levels == null || levels.Length == 0) return;
        var cfg = currentLevelConfig;
        if (cfg == null) return;
        // ----- Timer -----
        if (timerStarted && !timerPaused)
        {
            levelTimeElapsed += Time.unscaledDeltaTime;
            if (cfg.timeLimit > 0f)
            {
                float remaining = Mathf.Max(0f, levelTimeLimit - levelTimeElapsed);
                UpdateLevelTimerText(remaining);
                SetPanicBgm(remaining > 0f && remaining <= panicTimeThresholdSec);
                if (remaining <= 0f)
                {
                    StopLevelTimer();
                    TriggerStageFail("Time up");
                    return;
                }
            }
            else
            {
                UpdateLevelTimerText(levelTimeElapsed); // ใช้เป็น stopwatch
            }
        }
        // ----- Level 2 ticks -----
        if (currentLevelConfig?.levelIndex == 2)
            Level2Controller.Instance?.Tick(Time.unscaledDeltaTime);
        if (currentLevelConfig?.levelIndex == 2 && level2_useTriangleObjective)
        {
            int prev = level2_triangleLinkedCount;
            level2_triangleLinkedCount = Level2Controller.Instance ? Level2Controller.Instance.GetTouchedNodeCount() : 0;
            level2_triangleComplete = Level2Controller.Instance && Level2Controller.Instance.IsTriangleComplete();
            if (prev != level2_triangleLinkedCount)
                LevelTaskUI.I?.Refresh();
        }
                // ===== Level 3 tick =====
        if (cfg.levelIndex == 3 && level3_enableBoss)
        {
            // เช็คพัซเซิลเชื่อมจุดตอน vanish
            if (level3_phaseChangeActive && level3_puzzleStage > 0)
            {
                level3_puzzleCheckTimer += Time.unscaledDeltaTime;
                if (level3_puzzleCheckTimer >= level3_puzzleCheckPeriod)
                {
                    level3_puzzleCheckTimer = 0f;
                    if (L3_CheckPuzzleConnected(level3_puzzleA, level3_puzzleB))
                    {
                        // ผ่านขั้นนี้ → ขยับสัดส่วนการเติมบอร์ด
                        level3_puzzleStage++;
                        if (level3_puzzleStage <= 3)
                        {
                            float frac = (level3_puzzleStage) / 3f; // 1/3 → 2/3 → 3/3
                            L3_ResetBoardToFill(frac);
                            L3_PickPuzzlePoints(); // จุดใหม่ทุกขั้น
                            UIManager.Instance?.ShowMessage($"Puzzle stage {level3_puzzleStage}/3", 2f);
                        }
                        if (level3_puzzleStage > 3)
                        {
                            // จบ phase change – บอสกลับมา
                            level3_phaseChangeActive = false;
                            UIManager.Instance?.ShowMessage("Hydra reappears!", 2f);
                            // กลับมาเดินกลไกปกติ
                            if (level3_conveyorRoutine == null) level3_conveyorRoutine = StartCoroutine(L3_ConveyorLoop());
                            if (level3_lockRoutine     == null) level3_lockRoutine     = StartCoroutine(L3_LockWaveLoop());
                            if (level3_fieldRoutine    == null) level3_fieldRoutine    = StartCoroutine(L3_FieldEffectsLoop());
                            if (level3_deleteRoutine   == null) level3_deleteRoutine   = StartCoroutine(L3_DeleteActionLoop());
                        }
                    }
                }
            }
        }
        // ----- Win condition -----
        if (CheckWinConditions(cfg) && !(TurnManager.Instance?.IsScoringAnimation ?? false))
            ShowStageClearAndShop(cfg);
    }
    // ------------------------------ Public hooks ------------------------------
    public void OnScoreOrWordProgressChanged()
    {
        if (phase != GamePhase.Running) return;
        // รีเฟรช Task panel ทุกครั้งที่คะแนน/เป้าหมายคำคืบหน้า
        LevelTaskUI.I?.Refresh();
        // ของเดิม (ด่าน 2 ฯลฯ)
        if (currentLevelConfig?.levelIndex == 2)
        {
            Level2_TryUnlockByWordLength();
            if (level2_benchPenaltyPerMove > 0) Level2_TryApplyBenchPenalty();
        }
        if (CheckWinConditions(currentLevelConfig) && !(TurnManager.Instance?.IsScoringAnimation ?? false))
            ShowStageClearAndShop(currentLevelConfig);
    }
    public void RegisterConfirmedWords(IEnumerable<string> words)
    {
        if (phase == GamePhase.GameOver || words == null) return;
        if (currentLevelConfig?.levelIndex != 1) return;
        int before = itWordsFound.Count;
        foreach (var w in words)
        {
            var n = Normalize(w);
            if (IsITWord(n)) itWordsFound.Add(n);
        }
        if (itProgressText) itProgressText.text = $"IT words: {itWordsFound.Count}/{itWordsTargetLevel1}";
        if (itWordsFound.Count != before) OnScoreOrWordProgressChanged();
        // --- WordRequest / Themed words ---
        var cfg2 = currentLevelConfig;
        if (cfg2 != null && cfg2.requireThemedWords && cfg2.requiredThemeCount > 0)
        {
            // words ที่ส่งมาคือรายการ "คำถูกต้อง" สำหรับเทิร์นนี้แล้ว
            int add = words.Count();  // ✅ นับทั้งหมด ไม่กรอง IT อีกต่อไป
            if (add > 0)
            {
                wordRequestsDone = Mathf.Min(wordRequestsDone + add, cfg2.requiredThemeCount); // กันเกินเป้า
                LevelTaskUI.I?.Refresh();
                OnScoreOrWordProgressChanged();
            }
        }
    }
    public void OnFirstConfirm()
    {
        if (phase != GamePhase.Ready) return;
        if (levelTimeLimit > 0f) StartLevelTimer();
        if (!timerStarted) { timerStarted = true; timerPaused = false; }
        phase = GamePhase.Running;
        // เริ่ม wave x2 ของด่าน 2 ถ้ายังไม่เริ่ม
        var cfg = currentLevelConfig;
        // ✅ ให้ Level2Controller จัดการทั้งหมด
        if (currentLevelConfig?.levelIndex == 2)
            Level2Controller.Instance?.OnTimerStart();
    }
    public void PauseLevelTimer() { timerPaused = true; }
    public void ResumeLevelTimer() { timerPaused = false; }
    public void ShowToast(string msg, Color col)
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowFloatingToast(msg, col, 2f);
        else Debug.Log(msg);
    }
    // ------------------------------ Flow ------------------------------
    private void SetupLevel(int idx)
    {
        idx = Mathf.Clamp(idx, 0, levels.Length - 1);
        StopAllLoops();
        isGameOver = false;
        phase = GamePhase.Setup;
        currentLevel = idx;
        currentLevelConfig = levels[currentLevel];
        // UI
        if (levelText) levelText.text = $"Level {currentLevelConfig.levelIndex}";
        levelTimeElapsed = 0f;
        levelTimeLimit = Mathf.Max(0f, currentLevelConfig.timeLimit);
        levelTimerRunning = false;
        timerStarted = timerPaused = false;
        UpdateLevelTimerText(levelTimeLimit > 0 ? levelTimeLimit : 0f);
        // ด่าน 1: reset progress
        itWordsFound.Clear();
        if (itProgressText)
        {
            if (currentLevelConfig.levelIndex == 1)
            {
                itProgressText.gameObject.SetActive(true);
                itProgressText.text = $"IT words: 0/{itWordsTargetLevel1}";
            }
            else itProgressText.gameObject.SetActive(false);
        }
        wordRequestsDone = 0;
        // เตรียมบอร์ด/เบนช์
        BoardManager.Instance?.GenerateBoard();
        TurnManager.Instance?.ResetForNewLevel();
        TileBag.Instance?.RefillTileBag();
        BenchManager.Instance?.RefillEmptySlots();
        TurnManager.Instance?.UpdateBagUI();
        if (currentLevelConfig?.levelIndex == 2)
        {
            Level2_ClearLockedSegments();
            Level2_SpawnLockedSegments();
        }
        // เรียก Garbled ครั้งเดียวพอ
        Level1GarbledIT.Instance?.ClearAll();
        Level1GarbledIT.Instance?.Setup(currentLevelConfig);
        // ด่าน 2: seed/ธีม/เริ่มโซน x2
        if (currentLevelConfig?.levelIndex == 2)
        {
            Level2_ClearLockedSegments();
            Level2Controller.Instance?.Setup();   // ← สร้าง Triangle ก่อน
            Level2_SpawnLockedSegments();         // ← แล้วค่อยวางล็อก
        }
        // ด่าน 3: Boss
        if (currentLevelConfig?.levelIndex == 3)
        {
            // ธีม
            Debug.Log("[Level3] Apply theme: black-purple background, Hydra behind board.");
            // อัปเกรดถาวร (ใช้เหมือนเลเวล 2)
            var prog = PlayerProgressSO.Instance?.data;
            if (prog != null) TurnManager.Instance?.UpgradeMaxMana(prog.maxMana);
            level3_bossHP = Mathf.Max(1, level3_bossMaxHP);
            level3_phaseChangeActive = false;
            level3_phaseTriggered = false;
            level3_sprintTriggered = false;
            L3_ClearZonesAndLocks();
            L3_UpdateBossUI();
            // สตาร์ทกลไก
            if (level3_enableBoss)
            {
                if (level3_conveyorRoutine == null) level3_conveyorRoutine = StartCoroutine(L3_ConveyorLoop());
                if (level3_lockRoutine     == null) level3_lockRoutine     = StartCoroutine(L3_LockWaveLoop());
                if (level3_fieldRoutine    == null) level3_fieldRoutine    = StartCoroutine(L3_FieldEffectsLoop());
                if (level3_deleteRoutine   == null) level3_deleteRoutine   = StartCoroutine(L3_DeleteActionLoop());
            }
        }
        Debug.Log($"▶ เริ่มด่าน {currentLevelConfig.levelIndex} | Time: {currentLevelConfig.timeLimit}s | Score target: {currentLevelConfig.requiredScore}");
        LevelTaskUI.I?.Refresh();    // <-- เพิ่ม
        phase = GamePhase.Ready;
    }
    void SetupLevel_Level2Hook()
    {
        if (currentLevelConfig != null && currentLevelConfig.levelIndex == 2)
            Level2Controller.Instance?.Setup(); // <-- ไม่มีพารามิเตอร์แล้ว
    }
    void Update_Level2Hook()
    {
        if (currentLevelConfig != null && currentLevelConfig.levelIndex == 2)
            Level2Controller.Instance?.Tick(Time.unscaledDeltaTime);
    }
    void OnFirstConfirm_Level2Hook()
    {
        if (currentLevelConfig != null && currentLevelConfig.levelIndex == 2)
            Level2Controller.Instance?.OnTimerStart();
    }
    bool CheckWinConditions_Level2Hook(bool baseOK)
    {
        if (!baseOK) return false;
        if (currentLevelConfig != null && currentLevelConfig.levelIndex == 2)
            return !level2_useTriangleObjective || (Level2Controller.Instance?.IsTriangleComplete() ?? false);
        return true;
    }
    public bool IsTriangleComplete() => level2_triangleComplete;
    private void ShowStageClearAndShop(LevelConfig cfg)
    {
        if (phase == GamePhase.Transition || phase == GamePhase.GameOver) return;
        phase = GamePhase.Transition;
        StopAllLoops();
        var result = BuildStageResult(cfg);
        // บวกเหรียญเข้ากระเป๋าผู้เล่นจริง ๆ
        if (result.totalCoins > 0)
        {
            CurrencyManager.Instance?.Add(result.totalCoins);
            UIManager.Instance?.ShowFloatingToast($"+{result.totalCoins} coins", Color.yellow, 1.2f);
        }
        stageClearPanel?.Show(result, next: () =>
        {
            // ส่งข้อมูลไปฝั่งถัดไป
            StageResultBus.LastResult = result;
            StageResultBus.NextLevelIndex = Mathf.Clamp(currentLevel + 1, 0, levels.Length - 1);
            StageResultBus.GameplaySceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            // คืนเวลา/เสียง + เคลียร์เสียงของซีนปัจจุบันก่อนเปลี่ยน
            Time.timeScale = 1f;
            AudioListener.pause = false;
            BgmPlayer.I?.StopImmediateAndClear();
            SfxPlayer.I?.StopAllAndClearBank();
            // โหลดผ่าน SceneTransitioner (มีเฟด)
            SceneTransitioner.LoadScene(shopSceneName); // <- เดิมเคยใช้ SceneManager.LoadScene
        });
    }

    // ✅ ฟังก์ชันที่ปรับแล้ว — รองรับด่าน 3 อย่างถูกต้อง
    private bool CheckWinConditions(LevelConfig cfg)
    {
        if (isGameOver || TurnManager.Instance == null || cfg == null) return false;

        // ===== Level 3: Victory = Boss HP ≤ 0 (ไม่สนใจ score/words) =====
        if (cfg.levelIndex == 3)
        {
            return level3_bossHP <= 0;
        }

        // ===== Level 1–2: ใช้ score/words + objectives =====
        int score = TurnManager.Instance.Score;
        int words = TurnManager.Instance.UniqueWordsThisLevel;
        bool baseOK = score >= cfg.requiredScore && words >= cfg.requiredWords;
        if (!baseOK) return false;

        if (cfg.levelIndex == 1 && itWordsFound.Count < itWordsTargetLevel1) return false;
        if (cfg.levelIndex == 2 && level2_useTriangleObjective &&
            !(Level2Controller.Instance?.IsTriangleComplete() ?? false)) return false;

        return true;
    }

    public (int linked, int total) GetTriangleLinkProgress()
    {
        var c = Level2Controller.Instance;
        int total = c != null ? Mathf.Min(3, c.NodeCount) : 3;
        int linked = c != null ? c.GetTouchedNodeCount() : 0;
        return (linked, total);
    }
    private StageResult BuildStageResult(LevelConfig cfg)
    {
        var tm = TurnManager.Instance;
        int timeUsed = Mathf.FloorToInt(levelTimeElapsed);
        int words = tm ? tm.UniqueWordsThisLevel : 0;
        int turns = tm ? tm.ConfirmsThisLevel : 0;
        int tilesLeft = TileBag.Instance ? TileBag.Instance.Remaining : 0;
        int moveScore = tm ? tm.Score : 0;
        // คะแนนโบนัส
        int timeLeft = Mathf.Max(0, cfg.targetTimeSec - timeUsed);
        int wordsScoreCount = Mathf.Min(words, Mathf.Max(0, cfg.maxWordsForScore));
        int bonusScore =
            Mathf.Max(0, Mathf.RoundToInt(timeLeft * cfg.coefTimeScore)) +
            (wordsScoreCount * cfg.coefWordScore) +
            (turns * cfg.coefTurnScore) +
            (tilesLeft * cfg.coefTilesLeftScore);
        int totalScore = Mathf.Max(0, moveScore + bonusScore);
        // เหรียญ
        int wordsMoneyCount = Mathf.Min(words, Mathf.Max(0, cfg.maxWordsForMoney));
        int bonusCoins =
            Mathf.Max(0, Mathf.RoundToInt(timeLeft * cfg.coefTimeMoney)) +
            (wordsMoneyCount * cfg.coefWordMoney) +
            (turns * cfg.coefTurnMoney) +
            (tilesLeft * cfg.coefTilesLeftMoney);
        int totalCoins = Mathf.Max(0, cfg.baseCoins + bonusCoins);
        return new StageResult
        {
            levelIndex = cfg.levelIndex,
            timeUsedSec = timeUsed,
            words = words,
            turns = turns,
            tilesLeft = tilesLeft,
            moveScore = moveScore,
            bonusScore = bonusScore,
            totalScore = totalScore,
            baseCoins = cfg.baseCoins,
            bonusCoins = bonusCoins,
            totalCoins = totalCoins
        };
    }
    // ------------------------------ Timer helpers ------------------------------
    private void StartLevelTimer() { if (!levelTimerRunning) { levelTimerRunning = true; levelTimeElapsed = 0f; } }
    private void StopLevelTimer() { if (levelTimerRunning) { levelTimerRunning = false; } }
    // ------------------------------ Game over / stop loops ------------------------------
    private void GameOver(bool win)
    {
        if (isGameOver || phase == GamePhase.GameOver) return;
        isGameOver = true;
        StopLevelTimer();
        StopAllLoops();
        phase = GamePhase.GameOver;
        if (levelTimerText) levelTimerText.color = win ? Color.green : Color.red;
        if (!win) { TriggerStageFail("GameOver(false)"); return; }
        if (win && currentLevelConfig?.levelIndex == 2 && level2_grantWinRewards)
            TryGrantLevel2Rewards(level2_winCogCoin, level2_nextFloorClue);
        Debug.Log("🎉 ชนะทุกด่าน");
    }
    private void StopAllLoops()
    {
        if (level2_x2Routine != null) { StopCoroutine(level2_x2Routine); level2_x2Routine = null; }
        if (level2_benchIssueRoutine != null) { StopCoroutine(level2_benchIssueRoutine); level2_benchIssueRoutine = null; }
        Level2_RevertAllZones();
        // L3
        if (level3_conveyorRoutine != null) { StopCoroutine(level3_conveyorRoutine); level3_conveyorRoutine = null; }
        if (level3_lockRoutine     != null) { StopCoroutine(level3_lockRoutine);     level3_lockRoutine     = null; }
        if (level3_fieldRoutine    != null) { StopCoroutine(level3_fieldRoutine);    level3_fieldRoutine    = null; }
        if (level3_deleteRoutine   != null) { StopCoroutine(level3_deleteRoutine);   level3_deleteRoutine   = null; }
        L3_ClearZonesAndLocks();
    }
    private void L3_UpdateBossUI()
    {
        if (bossHpText) bossHpText.text = $"Hydra HP: {Mathf.Max(0, level3_bossHP)}/{level3_bossMaxHP}";
    }
    // ------------------------------ Utils ------------------------------
    private static string Normalize(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();
    private bool IsITWord(string w)
    {
        if (string.IsNullOrEmpty(w)) return false;
        var n = Normalize(w);
        for (int i = 0; i < itKeywordsLevel1.Length; i++)
        {
            var k = itKeywordsLevel1[i];
            if (string.IsNullOrWhiteSpace(k)) continue;
            if (n.Contains(k.Trim().ToLowerInvariant())) return true;
        }
        return false;
    }
    // ======== Level 2 (เหมือนเดิม) ========
    private void Level2_ApplyThemeAndUpgrades()
    {
        if (level2_applyThemeOnStart)
            Debug.Log("[Level2] Apply theme: dark system with small black motes.");
        var prog = PlayerProgressSO.Instance?.data;
        if (prog != null)
            TurnManager.Instance?.UpgradeMaxMana(PlayerProgressSO.Instance.data.maxMana);
    }
    private void Level2_SeedLockedSlots()
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return;
        int rows = bm.rows, cols = bm.cols;
        var all = new List<BoardSlot>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var s = bm.grid[r, c];
                if (s == null) continue;
                if (s.HasLetterTile()) continue;
                all.Add(s);
            }
        if (all.Count == 0) return;
        int want = Mathf.Clamp(level2_lockedCount, 0, all.Count);
        level2_lockedSlots.Clear();
        for (int i = 0; i < want; i++)
        {
            int idx = UnityEngine.Random.Range(0, all.Count);
            var slot = all[idx];
            all.RemoveAt(idx);
            int reqLen = UnityEngine.Random.Range(level2_requiredLenRange.x, level2_requiredLenRange.y + 1);
            slot.IsLocked = true;
            slot.bg.color = new Color32(120, 120, 120, 255);
            level2_lockedSlots[slot] = reqLen;
        }
        if (level2_lockedSlots.Count > 0)
            UIManager.Instance?.ShowMessage($"Board bugged: {level2_lockedSlots.Count} slots locked (unlock by word length)", 2f);
    }
    private void Level2_TryUnlockByWordLength()
    {
        if (!level2_enableLockedBoard || level2_lockedSlots.Count == 0) return;
        string main = TurnManager.Instance?.LastConfirmedWord ?? string.Empty;
        if (string.IsNullOrWhiteSpace(main)) return;
        int len = main.Trim().Length;
        if (len <= 0) return;
        var toUnlock = level2_lockedSlots.Where(kv => kv.Value == len).Select(kv => kv.Key).ToList();
        if (toUnlock.Count == 0) return;
        foreach (var s in toUnlock)
        {
            if (s == null) { level2_lockedSlots.Remove(s); continue; }
            s.IsLocked = false;
            s.ApplyVisual();
            s.Flash(Color.green, 2, 0.08f);
            level2_lockedSlots.Remove(s);
        }
        UIManager.Instance?.ShowMessage($"Unlocked {toUnlock.Count} bugged slot(s) by length {len}", 2f);
    }
    private IEnumerator Level2_BenchIssueLoop()
    {
        while (!isGameOver && currentLevelConfig != null && currentLevelConfig.levelIndex == 2)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(5f, level2_benchIssueIntervalSec));
            level2_benchIssueActive = true;
            level2_benchIssueEndTime = Time.unscaledTime + Mathf.Max(3f, level2_benchIssueDurationSec);
            level2_lastPenalizedWord = "";
            UIManager.Instance?.ShowMessage("Bench bug active: some bench letters give 0 score!", level2_benchIssueDurationSec);
            while (Time.unscaledTime < level2_benchIssueEndTime && !isGameOver)
                yield return null;
            level2_benchIssueActive = false;
            UIManager.Instance?.ShowMessage("Bench bug ended.", 1.2f);
        }
        level2_benchIssueRoutine = null;
    }
    private void Level2_TryApplyBenchPenalty()
    {
        if (!level2_enableBenchIssue || !level2_benchIssueActive) return;
        if (level2_benchPenaltyPerMove <= 0) return;
        string main = TurnManager.Instance?.LastConfirmedWord ?? string.Empty;
        if (string.IsNullOrWhiteSpace(main)) return;
        if (main.Equals(level2_lastPenalizedWord, StringComparison.OrdinalIgnoreCase)) return;
        int p = Mathf.Abs(level2_benchPenaltyPerMove);
        TurnManager.Instance?.AddScore(-p);
        level2_lastPenalizedWord = main;
    }
    public bool Level2_IsBenchIssueActive() => level2_benchIssueActive;
    public int Level2_SelectZeroCount(int placedCount)
    {
        if (!level2_enableBenchIssue || !level2_benchIssueActive) return 0;
        if (placedCount <= 0) return 0;
        return Mathf.Clamp(level2_benchZeroPerMove, 0, placedCount);
    }
    private bool CheckTriangleComplete()
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return false;
        if (level2_triangleTargets == null || level2_triangleTargets.Length < 3) return false;
        var targets = new List<Vector2Int>();
        foreach (var v in level2_triangleTargets)
        {
            int r = v.x, c = v.y;
            if (r < 0 || r >= bm.rows || c < 0 || c >= bm.cols) return false;
            var slot = bm.grid[r, c];
            if (slot == null || !slot.HasLetterTile()) return false;
            targets.Add(new Vector2Int(r, c));
        }
        var start = targets[0];
        var visited = new bool[bm.rows, bm.cols];
        var q = new Queue<Vector2Int>();
        q.Enqueue(start);
        visited[start.x, start.y] = true;
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nr = cur.x + dr[k], nc = cur.y + dc[k];
                if (nr < 0 || nr >= bm.rows || nc < 0 || nc >= bm.cols) continue;
                if (visited[nr, nc]) continue;
                var s = bm.grid[nr, nc];
                if (s == null || !s.HasLetterTile()) continue;
                visited[nr, nc] = true;
                q.Enqueue(new Vector2Int(nr, nc));
            }
        }
        return visited[targets[1].x, targets[1].y] && visited[targets[2].x, targets[2].y];
    }
    private int Level2_RecomputeTriangleLinks()
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null || level2_triangleTargets == null || level2_triangleTargets.Length < 3)
            return 0;
        // เอาเฉพาะเป้าหมายที่ "มีตัวอักษรอยู่"
        var active = new List<Vector2Int>();
        foreach (var v in level2_triangleTargets)
        {
            int r = v.x, c = v.y;
            if (r < 0 || r >= bm.rows || c < 0 || c >= bm.cols) continue;
            var s = bm.grid[r, c];
            if (s != null && s.HasLetterTile()) active.Add(new Vector2Int(r, c));
        }
        if (active.Count == 0) return 0;
        // BFS ไปตามช่องที่มีตัวอักษร ติดกัน 4 ทิศ
        var visited = new bool[bm.rows, bm.cols];
        var q = new Queue<Vector2Int>();
        q.Enqueue(active[0]);
        visited[active[0].x, active[0].y] = true;
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nr = cur.x + dr[k], nc = cur.y + dc[k];
                if (nr < 0 || nr >= bm.rows || nc < 0 || nc >= bm.cols) continue;
                if (visited[nr, nc]) continue;
                var s = bm.grid[nr, nc];
                if (s == null || !s.HasLetterTile()) continue;
                visited[nr, nc] = true;
                q.Enqueue(new Vector2Int(nr, nc));
            }
        }
        int count = 0;
        foreach (var t in active)
            if (visited[t.x, t.y]) count++;
        return Mathf.Clamp(count, 0, 3);
    }
    private IEnumerator Level2_PeriodicX2Zones(bool spawnImmediately = false)
    {
        if (spawnImmediately)
            ApplyX2ZonesOnce(Mathf.Max(1, level2_x2ZonesPerWave), Mathf.Max(5f, level2_x2ZoneDurationSec));
        while (!isGameOver && currentLevelConfig != null && currentLevelConfig.levelIndex == 2)
        {
            float wait = Mathf.Max(1f, level2_x2IntervalSec);
            yield return new WaitForSecondsRealtime(wait);
            ApplyX2ZonesOnce(Mathf.Max(1, level2_x2ZonesPerWave), Mathf.Max(5f, level2_x2ZoneDurationSec));
        }
        level2_x2Routine = null;
    }
    private void ApplyX2ZonesOnce(int zones, float duration)
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return;
        if (bm.rows < 3 || bm.cols < 3) return;
        Level2_RevertAllZones();
        int rows = bm.rows, cols = bm.cols;
        int attempts = 0, maxAttempts = 200;
        var chosenCenters = new List<Vector2Int>();
        while (chosenCenters.Count < zones && attempts++ < maxAttempts)
        {
            int r = UnityEngine.Random.Range(1, rows - 1);
            int c = UnityEngine.Random.Range(1, cols - 1);
            bool tooClose = chosenCenters.Any(cc => Mathf.Abs(cc.x - r) + Mathf.Abs(cc.y - c) < 3);
            if (tooClose) continue;
            chosenCenters.Add(new Vector2Int(r, c));
        }
        if (chosenCenters.Count == 0) return;
        foreach (var center in chosenCenters)
        {
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    int rr = center.x + dr, cc = center.y + dc;
                    if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) continue;
                    var slot = bm.grid[rr, cc];
                    if (slot == null) continue;
                    level2_activeZoneChanges.Add((new Vector2Int(rr, cc), slot.type, slot.manaGain));
                    slot.type = level2_multiplierSlotType;
                    slot.ApplyVisual();
                }
        }
        if (level2_activeZoneChanges.Count > 0)
        {
            UIManager.Instance?.ShowMessage("x2 Zones appeared!", 2f);
            StartCoroutine(Level2_RevertZonesAfter(duration));
        }
    }
    private IEnumerator Level2_RevertZonesAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        Level2_RevertAllZones();
        UIManager.Instance?.ShowMessage("x2 Zones ended", 1.5f);
    }
    private void Level2_RevertAllZones()
    {
        if (level2_activeZoneChanges.Count == 0) return;
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) { level2_activeZoneChanges.Clear(); return; }
        foreach (var it in level2_activeZoneChanges)
        {
            var v = it.pos;
            if (v.x < 0 || v.x >= bm.rows || v.y < 0 || v.y >= bm.cols) continue;
            var s = bm.grid[v.x, v.y];
            if (s == null) continue;
            s.type = it.prevType;
            s.manaGain = it.prevMana;
            s.ApplyVisual();
        }
        level2_activeZoneChanges.Clear();
    }
    // ===== Rewards (safe reflection, ไม่พังถ้าไม่มีฟิลด์/พร็อพ) =====
    private void TryGrantLevel2Rewards(int addCogCoin, string clue)
    {
        try
        {
            var so = PlayerProgressSO.Instance;
            var data = so != null ? so.data : null;
            if (data == null) { Debug.Log($"[Level2] Reward hook — clue: {clue}"); return; }
            int add = Mathf.Max(0, addCogCoin);
            var t = data.GetType();
            var field = t.GetField("cogCoin") ?? t.GetField("CogCoin");
            if (field != null) { int cur = Convert.ToInt32(field.GetValue(data)); field.SetValue(data, cur + add); return; }
            var prop = t.GetProperty("cogCoin") ?? t.GetProperty("CogCoin");
            if (prop != null && prop.CanRead && prop.CanWrite)
            { int cur = Convert.ToInt32(prop.GetValue(data, null)); prop.SetValue(data, cur + add, null); return; }
            Debug.Log($"[Level2] (Reward hook) No cogCoin field/property found. Clue: {clue}");
        }
        catch (Exception ex) { Debug.LogWarning($"[Level2] Reward hook exception: {ex.Message}"); }
    }
    public void TriggerBenchIssueAfterRefill()
    {
        if (!level2_enableBenchIssue) return;
        if (currentLevelConfig == null || currentLevelConfig.levelIndex != 2) return;
        if (level2_benchIssueAfterRefillCo != null)
            StopCoroutine(level2_benchIssueAfterRefillCo);
        level2_benchIssueAfterRefillCo = StartCoroutine(Level2_BenchIssueAfterRefillCo());
    }
    private IEnumerator Level2_BenchIssueAfterRefillCo()
    {
        var bm = BenchManager.Instance;
        if (bm == null) yield break;
        // รอจนเติม Bench เสร็จ (รวมอนิเมชัน)
        while (bm.IsRefilling()) yield return null;
        yield return null; // กัน 1 เฟรมใหญ่วางลงจริง
        // เคลียร์สถานะเก่าก่อน
        ScoreManager.ClearZeroScoreTiles();
        foreach (var t in bm.GetAllBenchTiles())
            t.SetBenchIssueOverlay(false);
        // สุ่ม x ตัวจาก Bench
        var pool = new List<LetterTile>(bm.GetAllBenchTiles());
        int pick = Mathf.Clamp(level2_benchIssueCount, 0, pool.Count);
        var chosen = new List<LetterTile>();
        for (int i = 0; i < pick && pool.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            chosen.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        // ทำให้ตัวที่ถูกเลือก "คะแนน = 0" + ใส่ overlay
        if (chosen.Count > 0)
        {
            ScoreManager.MarkZeroScoreTiles(chosen);
            foreach (var t in chosen)
                t.SetBenchIssueOverlay(true, level2_benchIssueOverlayColor);
            UIManager.Instance?.ShowFloatingToast($"Bench issue: {chosen.Count} tile(s) score 0 next turn", Color.gray, 2f);
        }
        level2_benchIssueAfterRefillCo = null;
    }
    private void Level2_ClearLockedSegments()
    {
        if (level2_lockedSegmentSlots.Count == 0) return;
        foreach (var s in level2_lockedSegmentSlots)
            if (s)
            {
                s.IsLocked = false;                            // ✅ ล้างสถานะล็อก
                s.SetLockedVisual(false);
                s.ApplyVisual();                               // รีเฟรชหน้าตาเผื่อมีผลสี/ไอคอน
            }
        level2_lockedSegmentSlots.Clear();
    }
    private void Level2_SpawnLockedSegments()
    {
        if (!level2_enableLockedSegments) return;
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return;
        int rows = bm.rows, cols = bm.cols;
        int segLen = Mathf.Max(1, level2_lockedSegmentLength);
        int segCount = Mathf.Max(0, level2_lockedSegmentCount);
        int centerR = rows / 2;
        int centerC = cols / 2;
        bool InCenter3x3(int r, int c) => Mathf.Abs(r - centerR) <= 1 && Mathf.Abs(c - centerC) <= 1;
        // 8 ทิศ (กันแตะทั้งข้างและชนมุม)
        Vector2Int[] ADJ8 = new Vector2Int[] {
            new Vector2Int(-1, 0), new Vector2Int(1, 0),
            new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(-1,-1), new Vector2Int(-1, 1),
            new Vector2Int( 1,-1), new Vector2Int( 1, 1),
        };
        bool NearTriangle8(int r, int c)
        {
            if (Level2Controller.IsTriangleCell(r, c)) return true;
            for (int i = 0; i < ADJ8.Length; i++)
            {
                int nr = r + ADJ8[i].x, nc = c + ADJ8[i].y;
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                if (Level2Controller.IsTriangleCell(nr, nc)) return true;
            }
            return false;
        }
        bool HasLockedNeighbor8(int r, int c)
        {
            for (int i = 0; i < ADJ8.Length; i++)
            {
                int nr = r + ADJ8[i].x, nc = c + ADJ8[i].y;
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                var ns = bm.grid[nr, nc];
                if (ns != null && ns.IsLocked) return true;
            }
            return false;
        }
        int attemptsPerSeg = 200;
        for (int seg = 0; seg < segCount; seg++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < attemptsPerSeg && !placed; attempt++)
            {
                bool vertical = UnityEngine.Random.value < 0.5f;
                int startR = vertical
                    ? UnityEngine.Random.Range(0, rows - segLen + 1)
                    : UnityEngine.Random.Range(0, rows);
                int startC = vertical
                    ? UnityEngine.Random.Range(0, cols)
                    : UnityEngine.Random.Range(0, cols - segLen + 1);
                var candidates = new List<BoardSlot>();
                for (int k = 0; k < segLen; k++)
                {
                    int r = startR + (vertical ? k : 0);
                    int c = startC + (vertical ? 0 : k);
                    var s = bm.grid[r, c];
                    // เงื่อนไข: ต้องอยู่นอกกลาง 3×3, ยังไม่ล็อก, ไม่มีตัวอักษร, และ "ไม่มีเพื่อนล็อก" 8 ทิศ
                    if (!s || InCenter3x3(r, c) || s.IsLocked || s.HasLetterTile()
                        || Level2Controller.IsTriangleCell(r, c)       // ✅ กันวางทับ Triangle
                        || HasLockedNeighbor8(r, c)
                        || NearTriangle8(r, c))
                    {
                        candidates.Clear(); break;
                    }
                    candidates.Add(s);
                }
                if (candidates.Count == segLen)
                {
                    foreach (var s in candidates)
                    {
                        s.IsLocked = true;                                 // ✅ ต้องตั้งสถานะล็อกจริง
                        s.SetLockedVisual(true, level2_lockedOverlayColor);
                        level2_lockedSegmentSlots.Add(s);
                    }
                    placed = true;
                }
            }
        }
    }
    // ==============================
    // Level 3: Boss Hydra – APIs
    // ==============================
    /// <summary>
    /// TurnManager เรียกเมื่อยืนยันคำ: ส่งจำนวนตัวอักษรที่ "วางใหม่" (placedCount),
    /// ผลรวมคะแนนตัวอักษร (หลัง DL/TL และ zero score) ของ "ตัวอักษรที่วางใหม่เท่านั้น",
    /// ความยาวคำหลัก และตำแหน่งไทล์ที่ผู้เล่นวาง (ไว้เช็กโซน buff/debuff)
    /// </summary>
    public void Level3_OnPlayerDealtWord(int placedCount, int placedLettersDamageSum, int mainWordLen, List<Vector2Int> placedCoords)
    {
        if (!level3_enableBoss) return;
        var cfg = currentLevelConfig;
        if (cfg == null || cfg.levelIndex != 3) return;
        if (phase != GamePhase.Running) return;
        if (level3_phaseChangeActive) return; // vanish อยู่ – ยังตีบอสไม่ได้
        int sum = Mathf.Max(0, placedLettersDamageSum);
        if (sum <= 0 || placedCount <= 0) return;
        // “สุ่มจากจำนวนตัวอักษรที่ลงเป็นช่องในการสุ่ม” → best-of-N roll ในช่วง [1..sum]
        int draws = Mathf.Max(1, placedCount);
        int best = 0;
        for (int i = 0; i < draws; i++)
        {
            int roll = UnityEngine.Random.Range(1, sum + 1); // inclusive max
            if (roll > best) best = roll;
        }
        float dmg = best;
        // Field Effects
        bool hitBuff = false, hitDebuff = false;
        if (placedCoords != null && placedCoords.Count > 0 && level3_activeZones.Count > 0)
        {
            foreach (var z in level3_activeZones)
            {
                if (Time.unscaledTime > z.end) continue;
                foreach (var p in placedCoords)
                {
                    if (z.rect.Contains(new Vector2Int(p.x, p.y)))
                    {
                        if (z.isBuff) hitBuff = true; else hitDebuff = true;
                    }
                }
            }
        }
        if (hitBuff)   dmg *= 2f;
        if (hitDebuff) dmg *= 0.5f;
        // Critical
        if (mainWordLen >= level3_criticalLength)
            dmg *= (1f + Mathf.Max(0f, level3_criticalBonus));
        int final = Mathf.Max(0, Mathf.RoundToInt(dmg));
        if (final <= 0) return;
        level3_bossHP = Mathf.Max(0, level3_bossHP - final);
        L3_UpdateBossUI();
        UIManager.Instance?.ShowMessage($"🗡 Hydra -{final}", 1.5f);
        // Thresholds
        int hp50 = Mathf.CeilToInt(level3_bossMaxHP * level3_phaseChangeHPPercent);
        int hp25 = Mathf.CeilToInt(level3_bossMaxHP * level3_sprintHPPercent);
        if (!level3_phaseTriggered && level3_bossHP <= hp50)
        {
            level3_phaseTriggered = true;
            StartCoroutine(L3_StartPhaseChange());
        }
        if (!level3_sprintTriggered && level3_bossHP <= hp25)
        {
            level3_sprintTriggered = true;
            // บีบเวลาให้เหลือ 3:00 (ถ้าน้อยกว่านี้ก็เซ็ตเป็น 3:00 อยู่ดี)
            levelTimeLimit = levelTimeElapsed + Mathf.Max(0f, level3_sprintRemainingSec);
            UpdateLevelTimerText(level3_sprintRemainingSec);
            UIManager.Instance?.ShowMessage("⏱ Hydra enraged: time set to 3:00!", 2.5f);
        }
        if (level3_bossHP <= 0)
        {
            GameOver(true); // จบเกมตามสเป็ค
        }
    }
    private IEnumerator L3_StartPhaseChange()
    {
        if (level3_phaseChangeActive) yield break;
        // หยุดคลื่น/โซน/ลบต่าง ๆ ชั่วคราว
        if (level3_conveyorRoutine != null) { StopCoroutine(level3_conveyorRoutine); level3_conveyorRoutine = null; }
        if (level3_lockRoutine     != null) { StopCoroutine(level3_lockRoutine);     level3_lockRoutine     = null; }
        if (level3_fieldRoutine    != null) { StopCoroutine(level3_fieldRoutine);    level3_fieldRoutine    = null; }
        if (level3_deleteRoutine   != null) { StopCoroutine(level3_deleteRoutine);   level3_deleteRoutine   = null; }
        L3_ClearZonesAndLocks();
        level3_phaseChangeActive = true;
        level3_puzzleStage = 1;
        // + เวลา 7:30 (เพราะ levelTimeLimit คือ "เวลาสูงสุดนับจากเริ่ม")
        levelTimeLimit += Mathf.Max(0f, level3_phaseTimeBonusSec);
        // reset board → เหลือเติม 1/3
        L3_ResetBoardToFill(1f/3f);
        L3_PickPuzzlePoints();
        UIManager.Instance?.ShowMessage("Hydra vanished! Connect the two points (1/3 → 3/3).", 3f);
        yield break;
    }
    private void L3_ResetBoardToFill(float fraction)
    {
        var bm = BoardManager.Instance; if (bm == null) return;
        // สร้างบอร์ดใหม่ทั้งหมด แล้วตัดให้เหลือตาม fraction
        bm.GenerateBoard();
        int rows = bm.rows, cols = bm.cols;
        var filled = new List<BoardSlot>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var s = bm.grid[r, c];
                if (s != null && s.HasLetterTile()) filled.Add(s);
            }
        int wantKeep = Mathf.Clamp(Mathf.RoundToInt(rows * cols * Mathf.Clamp01(fraction)), 0, filled.Count);
        int toRemove = Mathf.Max(0, filled.Count - wantKeep);
        for (int i = 0; i < toRemove; i++)
        {
            int idx = UnityEngine.Random.Range(0, filled.Count);
            var s = filled[idx]; filled.RemoveAt(idx);
            var t = s.RemoveLetter();
            if (t != null) SpaceManager.Instance.RemoveTile(t); // ทิ้ง
        }
    }
    private void L3_PickPuzzlePoints()
    {
        var bm = BoardManager.Instance; if (bm == null) return;
        var candidates = new List<Vector2Int>();
        for (int r = 0; r < bm.rows; r++)
            for (int c = 0; c < bm.cols; c++)
                if (bm.grid[r, c] != null && bm.grid[r, c].HasLetterTile())
                    candidates.Add(new Vector2Int(r, c));
        if (candidates.Count < 2) { level3_puzzleA = Vector2Int.zero; level3_puzzleB = Vector2Int.zero; return; }
        level3_puzzleA = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        level3_puzzleB = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        UIManager.Instance?.UpdateTriangleHint(false); // reuse indicator ถ้าต้องการ
    }
    private bool L3_CheckPuzzleConnected(Vector2Int a, Vector2Int b)
    {
        var bm = BoardManager.Instance; if (bm == null || bm.grid == null) return false;
        if (a == b) return true;
        bool[,] vis = new bool[bm.rows, bm.cols];
        var q = new Queue<Vector2Int>();
        if (a.x < 0 || a.x >= bm.rows || a.y < 0 || a.y >= bm.cols) return false;
        if (b.x < 0 || b.x >= bm.rows || b.y < 0 || b.y >= bm.cols) return false;
        if (bm.grid[a.x, a.y] == null || !bm.grid[a.x, a.y].HasLetterTile()) return false;
        if (bm.grid[b.x, b.y] == null || !bm.grid[b.x, b.y].HasLetterTile()) return false;
        q.Enqueue(a); vis[a.x, a.y] = true;
        int[] dr = {-1,1,0,0}; int[] dc = {0,0,-1,1};
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nr = cur.x + dr[k], nc = cur.y + dc[k];
                if (nr < 0 || nr >= bm.rows || nc < 0 || nc >= bm.cols) continue;
                if (vis[nr,nc]) continue;
                var s = bm.grid[nr, nc];
                if (s == null || !s.HasLetterTile()) continue;
                vis[nr, nc] = true;
                if (nr == b.x && nc == b.y) return true;
                q.Enqueue(new Vector2Int(nr, nc));
            }
        }
        return false;
    }
    private IEnumerator L3_ConveyorLoop()
    {
        while (!isGameOver && currentLevelConfig?.levelIndex == 3 && !level3_phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(5f, level3_conveyorIntervalSec));
            var bm = BoardManager.Instance;
            if (bm == null || bm.grid == null) continue;
            var slots = new List<BoardSlot>();
            for (int r = 0; r < bm.rows; r++)
                for (int c = 0; c < bm.cols; c++)
                {
                    var s = bm.grid[r,c];
                    if (s == null) continue;
                    var t = s.GetLetterTile();
                    if (t == null) continue;
                    if (t.isLocked) continue; // อย่าแตะไทล์ที่ล็อกแล้ว
                    slots.Add(s);
                }
            if (slots.Count < 2) { continue; }
            var tiles = new List<LetterTile>(slots.Count);
            foreach (var s in slots) tiles.Add(s.RemoveLetter());
            int shift = Mathf.Max(1, level3_conveyorShift) % tiles.Count;
            if (shift > 0)
            {
                var rotated = new List<LetterTile>(tiles.Count);
                for (int i = 0; i < tiles.Count; i++)
                    rotated.Add(tiles[(i - shift + tiles.Count) % tiles.Count]);
                tiles = rotated;
            }
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var tile = tiles[i];
                if (!slot || !tile) continue;
                tile.transform.SetParent(slot.transform, false);
                var rt = tile.GetComponent<RectTransform>();
                if (rt != null) { rt.anchoredPosition = Vector2.zero; rt.localScale = Vector3.one; }
                else { tile.transform.localPosition = Vector3.zero; tile.transform.localScale = Vector3.one; }
                slot.Flash(new Color(0.7f,0.7f,1f,1f), 1, 0.06f);
            }
            UIManager.Instance?.ShowMessage("Conveyor Shuffle!", 1.5f);
        }
        level3_conveyorRoutine = null;
    }
    private IEnumerator L3_LockWaveLoop()
    {
        while (!isGameOver && currentLevelConfig?.levelIndex == 3 && !level3_phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(10f, level3_lockWaveIntervalSec));
            var bm = BoardManager.Instance; if (bm == null || bm.grid == null) continue;
            // เลือกสุ่มและล็อกชั่วคราว
            var all = new List<BoardSlot>();
            for (int r = 0; r < bm.rows; r++)
                for (int c = 0; c < bm.cols; c++)
                {
                    var s = bm.grid[r,c];
                    if (s != null && !s.IsLocked) all.Add(s);
                }
            int want = Mathf.Min(level3_lockCountPerWave, all.Count);
            for (int i = 0; i < want; i++)
            {
                int idx = UnityEngine.Random.Range(0, all.Count);
                var s = all[idx]; all.RemoveAt(idx);
                s.IsLocked = true; s.ApplyVisual(); s.Flash(Color.gray, 2, 0.08f);
                level3_lockedByBoss.Add(s);
            }
            UIManager.Instance?.ShowMessage($"Hydra locks {want} slots!", 1.5f);
            yield return new WaitForSecondsRealtime(Mathf.Max(3f, level3_lockDurationSec));
            // ปลดล็อก
            foreach (var s in level3_lockedByBoss) { if (s) { s.IsLocked = false; s.ApplyVisual(); } }
            level3_lockedByBoss.Clear();
        }
        level3_lockRoutine = null;
    }
    private IEnumerator L3_FieldEffectsLoop()
    {
        while (!isGameOver && currentLevelConfig?.levelIndex == 3 && !level3_phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(8f, level3_fieldEffectIntervalSec));
            var bm = BoardManager.Instance; if (bm == null) continue;
            level3_activeZones.RemoveAll(z => Time.unscaledTime > z.end);
            // spawn buff/debuff zones
            int toSpawnEach = Mathf.Max(1, level3_zonesPerType);
            for (int t = 0; t < toSpawnEach; t++)
            {
                var buff = new L3Zone { rect = L3_RandomRectInBoard(bm, level3_zoneSize), isBuff = true,  end = Time.unscaledTime + level3_fieldEffectDurationSec };
                var debf = new L3Zone { rect = L3_RandomRectInBoard(bm, level3_zoneSize), isBuff = false, end = Time.unscaledTime + level3_fieldEffectDurationSec };
                level3_activeZones.Add(buff);
                level3_activeZones.Add(debf);
            }
            UIManager.Instance?.ShowMessage("Zones: Green x2 / Red x0.5", 1.8f);
        }
        level3_fieldRoutine = null;
    }
    private RectInt L3_RandomRectInBoard(BoardManager bm, int size)
    {
        // NOTE: RectInt(x=row, y=col, width=heightInRows, height=widthInCols) – กำหนดตามแกนของบอร์ด
        int w = Mathf.Clamp(size, 1, Mathf.Max(1, bm.cols));
        int h = Mathf.Clamp(size, 1, Mathf.Max(1, bm.rows));
        int x = UnityEngine.Random.Range(0, Mathf.Max(1, bm.rows - h + 1));
        int y = UnityEngine.Random.Range(0, Mathf.Max(1, bm.cols - w + 1));
        return new RectInt(x, y, h, w); // x=row, y=col
    }
    private IEnumerator L3_DeleteActionLoop()
    {
        while (!isGameOver && currentLevelConfig?.levelIndex == 3 && !level3_phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(5f, level3_deleteActionIntervalSec));
            // สุ่มว่าจะลบ "ตัวอักษรบนบอร์ด/เบนช์" หรือ "การ์ด"
            bool tryLetters = UnityEngine.Random.value < 0.6f; // 60% ลบตัวอักษร
            if (tryLetters && Time.unscaledTime >= level3_nextLetterDeleteTime)
            {
                bool deleted = L3_DeleteLettersFromBoard(level3_deleteBoardCount);
                if (!deleted && OnBossDeleteBenchRequest != null)
                {
                    OnBossDeleteBenchRequest.Invoke(Mathf.Max(1, level3_deleteBenchCount));
                    UIManager.Instance?.ShowMessage("Hydra deletes bench letters!", 1.5f);
                }
                else if (!deleted)
                {
                    UIManager.Instance?.ShowMessage("Hydra tried to delete letters", 1.2f);
                }
                level3_nextLetterDeleteTime = Time.unscaledTime + level3_deleteLettersCooldownSec;
            }
            else if (!tryLetters && Time.unscaledTime >= level3_nextCardDeleteTime)
            {
                if (OnBossDeleteRandomCardRequest != null)
                {
                    OnBossDeleteRandomCardRequest.Invoke();
                    UIManager.Instance?.ShowMessage("Hydra deletes a card!", 1.5f);
                }
                else if (OnBossLockCardSlotRequest != null)
                {
                    OnBossLockCardSlotRequest.Invoke(level3_cardSlotLockDurationSec);
                    UIManager.Instance?.ShowMessage("Hydra locks card slot!", 1.5f);
                }
                else
                {
                    UIManager.Instance?.ShowMessage("Hydra tried to mess with cards", 1.2f);
                }
                level3_nextCardDeleteTime = Time.unscaledTime + level3_deleteCardsCooldownSec;
            }
        }
        level3_deleteRoutine = null;
    }
    private bool L3_DeleteLettersFromBoard(int count)
    {
        var bm = BoardManager.Instance; if (bm == null || bm.grid == null) return false;
        var filled = new List<BoardSlot>();
        for (int r = 0; r < bm.rows; r++)
            for (int c = 0; c < bm.cols; c++)
            {
                var s = bm.grid[r,c];
                if (s != null && s.HasLetterTile())
                {
                    var t = s.GetLetterTile();
                    if (t && !t.isLocked) filled.Add(s);
                }
            }
        if (filled.Count == 0) return false;
        int k = Mathf.Clamp(count, 1, filled.Count);
        for (int i = 0; i < k; i++)
        {
            int idx = UnityEngine.Random.Range(0, filled.Count);
            var s = filled[idx]; filled.RemoveAt(idx);
            var t = s.RemoveLetter();
            if (t) SpaceManager.Instance.RemoveTile(t);
            s.Flash(Color.red, 2, 0.08f);
        }
        UIManager.Instance?.ShowMessage($"Hydra deletes {k} letters on board!", 1.4f);
        return true;
    }
    private void L3_ClearZonesAndLocks()
    {
        level3_activeZones.Clear();
        // ปลดล็อกที่ล็อกโดยบอส
        foreach (var s in level3_lockedByBoss) { if (s) { s.IsLocked = false; s.ApplyVisual(); } }
        level3_lockedByBoss.Clear();
    }
    void TriggerStageFail(string reason)
    {
        if (isGameOver || phase == GamePhase.GameOver) return;
        isGameOver = true;
        StopLevelTimer();
        StopAllLoops();
        phase = GamePhase.GameOver;
        // กันพาเนล Pause ทับ + ตัดเสียง
        PauseManager.I?.ClosePause();
        BgmPlayer.I?.DuckAndStop(0.18f);
        SfxPlayer.I?.StopAllAndClearBank();
        // โชว์ป๊อปอัปแพ้ใต้ Canvas หลัก (เหมือน StageClear)
        var panel = StageFailPanel.Instance ?? FindObjectOfType<StageFailPanel>(true);
        if (panel) panel.Show("Stage Fail", "กลับสู่เมนูหลัก?");
        else
        {
            Debug.LogError("StageFailPanel not found under Main Canvas.");
            PauseManager.I?.Btn_ReturnToMainMenu();
        }
    }
}