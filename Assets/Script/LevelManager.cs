// ========================== LevelManager.cs ==========================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Configs")]
    public LevelConfig[] levels;

    [Header("UI (ผูกใน Inspector)")]
    public TMP_Text levelText;
    public TMP_Text timerText;      // legacy
    public TMP_Text levelTimerText; // ตัวจับเวลาเลเวล (ขึ้น/ลง)
    [Tooltip("Progress ของคำ IT สำหรับด่าน 1 (ไม่ผูกก็ได้)")]
    public TMP_Text itProgressText;

    /// <summary>
    /// Zero-based index within the levels array.
    /// </summary>
    public int CurrentLevel => currentLevel;

    private enum GamePhase { None, Setup, Ready, Running, Transition, GameOver }
    private GamePhase phase = GamePhase.None;
    private int currentLevel;
    private bool isGameOver = false;
    private bool isTransitioning = false;

    private float levelTimeLimit;    // หมายถึง "เวลาสะสมสูงสุดตั้งแต่เริ่มเลเวล" (not remaining)
    private float levelTimeElapsed;  // เวลาที่ผ่านไป
    private bool levelTimerRunning;
    bool timerStarted;
    bool timerPaused;

    private Color _timerDefaultColor = Color.white; // จะ override จากสีจริงใน Awake()

    // ===== Level 1 – IT words requirement =====
    [Header("Level 1 – IT Words")]
    [Tooltip("จำนวน 'คำ IT' ขั้นต่ำที่ต้องทำให้ได้ในด่าน 1")]
    public int itWordsTargetLevel1 = 5;

    [Tooltip("คีย์เวิร์ดสำหรับตรวจกับคำ (จับแบบ contains, ไม่สนตัวพิมพ์)")]
    public string[] itKeywordsLevel1 = new string[]
    {
        "it","code","bug","dev","server","client","api","database","db","sql",
        "data","cloud","ai","ml","python","java","c#","csharp","unity","scene",
        "asset","compile","build","network","socket","array","stack","cache","login","token"
    };

    private readonly HashSet<string> itWordsFound = new HashSet<string>();

    // ===== Level 1 – Garbled IT Obstacle =====
    [Header("Level 1 – Garbled IT Obstacle")]
    public bool  level1_enableGarbled = true;
    public float level1_garbleTickSec = 3f;
    public int   level1_garbleClusterSize = 6;
    public int   level1_wrongGuessPenalty = 20;
    public float level1_garbleSuspendDuration = 10f;

    private Coroutine level1_garbleRoutine;
    private bool  level1_garbleSuspended = false;
    private float level1_garbleResumeTime = 0f;

    // ===== Level 2 – Triangle + Obstacles =====
    [Header("Level 2 – Triangle Objective")]
    public bool level2_useTriangleObjective = true;
    public Vector2Int[] level2_triangleTargets = new Vector2Int[]
    {
        new Vector2Int(2,2),
        new Vector2Int(2,12),
        new Vector2Int(12,7)
    };
    public float level2_triangleCheckPeriod = 0.5f;
    private bool  level2_triangleComplete = false;
    private float level2_triangleCheckTimer = 0f;

    [Header("Level 2 – Periodic X2 Zones (3x3)")]
    public bool  level2_enablePeriodicX2Zones = true;
    public float level2_x2IntervalSec = 180f;  // 3 นาที
    public int   level2_x2ZonesPerWave = 2;    // 2–3 แล้วแต่ config
    public float level2_x2ZoneDurationSec = 30f;
    public SlotType level2_multiplierSlotType = SlotType.DoubleWord;

    private Coroutine level2_x2Routine;
    private readonly List<(Vector2Int pos, SlotType prevType, int prevMana)> level2_activeZoneChanges
        = new List<(Vector2Int, SlotType, int)>();

    [Header("Level 2 – Locked Board (ปลดด้วยความยาวคำหลัก)")]
    public bool  level2_enableLockedBoard = true;
    public int   level2_lockedCount = 7;
    public Vector2Int level2_requiredLenRange = new Vector2Int(3, 7);
    private readonly Dictionary<BoardSlot,int> level2_lockedSlots = new Dictionary<BoardSlot,int>();

    [Header("Level 2 – Bench Issue (ช่วงเวลาบั๊ก)")]
    public bool  level2_enableBenchIssue = true;
    public float level2_benchIssueIntervalSec = 60f;
    public float level2_benchIssueDurationSec = 20f;
    [Tooltip("จำนวนตัวอักษร (วางในเทิร์น) ที่จะโดนทำคะแนนตัวอักษรเป็น 0 เมื่อ Bench bug ทำงาน")]
    public int level2_benchZeroPerMove = 2;
    [Tooltip("(ตัวเลือก) หักแต้มคงที่ต่อคำหลัก (0 = ไม่หัก)")]
    public int level2_benchPenaltyPerMove = 0;

    private bool level2_benchIssueActive = false;
    private float level2_benchIssueEndTime = 0f;
    private Coroutine level2_benchIssueRoutine;
    private string level2_lastPenalizedWord = "";

    [Header("Level 2 – Theme & Rewards")]
    public bool   level2_applyThemeOnStart = true;
    public bool   level2_grantWinRewards  = true;
    public int    level2_winCogCoin       = 1;
    public string level2_nextFloorClue    = "เลขชั้นถัดไป";

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
    private static readonly WaitForEndOfFrame WaitEOF = new WaitForEndOfFrame();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (levelTimerText) _timerDefaultColor = levelTimerText.color; // เก็บสี default ไว้
        SetPhase(GamePhase.None);
    }

    private void Start()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("[LevelManager] No level configuration provided!");
            return;
        }
        SetupLevel(0);
    }

    private void OnDisable() => StopAllLoops();

    public bool IsGameOver() => isGameOver;

    void UpdateLevelTimerText(float seconds)
    {
        var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int mm = total / 60, ss = total % 60;
        if (levelTimerText) levelTimerText.text = $"{mm:00}:{ss:00}";
    }

    private void Update()
    {
        if (phase != GamePhase.Running || levels == null || levels.Length == 0) return;

        var cfg = GetCurrentConfig();
        if (cfg == null) return;

        // เดินเวลา
        if (timerStarted && !timerPaused)
        {
            levelTimeElapsed += Time.unscaledDeltaTime;

            if (cfg.timeLimit > 0f)
            {
                float remaining = Mathf.Max(0f, levelTimeLimit - levelTimeElapsed);
                UpdateLevelTimerText(remaining);
                if (remaining <= 0f)
                {
                    StopLevelTimer();
                    GameOver(false);
                    return;
                }
            }
            else
            {
                UpdateLevelTimerText(levelTimeElapsed);
            }
        }

        // ===== Level 1 tick =====
        if (cfg.levelIndex == 1 && level1_enableGarbled)
        {
            if (level1_garbleSuspended && Time.unscaledTime >= level1_garbleResumeTime)
                level1_garbleSuspended = false;

            if (!level1_garbleSuspended && level1_garbleRoutine == null)
                level1_garbleRoutine = StartCoroutine(Level1_GarbleLoop());
        }

        // ===== Level 2 tick =====
        if (cfg.levelIndex == 2)
        {
            // Triangle check (throttle)
            if (level2_useTriangleObjective && level2_triangleTargets != null && level2_triangleTargets.Length >= 3)
            {
                level2_triangleCheckTimer += Time.unscaledDeltaTime;
                if (level2_triangleCheckTimer >= level2_triangleCheckPeriod)
                {
                    level2_triangleCheckTimer = 0f;
                    level2_triangleComplete = CheckTriangleComplete();
                    // >>> อัปเดต UI Indicator ทุกครั้งที่เช็ก
                    UIManager.Instance?.UpdateTriangleHint(level2_triangleComplete);
                }
            }

            // Periodic x2 waves
            if (level2_enablePeriodicX2Zones && level2_x2Routine == null)
                level2_x2Routine = StartCoroutine(Level2_PeriodicX2Zones(spawnImmediately: true));

            // Bench issue
            if (level2_enableBenchIssue && level2_benchIssueRoutine == null)
                level2_benchIssueRoutine = StartCoroutine(Level2_BenchIssueLoop());
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

        // เงื่อนไขผ่านด่าน (ยกเว้นเลเวล 3 ให้บอสจัดการเอง)
        if (cfg.levelIndex != 3 && CheckWinConditions(cfg))
        {
            AnnounceLevelComplete();
            _ = StartCoroutine(GoToNextLevel());
        }
    }

    /// <summary>ให้ TurnManager เรียกเมื่อคะแนน/จำนวนคำเปลี่ยน</summary>
    public void OnScoreOrWordProgressChanged()
    {
        if (phase != GamePhase.Running) return;

        // ด่าน 2: ปลดล็อก/หักบั๊กแบบคงที่
        if (GetCurrentConfig()?.levelIndex == 2)
        {
            Level2_TryUnlockByWordLength();
            if (level2_benchPenaltyPerMove > 0) Level2_TryApplyBenchPenalty();
        }

        var cfg = GetCurrentConfig();
        if (cfg != null && cfg.levelIndex != 3 && CheckWinConditions(cfg))
        {
            AnnounceLevelComplete();
            _ = StartCoroutine(GoToNextLevel());
        }
    }

    // ===== ด่าน 1: รับคำ IT ถูกแล้วอัปเดต progress =====
    public void RegisterConfirmedWords(IEnumerable<string> words)
    {
        if (phase == GamePhase.GameOver || words == null) return;
        if (GetCurrentConfig()?.levelIndex != 1) return;

        int before = itWordsFound.Count;
        foreach (var w in words)
        {
            var n = Normalize(w);
            if (IsITWord(n)) itWordsFound.Add(n);
        }
        if (itProgressText) itProgressText.text = $"IT words: {itWordsFound.Count}/{itWordsTargetLevel1}";
        if (itWordsFound.Count != before) OnScoreOrWordProgressChanged();
    }

    private bool CheckWinConditions(LevelConfig cfg)
    {
        if (isGameOver) return false;
        if (TurnManager.Instance == null) return false;

        // ด่าน 3 ให้ระบบบอสจัดการจบเกมเอง
        if (cfg.levelIndex == 3) return false;

        bool baseOK =
            TurnManager.Instance.Score >= cfg.requiredScore &&
            TurnManager.Instance.CheckedWordCount >= cfg.requiredWords;

        if (!baseOK) return false;

        // ด่าน 1: ต้องมีคำ IT ถึงเป้า
        if (cfg.levelIndex == 1)
        {
            if (itWordsFound.Count < itWordsTargetLevel1) return false;
        }

        // ด่าน 2: ต้องปิดสามเหลี่ยม (ถ้าเปิดใช้)
        if (cfg.levelIndex == 2 && level2_useTriangleObjective)
        {
            if (!level2_triangleComplete) return false;
        }

        return true;
    }

    // ------------------------------ Level flow ------------------------------
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
        if (timerText) timerText.gameObject.SetActive(false);
        if (levelTimerText) levelTimerText.color = _timerDefaultColor; // รีเซ็ตสีที่อาจถูกเปลี่ยนตอนจบเกมก่อนหน้า

        // Timer setup
        levelTimeElapsed = 0f;
        levelTimeLimit   = Mathf.Max(0f, cfg.timeLimit);
        levelTimerRunning = false;
        timerStarted = false;
        timerPaused  = false;
        UpdateLevelTimerText(levelTimeLimit > 0 ? levelTimeLimit : 0f);

        // ===== ด่าน 1 reset =====
        itWordsFound.Clear();
        if (itProgressText)
        {
            if (cfg.levelIndex == 1)
            {
                itProgressText.gameObject.SetActive(true);
                itProgressText.text = $"IT words: 0/{itWordsTargetLevel1}";
            }
            else itProgressText.gameObject.SetActive(false);
        }
        level1_garbleSuspended = false;
        level1_garbleResumeTime = 0f;
        if (level1_garbleRoutine != null) { StopCoroutine(level1_garbleRoutine); level1_garbleRoutine = null; }

        // แสดง/ซ่อนแผงเดาคำ IT
        UIManager.Instance?.ShowGarbledUI(cfg.levelIndex == 1 && level1_enableGarbled);

        // ===== ด่าน 2 reset =====
        level2_triangleComplete = false;
        level2_triangleCheckTimer = 0f;
        Level2_RevertAllZones();
        level2_lockedSlots.Clear();
        if (level2_benchIssueRoutine != null) { StopCoroutine(level2_benchIssueRoutine); level2_benchIssueRoutine = null; }
        level2_benchIssueActive = false;
        level2_benchIssueEndTime = 0f;
        level2_lastPenalizedWord = "";

        // ซ่อน Triangle hint ตอนเข้าเลเวลอื่น, เปิดตอนเลเวล 2
        UIManager.Instance?.SetTriangleHintVisible(cfg.levelIndex == 2);

        // Prepare board & turn
        if (BoardManager.Instance != null) BoardManager.Instance.GenerateBoard();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ResetForNewLevel();
            if (TileBag.Instance != null) TileBag.Instance.RefillTileBag();
            if (BenchManager.Instance != null) BenchManager.Instance.RefillEmptySlots();
            TurnManager.Instance.UpdateBagUI(); // เรียกครั้งเดียวหลังเติมครบ
        }

        // ด่าน 2: theme / locked seeds / x2 wave start
        if (cfg.levelIndex == 2)
        {
            Level2_ApplyThemeAndUpgrades();
            if (level2_enableLockedBoard) Level2_SeedLockedSlots();

            // เริ่มโซน x2 ทันที (กันลืม OnFirstConfirm) และอัปเดต triangle hint ครั้งแรก
            if (level2_enablePeriodicX2Zones && level2_x2Routine == null)
                level2_x2Routine = StartCoroutine(Level2_PeriodicX2Zones(spawnImmediately: true));

            UIManager.Instance?.UpdateTriangleHint(level2_triangleComplete);
        }

        // ด่าน 3: Boss
        if (cfg.levelIndex == 3)
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

        Debug.Log($"▶ เริ่มด่าน {cfg.levelIndex} | Time: {cfg.timeLimit}s | Score target: {cfg.requiredScore}");
        SetPhase(GamePhase.Ready);
    }

    private IEnumerator GoToNextLevel()
    {
        if (isTransitioning || phase == GamePhase.Transition || phase == GamePhase.GameOver) yield break;
        isTransitioning = true;
        SetPhase(GamePhase.Transition);

        StopAllLoops();
        yield return WaitEOF;

        if (currentLevel + 1 < levels.Length) SetupLevel(currentLevel + 1);
        else GameOver(true);

        isTransitioning = false;
    }

    /// <summary>เรียกจาก TurnManager ครั้งแรกหลังยืนยันคำแรก เพื่อเริ่มจับเวลา</summary>
    public void OnFirstConfirm()
    {
        if (phase != GamePhase.Ready)
        {
            Debug.LogWarning($"OnFirstConfirm ignored. Phase={phase}");
            return;
        }
        if (levelTimeLimit > 0f) StartLevelTimer();
        if (!timerStarted) { timerStarted = true; timerPaused = false; }
        SetPhase(GamePhase.Running);
        Debug.Log("Level started");

        var cfg = GetCurrentConfig();
        if (cfg != null && cfg.levelIndex == 2 && level2_enablePeriodicX2Zones && level2_x2Routine == null)
            level2_x2Routine = StartCoroutine(Level2_PeriodicX2Zones(spawnImmediately: true));
    }

    public void PauseLevelTimer()  { timerPaused = true;  }
    public void ResumeLevelTimer() { timerPaused = false; }

    // ------------------------------ Timer control ------------------------------
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

    // ------------------------------ Level end ------------------------------
    private void GameOver(bool win)
    {
        if (isGameOver || phase == GamePhase.GameOver) return;

        isGameOver = true;
        StopLevelTimer();
        StopAllLoops();
        SetPhase(GamePhase.GameOver);

        if (timerText) timerText.gameObject.SetActive(false);
        if (levelTimerText) levelTimerText.color = win ? Color.green : Color.red;

        // ปิด UI เฉพาะเลเวล
        UIManager.Instance?.ShowGarbledUI(false);
        UIManager.Instance?.SetTriangleHintVisible(false);

        if (win && GetCurrentConfig()?.levelIndex == 2 && level2_grantWinRewards)
        {
            TryGrantLevel2Rewards(level2_winCogCoin, level2_nextFloorClue);
        }

        Debug.Log(win ? "🎉 ชนะทุกด่าน" : "💀 แพ้เพราะหมดเวลา");
    }

    private void StopAllLoops()
    {
        if (level1_garbleRoutine != null) { StopCoroutine(level1_garbleRoutine); level1_garbleRoutine = null; }

        if (level2_x2Routine != null)         { StopCoroutine(level2_x2Routine);         level2_x2Routine = null; }
        if (level2_benchIssueRoutine != null) { StopCoroutine(level2_benchIssueRoutine); level2_benchIssueRoutine = null; }
        Level2_RevertAllZones();

        // L3
        if (level3_conveyorRoutine != null) { StopCoroutine(level3_conveyorRoutine); level3_conveyorRoutine = null; }
        if (level3_lockRoutine     != null) { StopCoroutine(level3_lockRoutine);     level3_lockRoutine     = null; }
        if (level3_fieldRoutine    != null) { StopCoroutine(level3_fieldRoutine);    level3_fieldRoutine    = null; }
        if (level3_deleteRoutine   != null) { StopCoroutine(level3_deleteRoutine);   level3_deleteRoutine   = null; }
        L3_ClearZonesAndLocks();
    }

    private void AnnounceLevelComplete()
    {
        var cfg = GetCurrentConfig();
        Debug.Log($"✅ ผ่านด่าน {cfg?.levelIndex}");
    }

    private LevelConfig GetCurrentConfig()
    {
        if (levels == null || levels.Length == 0) return null;
        int idx = Mathf.Clamp(currentLevel, 0, levels.Length - 1);
        return levels[idx];
    }

    // >>> Public API ที่ TurnManager หรือไฟล์อื่นอาจเรียก <<<
    /// <summary>
    /// 1-based LevelConfig.levelIndex shown to players.
    /// </summary>
    public int GetCurrentLevelIndex()
    {
        var cfg = GetCurrentConfig();
        return cfg != null ? cfg.levelIndex : 0; // 1-based index ใน LevelConfig
    }

    private void SetPhase(GamePhase next) => phase = next;

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

    private void L3_UpdateBossUI()
    {
        if (bossHpText) bossHpText.text = $"Hydra HP: {Mathf.Max(0, level3_bossHP)}/{level3_bossMaxHP}";
    }

    // ==============================
    // Level 1: Garbled IT Obstacle
    // ==============================
    private IEnumerator Level1_GarbleLoop()
    {
        while (!isGameOver && GetCurrentConfig() != null && GetCurrentConfig().levelIndex == 1 && level1_enableGarbled)
        {
            // ถ้า suspended ให้รอจนถึงเวลา resume โดยไม่ kill คอรุตีน
            if (level1_garbleSuspended)
            {
                while (level1_garbleSuspended && !isGameOver && GetCurrentConfig()?.levelIndex == 1)
                {
                    if (Time.unscaledTime >= level1_garbleResumeTime) level1_garbleSuspended = false;
                    yield return null;
                }
                if (isGameOver || GetCurrentConfig()?.levelIndex != 1) break;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, level1_garbleTickSec));
            if (!level1_garbleSuspended)
                TryGarbledShuffle(level1_garbleClusterSize);
        }
        level1_garbleRoutine = null;
    }

    public bool Level1_SubmitFixGuess(string guess)
    {
        if (GetCurrentConfig()?.levelIndex != 1 || string.IsNullOrWhiteSpace(guess)) return false;
        string g = Normalize(guess);

        if (IsITWord(g))
        {
            level1_garbleSuspended = true;
            level1_garbleResumeTime = Time.unscaledTime + Mathf.Max(1f, level1_garbleSuspendDuration);
            UIManager.Instance?.ShowMessage($"✅ Fix: \"{guess}\" — หยุดสลับชั่วคราว", 2f);
            return true;
        }
        else
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.AddScore(-Mathf.Abs(level1_wrongGuessPenalty));

            UIManager.Instance?.ShowMessage($"❌ เดาผิด -{Mathf.Abs(level1_wrongGuessPenalty)}", 2f);
            TryGarbledShuffle(level1_garbleClusterSize + 2);
            return false;
        }
    }

    private void TryGarbledShuffle(int clusterSize)
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return;

        var candidates = new List<BoardSlot>();
        int rows = bm.rows, cols = bm.cols;

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var s = bm.grid[r, c];
            if (s == null) continue;
            var t = s.GetLetterTile();
            if (t == null) continue;
            if (t.isLocked) continue; // ไม่ยุ่งกับไทล์ที่ล็อกไปแล้ว
            candidates.Add(s);
        }
        if (candidates.Count < 2) return;

        int take = Mathf.Clamp(clusterSize, 2, candidates.Count);
        var picked = new List<BoardSlot>(take);
        for (int i = 0; i < take; i++)
        {
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            picked.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }

        var tiles = new List<LetterTile>(picked.Count);
        foreach (var s in picked) tiles.Add(s.RemoveLetter());

        if (tiles.Count >= 2)
        {
            var last = tiles[tiles.Count - 1];
            for (int i = tiles.Count - 1; i >= 1; i--) tiles[i] = tiles[i - 1];
            tiles[0] = last;
        }

        for (int i = 0; i < picked.Count; i++)
        {
            var slot = picked[i];
            var tile = tiles[i];
            if (slot == null || tile == null) continue;

            tile.transform.SetParent(slot.transform, false);
            var rt = tile.GetComponent<RectTransform>();
            if (rt != null) { rt.anchoredPosition = Vector2.zero; rt.localScale = Vector3.one; }
            else { tile.transform.localPosition = Vector3.zero; tile.transform.localScale = Vector3.one; }

            slot.Flash(new Color(1f, 1f, 0.6f, 1f), 1, 0.06f);
        }
    }

    // ==============================
    // Level 2: Objectives & Obstacles
    // ==============================
    private void Level2_ApplyThemeAndUpgrades()
    {
        if (level2_applyThemeOnStart)
            Debug.Log("[Level2] Apply theme: dark system with small black motes.");

        var prog = PlayerProgressSO.Instance?.data;
        if (prog != null)
        {
            // ตัวอย่าง: ใช้ค่าที่ผู้เล่นอัปเกรดไว้ (เช่น max mana)
            TurnManager.Instance?.UpgradeMaxMana(PlayerProgressSO.Instance.data.maxMana);
            Debug.Log("[Level2] Applied permanent upgrades from shop (e.g., max mana).");
        }
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
            if (s.IsLocked) continue; // อย่าล็อกซ้ำ
            all.Add(s);                // ✅ อนุญาตล็อกทั้งช่องว่างและช่องที่มีตัวอักษร
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
            slot.ApplyVisual();
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

        // เลือกจาก snapshot เพื่อหลีกเลี่ยงการแก้ dict ระหว่าง iterate
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
        while (!isGameOver && GetCurrentConfig() != null && GetCurrentConfig().levelIndex == 2)
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
        Debug.Log($"[Level2] Bench bug penalty -{p} for word: {main}");
    }

    // === Public APIs for TurnManager (Bench Issue) ===
    public bool Level2_IsBenchIssueActive() => level2_benchIssueActive;
    public int Level2_SelectZeroCount(int placedCount)
    {
        if (!level2_enableBenchIssue || !level2_benchIssueActive) return 0;
        if (placedCount <= 0) return 0;
        return Mathf.Clamp(level2_benchZeroPerMove, 0, placedCount);
    }

    // ---------- Triangle ----------
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

    // ---------- Periodic X2 ----------
    private IEnumerator Level2_PeriodicX2Zones(bool spawnImmediately = false)
    {
        if (spawnImmediately)
        {
            ApplyX2ZonesOnce(
                zones: Mathf.Max(1, level2_x2ZonesPerWave),
                duration: Mathf.Max(5f, level2_x2ZoneDurationSec)
            );
        }

        while (!isGameOver && GetCurrentConfig() != null && GetCurrentConfig().levelIndex == 2)
        {
            float wait = Mathf.Max(1f, level2_x2IntervalSec);
            Debug.Log($"[Level2] Waiting {wait} sec for next x2 wave…");
            yield return new WaitForSecondsRealtime(wait);

            ApplyX2ZonesOnce(
                zones: Mathf.Max(1, level2_x2ZonesPerWave),
                duration: Mathf.Max(5f, level2_x2ZoneDurationSec)
            );
        }
        level2_x2Routine = null;
    }

    private void ApplyX2ZonesOnce(int zones, float duration)
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null)
        {
            Debug.LogWarning("[Level2] ApplyX2ZonesOnce aborted: board not ready.");
            return;
        }
        if (bm.rows < 3 || bm.cols < 3)
        {
            Debug.LogWarning("[Level2] Board too small (<3x3) — skip x2 zones.");
            return;
        }

        Level2_RevertAllZones();

        int rows = bm.rows, cols = bm.cols;
        int attempts = 0, maxAttempts = 200;
        var chosenCenters = new List<Vector2Int>();

        while (chosenCenters.Count < zones && attempts++ < maxAttempts)
        {
            int r = UnityEngine.Random.Range(1, rows - 1);  // เลือกศูนย์กลางที่ไม่ชนขอบ (สำหรับ 3x3)
            int c = UnityEngine.Random.Range(1, cols - 1);

            // เว้นระยะห่างระหว่าง center
            bool tooClose = chosenCenters.Any(cc => Mathf.Abs(cc.x - r) + Mathf.Abs(cc.y - c) < 3);
            if (tooClose) continue;

            chosenCenters.Add(new Vector2Int(r, c));
        }

        if (chosenCenters.Count == 0)
        {
            Debug.LogWarning("[Level2] No valid centers for x2 zones.");
            return;
        }

        foreach (var center in chosenCenters)
        {
            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int rr = center.x + dr, cc = center.y + dc;
                if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) continue;

                var slot = bm.grid[rr, cc];
                if (slot == null) continue;

                level2_activeZoneChanges.Add((new Vector2Int(rr, cc), slot.type, slot.manaGain)); // NOTE: prevMana เก็บไว้เผื่อในอนาคต
                slot.type = level2_multiplierSlotType; // ปัจจุบันเปลี่ยนเฉพาะ type
                slot.ApplyVisual();
            }
        }

        Debug.Log($"[Level2] x2 Zones appeared at centers: {string.Join(", ", chosenCenters)} for {duration}s.");

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

            // ✅ เช็คขอบเขตแบบ component-wise
            if (v.x < 0 || v.x >= bm.rows || v.y < 0 || v.y >= bm.cols) continue;

            var s = bm.grid[v.x, v.y];
            if (s == null) continue;

            s.type = it.prevType;
            s.manaGain = it.prevMana; // แม้ตอนนี้จะไม่ได้แก้ manaGain ก็ restore ให้เคสในอนาคต
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
            if (data == null)
            {
                Debug.Log($"[Level2] Reward hook — clue: {clue} (no PlayerProgressSO.data)");
                return;
            }

            int add = Mathf.Max(0, addCogCoin);

            var t = data.GetType();
            var field = t.GetField("cogCoin") ?? t.GetField("CogCoin");
            if (field != null)
            {
                int cur = Convert.ToInt32(field.GetValue(data));
                field.SetValue(data, cur + add);
                Debug.Log($"[Level2] +{add} CogCoin via field. Clue: {clue}");
                return;
            }

            var prop = t.GetProperty("cogCoin") ?? t.GetProperty("CogCoin");
            if (prop != null && prop.CanRead && prop.CanWrite)
            {
                int cur = Convert.ToInt32(prop.GetValue(data, null));
                prop.SetValue(data, cur + add, null);
                Debug.Log($"[Level2] +{add} CogCoin via property. Clue: {clue}");
                return;
            }

            // ถ้าไม่มีจริง ๆ ก็ล็อกเฉย ๆ
            Debug.Log($"[Level2] (Reward hook) No cogCoin field/property found. Clue: {clue}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Level2] Reward hook exception: {ex.Message}");
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
        var cfg = GetCurrentConfig();
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
            AnnounceLevelComplete();
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
        UIManager.Instance?.SetTriangleHintVisible(true);
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
        while (!isGameOver && GetCurrentConfig()?.levelIndex == 3 && !level3_phaseChangeActive)
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
        while (!isGameOver && GetCurrentConfig()?.levelIndex == 3 && !level3_phaseChangeActive)
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
        while (!isGameOver && GetCurrentConfig()?.levelIndex == 3 && !level3_phaseChangeActive)
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
        while (!isGameOver && GetCurrentConfig()?.levelIndex == 3 && !level3_phaseChangeActive)
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

#if UNITY_EDITOR
    [ContextMenu("Validate Levels")]
    private void ValidateLevels()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogWarning("[LevelManager] levels is empty.");
            return;
        }

        var seen = new HashSet<int>();
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

        if (cfg != null && cfg.levelIndex == 1 && itProgressText)
            itProgressText.text = $"IT words: 0/{itWordsTargetLevel1}";

        Debug.Log("[LevelManager] ValidateLevels done.");
    }
#endif
}
