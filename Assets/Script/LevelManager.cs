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

    private int   currentLevel;
    private bool  isGameOver;

    private float levelTimeLimit;
    private float levelTimeElapsed;
    private bool  levelTimerRunning;
    private bool  timerStarted, timerPaused;

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
    public bool level2_useTriangleObjective = true;
    public Vector2Int[] level2_triangleTargets = new Vector2Int[] {
        new Vector2Int(2,2),
        new Vector2Int(2,12),
        new Vector2Int(12,7)
    };
    public float level2_triangleCheckPeriod = 0.5f;
    private bool  level2_triangleComplete;
    private float level2_triangleCheckTimer;

    [Header("Level 2 – Periodic X2 Zones (3x3)")]
    public bool  level2_enablePeriodicX2Zones = true;
    public float level2_x2IntervalSec = 180f;
    public int   level2_x2ZonesPerWave = 2;
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

    [Header("Level 2 – Bench Issue")]
    public bool  level2_enableBenchIssue = true;
    public float level2_benchIssueIntervalSec = 60f;
    public float level2_benchIssueDurationSec = 20f;
    public int   level2_benchZeroPerMove = 2;
    public int   level2_benchPenaltyPerMove = 0;
    private bool   level2_benchIssueActive;
    private float  level2_benchIssueEndTime;
    private Coroutine level2_benchIssueRoutine;
    private string level2_lastPenalizedWord = "";

    [Header("Level 2 – Theme & Rewards")]
    public bool   level2_applyThemeOnStart = true;
    public bool   level2_grantWinRewards  = true;
    public int    level2_winCogCoin       = 1;
    public string level2_nextFloorClue    = "เลขชั้นถัดไป";

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

    private void OnDisable() => StopAllLoops();

    public bool IsGameOver() => isGameOver;
    public int  GetCurrentLevelIndex() => currentLevelConfig != null ? currentLevelConfig.levelIndex : 0;

    private void UpdateLevelTimerText(float seconds)
    {
        var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int mm = total / 60, ss = total % 60;
        if (levelTimerText) levelTimerText.text = $"{mm:00}:{ss:00}";
    }

    private void Update()
    {
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
                if (remaining <= 0f)
                {
                    StopLevelTimer();
                    GameOver(false);
                    return;
                }
            }
            else
            {
                UpdateLevelTimerText(levelTimeElapsed); // ใช้เป็น stopwatch
            }
        }

        // ----- Level 2 ticks -----
        if (cfg.levelIndex == 2)
        {
            if (level2_useTriangleObjective && level2_triangleTargets != null && level2_triangleTargets.Length >= 3)
            {
                level2_triangleCheckTimer += Time.unscaledDeltaTime;
                if (level2_triangleCheckTimer >= level2_triangleCheckPeriod)
                {
                    level2_triangleCheckTimer = 0f;
                    level2_triangleComplete = CheckTriangleComplete();
                    // >>> อัปเดต UI Indicator ทุกครั้งที่เช็ก
                }
            }

            if (level2_enablePeriodicX2Zones && level2_x2Routine == null)
                level2_x2Routine = StartCoroutine(Level2_PeriodicX2Zones(spawnImmediately: true));

            if (level2_enableBenchIssue && level2_benchIssueRoutine == null)
                level2_benchIssueRoutine = StartCoroutine(Level2_BenchIssueLoop());
        }

        // ----- Win condition -----
        if (CheckWinConditions(cfg) && !(TurnManager.Instance?.IsScoringAnimation ?? false))
            ShowStageClearAndShop(cfg);
    }

    // ------------------------------ Public hooks ------------------------------
    public void OnScoreOrWordProgressChanged()
    {
        if (phase != GamePhase.Running) return;

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
    }

    public void OnFirstConfirm()
    {
        if (phase != GamePhase.Ready) return;

        if (levelTimeLimit > 0f) StartLevelTimer();
        if (!timerStarted) { timerStarted = true; timerPaused = false; }
        phase = GamePhase.Running;

        // เริ่ม wave x2 ของด่าน 2 ถ้ายังไม่เริ่ม
        var cfg = currentLevelConfig;
        if (cfg != null && cfg.levelIndex == 2 && level2_enablePeriodicX2Zones && level2_x2Routine == null)
            level2_x2Routine = StartCoroutine(Level2_PeriodicX2Zones(spawnImmediately: true));
    }

    public void PauseLevelTimer()  { timerPaused = true;  }
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
        isGameOver       = false;
        phase            = GamePhase.Setup;

        currentLevel       = idx;
        currentLevelConfig = levels[currentLevel];

        // UI
        if (levelText) levelText.text = $"Level {currentLevelConfig.levelIndex}";
        levelTimeElapsed = 0f;
        levelTimeLimit   = Mathf.Max(0f, currentLevelConfig.timeLimit);
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

        // เตรียมบอร์ด/เบนช์
        BoardManager.Instance?.GenerateBoard();
        TurnManager.Instance?.ResetForNewLevel();
        TileBag.Instance?.RefillTileBag();
        BenchManager.Instance?.RefillEmptySlots();
        TurnManager.Instance?.UpdateBagUI();

        // เรียก Garbled ครั้งเดียวพอ
        Level1GarbledIT.Instance?.ClearAll();
        Level1GarbledIT.Instance?.Setup(currentLevelConfig);

        // ด่าน 2: seed/ธีม/เริ่มโซน x2
        if (currentLevelConfig.levelIndex == 2)
        {
            Level2_ApplyThemeAndUpgrades();
            if (level2_enableLockedBoard) Level2_SeedLockedSlots();

            // เริ่มโซน x2 ทันที (กันลืม OnFirstConfirm) และอัปเดต triangle hint ครั้งแรก

            if (level2_enablePeriodicX2Zones && level2_x2Routine == null)
                level2_x2Routine = StartCoroutine(Level2_PeriodicX2Zones(spawnImmediately: true));

        }

        Debug.Log($"▶ เริ่มด่าน {currentLevelConfig.levelIndex} | Time: {currentLevelConfig.timeLimit}s | Score target: {currentLevelConfig.requiredScore}");
        phase = GamePhase.Ready;
    }

    private void ShowStageClearAndShop(LevelConfig cfg)
    {
        if (phase == GamePhase.Transition || phase == GamePhase.GameOver) return;
        phase = GamePhase.Transition;

        StopAllLoops();

        var result = BuildStageResult(cfg);
        stageClearPanel?.Show(result, next: () =>
        {
            StageResultBus.LastResult       = result;
            StageResultBus.NextLevelIndex   = Mathf.Clamp(currentLevel + 1, 0, levels.Length - 1);
            StageResultBus.GameplaySceneName = SceneManager.GetActiveScene().name;

            Time.timeScale = 1f;
            SceneManager.LoadScene(shopSceneName);
        });
    }

    private bool CheckWinConditions(LevelConfig cfg)
    {
        if (isGameOver || TurnManager.Instance == null || cfg == null) return false;

        bool baseOK =
            TurnManager.Instance.Score >= cfg.requiredScore &&
            TurnManager.Instance.CheckedWordCount >= cfg.requiredWords;

        if (!baseOK) return false;

        if (cfg.levelIndex == 1 && itWordsFound.Count < itWordsTargetLevel1) return false;
        if (cfg.levelIndex == 2 && level2_useTriangleObjective && !level2_triangleComplete) return false;

        return true;
    }

    private StageResult BuildStageResult(LevelConfig cfg)
    {
        var tm = TurnManager.Instance;
        int timeUsed  = Mathf.FloorToInt(levelTimeElapsed);
        int words     = tm ? tm.UniqueWordsThisLevel : 0;
        int turns     = tm ? tm.ConfirmsThisLevel    : 0;
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

        return new StageResult {
            levelIndex  = cfg.levelIndex,
            timeUsedSec = timeUsed,
            words       = words,
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

    // ------------------------------ Timer helpers ------------------------------
    private void StartLevelTimer() { if (!levelTimerRunning) { levelTimerRunning = true; levelTimeElapsed = 0f; } }
    private void StopLevelTimer()  { if (levelTimerRunning)  { levelTimerRunning = false; } }

    // ------------------------------ Game over / stop loops ------------------------------
    private void GameOver(bool win)
    {
        if (isGameOver || phase == GamePhase.GameOver) return;

        isGameOver = true;
        StopLevelTimer();
        StopAllLoops();
        phase = GamePhase.GameOver;

        if (levelTimerText) levelTimerText.color = win ? Color.green : Color.red;


        if (win && currentLevelConfig?.levelIndex == 2 && level2_grantWinRewards)
            TryGrantLevel2Rewards(level2_winCogCoin, level2_nextFloorClue);

        Debug.Log(win ? "🎉 ชนะทุกด่าน" : "💀 แพ้เพราะหมดเวลา");
    }

    private void StopAllLoops()
    {

        if (level2_x2Routine != null)          { StopCoroutine(level2_x2Routine);          level2_x2Routine = null; }
        if (level2_benchIssueRoutine != null)  { StopCoroutine(level2_benchIssueRoutine);  level2_benchIssueRoutine = null; }
        Level2_RevertAllZones();
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
    public int  Level2_SelectZeroCount(int placedCount)
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
}
