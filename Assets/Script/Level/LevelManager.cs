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
    public enum GamePhase { None, Setup, Ready, Running, Transition, GameOver }
    public GamePhase phase = GamePhase.None;

    public int currentLevel;
    public bool isGameOver;

    public float levelTimeLimit;
    public float levelTimeElapsed;
    public bool levelTimerRunning;
    public bool timerStarted, timerPaused;
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
    [Header("Hooks")]
    [SerializeField] private Level2Controller level2;   // drag จาก Hierarchy
    private bool level2_triangleComplete;
    private float level2_triangleCheckTimer;
    private int level2_triangleLinkedCount = 0; // 0..3 ที่ "เชื่อมถึงกัน" ณ ตอนนี้
    private readonly List<BoardSlot> level2_lockedSegmentSlots = new();
    // =============== Level 3 – The Black Hydra (Boss) ===============
    [SerializeField] private Level3Controller level3;

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
    public void UpdateLevelTimerText(float seconds)
    {
        var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int mm = total / 60, ss = total % 60;
        if (levelTimerText) levelTimerText.text = $"{mm:00}:{ss:00}";
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

        if (currentLevelConfig?.levelIndex == 2 && Level2Controller.Instance?.L2_useTriangleObjective == true)
        {
            int prev = level2_triangleLinkedCount;
            level2_triangleLinkedCount = Level2Controller.Instance.GetTouchedNodeCount();
            level2_triangleComplete = Level2Controller.Instance.IsTriangleComplete();
            if (prev != level2_triangleLinkedCount) LevelTaskUI.I?.Refresh();
        }
                // ===== Level 3 tick =====
        if (currentLevelConfig?.levelIndex == 3 && level3 != null)
            level3.Tick(Time.unscaledDeltaTime);

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
            Level2Controller.Instance?.TryUnlockByWordLength();
            Level2Controller.Instance?.TryApplyBenchPenalty();
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
            Level2Controller.Instance?.Setup();
        }

        // เรียก Garbled ครั้งเดียวพอ
        Level1GarbledIT.Instance?.ClearAll();
        Level1GarbledIT.Instance?.Setup(currentLevelConfig);

        // ด่าน 2: seed/ธีม/เริ่มโซน x2
        if (currentLevelConfig?.levelIndex == 2)
        {
            Level2Controller.Instance?.Setup();   // ← สร้าง Triangle ก่อน
        }
        // ด่าน 3: Boss
        if (currentLevelConfig?.levelIndex == 3)
        {
            level3?.Setup();
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
            return !(Level2Controller.Instance?.L2_useTriangleObjective ?? false)
                    || (Level2Controller.Instance?.IsTriangleComplete() ?? false);
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


    private bool CheckWinConditions(LevelConfig cfg)
    {
        if (isGameOver || TurnManager.Instance == null || cfg == null) return false;

        int score = TurnManager.Instance.Score;
        int words = TurnManager.Instance.UniqueWordsThisLevel; // <-- เปลี่ยนมาใช้ตัวนี้

        bool baseOK = score >= cfg.requiredScore && words >= cfg.requiredWords;
        if (!baseOK) return false;

        if (cfg.levelIndex == 1 && itWordsFound.Count < itWordsTargetLevel1) return false;

        if (cfg.levelIndex == 2 &&
            (Level2Controller.Instance?.L2_useTriangleObjective ?? false) &&
            !(Level2Controller.Instance?.IsTriangleComplete() ?? false))
            return false;

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
    public void GameOver(bool win)
    {
        if (isGameOver || phase == GamePhase.GameOver) return;

        isGameOver = true;
        StopLevelTimer();
        StopAllLoops();
        phase = GamePhase.GameOver;

        if (levelTimerText) levelTimerText.color = win ? Color.green : Color.red;

        if (!win) { TriggerStageFail("GameOver(false)"); return; }

        if (win && currentLevelConfig?.levelIndex == 2)
        {
            var l2 = Level2Controller.Instance;
            if (l2 != null && l2.L2_grantWinRewards)
            {
                if (l2.L2_winCogCoin > 0)
                    CurrencyManager.Instance?.Add(l2.L2_winCogCoin);

                if (!string.IsNullOrWhiteSpace(l2.L2_nextFloorClue))
                    UIManager.Instance?.ShowFloatingToast($"Clue: {l2.L2_nextFloorClue}", Color.cyan, 1.6f);
            }
        }

        Debug.Log("🎉 ชนะทุกด่าน");
    }

    private void StopAllLoops()
    {
        // ให้คอนโทรลเลอร์ด่าน 2 เก็บกวาดเอง
        Level2Controller.Instance?.StopAllEffects();
        // (ถ้าคุณยังมีตัวแปรรูทีนเก่าอยู่ จะลบทิ้งได้เลย)
        level3?.StopAllLoops();
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
        if (currentLevelConfig?.levelIndex == 3)
            level3?.OnPlayerDealtWord(placedCount, placedLettersDamageSum, mainWordLen, placedCoords);
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
        var panel = StageFailPanel.Instance
        #if UNITY_2023_1_OR_NEWER
            ?? UnityEngine.Object.FindFirstObjectByType<StageFailPanel>(FindObjectsInactive.Include);
        #else
            ?? FindObjectOfType<StageFailPanel>(true);
        #endif
        if (panel) panel.Show("Stage Fail", "กลับสู่เมนูหลัก?");
        else
        {
            Debug.LogError("StageFailPanel not found under Main Canvas.");
            PauseManager.I?.Btn_ReturnToMainMenu();
        }
    }
}
