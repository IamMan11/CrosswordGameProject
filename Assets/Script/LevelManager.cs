// ========== LevelManager.cs (stable) ==========
// - เพิ่ม GetCurrentLevelIndex()
// - ใช้ reflection มอบ CogCoin ถ้ามีจริงใน PlayerProgressSO.data (ไม่พังถ้าไม่มี)
// - โซน x2 เริ่มทำงานทันทีที่เข้า Level 2 (spawnImmediately) และรันเป็นคาบ
// - กัน NPE หลายจุด, กัน start ซ้ำ, revert โซนอย่างปลอดภัย
// - ระบบ Garbled (Level 1) และ Bench Issue/Locked Board (Level 2) คงเดิม แต่ออกแบบกันพังมากขึ้น

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

    [Header("Configs")]
    public LevelConfig[] levels;

    [Header("UI (ผูกใน Inspector)")]
    public TMP_Text levelText;
    public TMP_Text timerText;      // legacy
    public TMP_Text levelTimerText; // ตัวจับเวลาเลเวล (ขึ้น/ลง)
    [Tooltip("Progress ของคำ IT สำหรับด่าน 1 (ไม่ผูกก็ได้)")]
    public TMP_Text itProgressText;

    public int CurrentLevel => currentLevel;

    private enum GamePhase { None, Setup, Ready, Running, Transition, GameOver }
    private GamePhase phase = GamePhase.None;
    private int currentLevel;
    private bool isGameOver = false;
    private bool isTransitioning = false;

    private float levelTimeLimit;
    private float levelTimeElapsed;
    private bool levelTimerRunning;
    bool timerStarted;
    bool timerPaused;
    [Header("Stage Clear UI")]
    public StageClearPanel stageClearPanel;
    public string shopSceneName = "Shop";     // ตั้งชื่อซีน Shop ใน Inspector

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

    // ----------------------------------------
    private static readonly WaitForEndOfFrame WaitEOF = new WaitForEndOfFrame();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SetPhase(GamePhase.None);
    }

    private void Start()
    {
        if (levels == null || levels.Length == 0) { Debug.LogError("No level configuration provided!"); return; }

        // ถ้ากลับมาจาก Shop และมีเลเวลถัดไปค้างอยู่ ให้เริ่มที่ค่านั้น
        int startIndex = 0;
        if (StageResultBus.HasPendingNextLevel)
        {
            startIndex = Mathf.Clamp(StageResultBus.NextLevelIndex, 0, levels.Length - 1);
            StageResultBus.ClearNextLevelFlag();
        }
        SetupLevel(startIndex);
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
                }
            }

            // Periodic x2 waves
            if (level2_enablePeriodicX2Zones && level2_x2Routine == null)
                level2_x2Routine = StartCoroutine(Level2_PeriodicX2Zones(spawnImmediately: true));

            // Bench issue
            if (level2_enableBenchIssue && level2_benchIssueRoutine == null)
                level2_benchIssueRoutine = StartCoroutine(Level2_BenchIssueLoop());
        }

        // ✅ เงื่อนไขผ่านด่าน
        if (CheckWinConditions(cfg) && !(TurnManager.Instance?.IsScoringAnimation ?? false))
        {
            ShowStageClearAndShop(cfg);
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
        if (CheckWinConditions(cfg) && !(TurnManager.Instance?.IsScoringAnimation ?? false))
        {
            ShowStageClearAndShop(cfg);
        }
    }
    private void ShowStageClearAndShop(LevelConfig cfg)
    {
        if (phase == GamePhase.Transition || phase == GamePhase.GameOver) return;
        SetPhase(GamePhase.Transition);

        StopAllLoops(); // หยุดคอร์รุตีน/โซนชั่วคราวของด่าน

        var result = BuildStageResult(cfg);

        // ตัวอย่าง: ย้ายเหรียญเข้าระบบคุณทันที (ถ้ามี Economy)
        // GameEconomy.Instance.AddCoins(result.totalCoins);

        stageClearPanel?.Show(result, next: () =>
        {
            // เก็บค่าที่ต้องใช้ใน Shop/ตอนกลับ
            StageResultBus.LastResult = result;
            StageResultBus.NextLevelIndex = Mathf.Clamp(currentLevel + 1, 0, levels.Length - 1);
            StageResultBus.GameplaySceneName = SceneManager.GetActiveScene().name;

            // ปลด timeScale กันค้างแล้วไป Shop
            Time.timeScale = 1f;
            SceneManager.LoadScene(shopSceneName);
        });
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
    private StageResult BuildStageResult(LevelConfig cfg)
    {
        var tm = TurnManager.Instance;
        int timeUsed = Mathf.FloorToInt(levelTimeElapsed);
        int wordsUnique = tm != null ? tm.UniqueWordsThisLevel : 0;
        int turns = tm != null ? tm.ConfirmsThisLevel : 0;
        int tilesLeft = TileBag.Instance != null ? TileBag.Instance.Remaining : 0;
        int moveScore = tm != null ? tm.Score : 0;

        // โบนัสคะแนน
        int timeLeft = Mathf.Max(0, cfg.targetTimeSec - timeUsed);
        int wordsScoreCount = Mathf.Min(wordsUnique, Mathf.Max(0, cfg.maxWordsForScore));
        int bonusScore =
            Mathf.Max(0, Mathf.RoundToInt(timeLeft * cfg.coefTimeScore)) +
            (wordsScoreCount * cfg.coefWordScore) +
            (turns * cfg.coefTurnScore) +
            (tilesLeft * cfg.coefTilesLeftScore);

        int totalScore = Mathf.Max(0, moveScore + bonusScore);

        // เหรียญ
        int wordsMoneyCount = Mathf.Min(wordsUnique, Mathf.Max(0, cfg.maxWordsForMoney));
        int bonusCoins =
            Mathf.Max(0, Mathf.RoundToInt(timeLeft * cfg.coefTimeMoney)) +
            (wordsMoneyCount * cfg.coefWordMoney) +
            (turns * cfg.coefTurnMoney) +
            (tilesLeft * cfg.coefTilesLeftMoney);

        int totalCoins = Mathf.Max(0, cfg.baseCoins + bonusCoins);

        return new StageResult {
            levelIndex  = cfg.levelIndex,
            timeUsedSec = timeUsed,
            words       = wordsUnique,
            turns       = turns,
            tilesLeft   = tilesLeft,
            moveScore   = moveScore,
            bonusScore  = bonusScore,
            totalScore  = totalScore,
            baseCoins   = cfg.baseCoins,
            bonusCoins  = bonusCoins,
            totalCoins  = totalCoins
        };
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

        // Timer setup
        levelTimeElapsed = 0f;
        levelTimeLimit   = Mathf.Max(0f, cfg.timeLimit);
        levelTimerRunning = false;
        timerStarted = false;
        timerPaused  = false;
        UpdateLevelTimerText(levelTimeLimit > 0 ? levelTimeLimit : 0f);

        // ด่าน 1 reset
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

        // ด่าน 2 reset
        level2_triangleComplete = false;
        level2_triangleCheckTimer = 0f;
        Level2_RevertAllZones();
        level2_lockedSlots.Clear();
        if (level2_benchIssueRoutine != null) { StopCoroutine(level2_benchIssueRoutine); level2_benchIssueRoutine = null; }
        level2_benchIssueActive = false;
        level2_benchIssueEndTime = 0f;
        level2_lastPenalizedWord = "";

        // Prepare board & turn
        if (BoardManager.Instance != null) BoardManager.Instance.GenerateBoard();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ResetForNewLevel();
            if (TileBag.Instance != null) TileBag.Instance.RefillTileBag();
            TurnManager.Instance.UpdateBagUI();
            if (BenchManager.Instance != null) BenchManager.Instance.RefillEmptySlots();
            TurnManager.Instance.UpdateBagUI();
        }

        // ด่าน 2: theme / locked seeds / x2 wave start
        if (cfg.levelIndex == 2)
        {
            Level2_ApplyThemeAndUpgrades();
            if (level2_enableLockedBoard) Level2_SeedLockedSlots();

            // เริ่มโซน x2 ทันที (กันลืม OnFirstConfirm)
            if (level2_enablePeriodicX2Zones && level2_x2Routine == null)
                level2_x2Routine = StartCoroutine(Level2_PeriodicX2Zones(spawnImmediately: true));
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

        if (win && GetCurrentConfig()?.levelIndex == 2 && level2_grantWinRewards)
        {
            TryGrantLevel2Rewards(level2_winCogCoin, level2_nextFloorClue);
        }

        Debug.Log(win ? "🎉 ชนะทุกด่าน" : "💀 แพ้เพราะหมดเวลา");
    }

    private void StopAllLoops()
    {
        if (level1_garbleRoutine != null) { StopCoroutine(level1_garbleRoutine); level1_garbleRoutine = null; }

        if (level2_x2Routine != null)      { StopCoroutine(level2_x2Routine);      level2_x2Routine = null; }
        if (level2_benchIssueRoutine != null) { StopCoroutine(level2_benchIssueRoutine); level2_benchIssueRoutine = null; }
        Level2_RevertAllZones();
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

    // >>> Public API ที่ TurnManager เคยเรียกหา <<<
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

    // ==============================
    // Level 1: Garbled IT Obstacle
    // ==============================
    private IEnumerator Level1_GarbleLoop()
    {
        while (!isGameOver && GetCurrentConfig() != null && GetCurrentConfig().levelIndex == 1 && level1_enableGarbled)
        {
            if (level1_garbleSuspended) break;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, level1_garbleTickSec));
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

                level2_activeZoneChanges.Add((new Vector2Int(rr, cc), slot.type, slot.manaGain));
                slot.type = level2_multiplierSlotType;
                slot.ApplyVisual();
            }
        }

        Debug.Log($"[Level2] x2 Zones appeared at centers: {string.Join(", ", chosenCenters)} for {duration}s.");

        if (level2_activeZoneChanges.Count > 0)
        {
            UIManager.Instance?.ShowMessage("x2 Zones appeared!", 2f);
            StartCoroutine(Level2_RevertZonesAfter(duration));
            Debug.LogWarning("เริ่ม โซน x2");
        }
    }

    private IEnumerator Level2_RevertZonesAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        Level2_RevertAllZones();
        UIManager.Instance?.ShowMessage("x2 Zones ended", 1.5f);
        Debug.LogWarning("จบ โซน x2");
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
