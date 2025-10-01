// ========================== LevelManager.cs ==========================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Level 1 – Word Request")]
    public int wordRequestTargetLevel1 = 5;  // ตั้ง > 0 ถ้ามีเป้าหมาย
    private int wordRequestsDone = 0;
    
    [Header("Configs")]
    public LevelConfig[] levels;
    [HideInInspector] public LevelConfig currentLevelConfig;
    
    [Header("UI")]
    public TMP_Text levelText;
    public TMP_Text levelTimerText;
    [Tooltip("Progress คำ IT ของด่าน 1 (ไม่ผูกก็ได้)")]
    public TMP_Text itProgressText;
    
    public int CurrentLevel => currentLevel;

    private enum GamePhase { None, Setup, Ready, Running, Transition, GameOver }
    private GamePhase phase = GamePhase.None;

    private int currentLevel;
    private bool isGameOver;

    private float levelTimeLimit;
    private float levelTimeElapsed;
    private bool levelTimerRunning;
    private bool timerStarted, timerPaused;
    [SerializeField] float panicTimeThresholdSec = 180f; // 3 นาที
    bool prepareFailActive = false;
    bool pendingPrepareFailCheck = false;
    bool panicBgmActive = false;

    private Color _timerDefaultColor = Color.white;

    // Level 1 - IT Words
    public string[] itKeywordsLevel1 = new string[] {
        "it","code","bug","dev","server","client","api","database","db","sql",
        "data","cloud","ai","ml","python","java","c#","csharp","unity","scene",
        "asset","compile","build","network","socket","array","stack","cache","login","token"
    };
    private readonly HashSet<string> itWordsFound = new HashSet<string>();

    // Level 2 - Triangle Objective
    [Header("Level 2 – Triangle Objective")]
    public Vector2Int[] level2_triangleTargets = new Vector2Int[] {
        new Vector2Int(2,2),
        new Vector2Int(2,12),
        new Vector2Int(12,7)
    };
    private bool level2_triangleComplete;
    private float level2_triangleCheckTimer;
    private int level2_triangleLinkedCount = 0;

    [Header("Level 2 – Periodic X2 Zones (3x3)")]
    private Coroutine level2_x2Routine;
    private readonly List<(Vector2Int pos, SlotType prevType, int prevMana)> level2_activeZoneChanges
        = new List<(Vector2Int, SlotType, int)>(); 

    // Level 3 - Boss
    [Header("Level 3 – Black Hydra (Boss)")]
    public bool  level3_enableBoss = true;
    public int level3_bossMaxHP = 1200;
    [Range(0f, 2f)] public float level3_criticalBonus = 0.5f;
    public float level3_conveyorIntervalSec = 120f;
    public int level3_conveyorShift = 1;

    [Header("L3 – Lock Board Wave")]
    public float level3_lockWaveIntervalSec = 90f;
    public int level3_lockCountPerWave = 6;
    public float level3_lockDurationSec = 25f;

    private int level3_bossHP;
    private Coroutine level3_conveyorRoutine;
    private Coroutine level3_lockRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (levelTimerText) _timerDefaultColor = levelTimerText.color; 
        SetPhase(GamePhase.None);
    }

    private void Start()
    {
        if (levels == null || levels.Length == 0) { Debug.LogError("No level configuration provided!"); return; }

        int startIndex = 0;
        SetupLevel(startIndex);
    }

    private void SetupLevel(int idx)
    {
        idx = Mathf.Clamp(idx, 0, levels.Length - 1);
        StopAllLoops();
        isGameOver = false;
        phase = GamePhase.Setup;

        currentLevel = idx;
        var cfg = levels[currentLevel];

        if (levelText) levelText.text = $"Level {cfg.levelIndex}";
        if (levelTimerText) levelTimerText.color = _timerDefaultColor; 

        // Timer setup
        levelTimeElapsed = 0f;
        levelTimeLimit = Mathf.Max(0f, currentLevelConfig.timeLimit);
        levelTimerRunning = false;
        timerStarted = timerPaused = false;
        UpdateLevelTimerText(levelTimeLimit > 0 ? levelTimeLimit : 0f);

        // Reset IT words progress for Level 1
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

        // Level 1 setup
        if (currentLevelConfig.levelIndex == 1)
        {
            Level1GarbledIT.Instance?.ClearAll();
            Level1GarbledIT.Instance?.Setup(currentLevelConfig);
        }

        // Level 2 setup
        if (currentLevelConfig.levelIndex == 2)
        {
            Level2Controller.Instance?.Setup();
            Level2_SpawnLockedSegments();
        }

        // Boss setup (Level 3)
        if (cfg.levelIndex == 3)
        {
            level3_bossHP = Mathf.Max(1, level3_bossMaxHP);
            if (level3_enableBoss)
            {
                if (level3_conveyorRoutine == null) level3_conveyorRoutine = StartCoroutine(L3_ConveyorLoop());
                if (level3_lockRoutine == null) level3_lockRoutine = StartCoroutine(L3_LockWaveLoop());
            }
        }

        SetPhase(GamePhase.Ready);
    }

    private void UpdateLevelTimerText(float seconds)
    {
        var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int mm = total / 60, ss = total % 60;
        if (levelTimerText) levelTimerText.text = $"{mm:00}:{ss:00}";
    }

    private void Update()
    {
        if (PauseManager.IsPaused) return;
        if (phase != GamePhase.Running || levels == null || levels.Length == 0) return;

        var cfg = currentLevelConfig;
        if (cfg == null) return;

        // Timer management
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
                    TriggerStageFail("Time up");
                    return;
                }
            }
        }

        // Level 2 ticks
        if (cfg.levelIndex == 2)
            Level2Controller.Instance?.Tick(Time.unscaledDeltaTime);

        if (CheckWinConditions(cfg))
        {
            AnnounceLevelComplete();
            _ = StartCoroutine(GoToNextLevel());
        }
    }

    private void AnnounceLevelComplete()
    {
        var cfg = GetCurrentConfig();
        Debug.Log($"✅ ผ่านด่าน {cfg?.levelIndex}");
    }

    private bool CheckWinConditions(LevelConfig cfg)
    {
        if (isGameOver || TurnManager.Instance == null || cfg == null) return false;

        int score = TurnManager.Instance.Score;
        int words = TurnManager.Instance.UniqueWordsThisLevel;

        bool baseOK = score >= cfg.requiredScore && words >= cfg.requiredWords;
        if (!baseOK) return false;

        if (cfg.levelIndex == 1 && itWordsFound.Count < itWordsTargetLevel1) return false;

        if (cfg.levelIndex == 2 && !(Level2Controller.Instance?.IsTriangleComplete() ?? false)) return false;

        return true;
    }

    private void StopLevelTimer() 
    {
        if (levelTimerRunning) 
        {
            levelTimerRunning = false; 
        }
    }

    private void SetPhase(GamePhase next) => phase = next;
}
// ============================ LevelManager.cs (ต่อ) ============================

private void Level2_SpawnLockedSegments()
{
    // Set up locked segments for level 2
    if (!level2_enableLockedSegments) return;

    var bm = BoardManager.Instance;
    if (bm == null) return;

    level2_lockedSlots.Clear();

    for (int i = 0; i < level2_lockedCount; i++)
    {
        var slot = bm.GetRandomSlot();
        if (slot == null) continue;
        int length = UnityEngine.Random.Range(level2_requiredLenRange.x, level2_requiredLenRange.y);
        level2_lockedSlots.Add(slot, length);
        slot.Lock(length);
        slot.ApplyVisual();
    }
}

private void Level2_ClearLockedSegments()
{
    // Clear locked segments in level 2
    if (level2_lockedSlots.Count > 0)
    {
        foreach (var slot in level2_lockedSlots)
        {
            slot.Key.Unlock();
            slot.Key.ApplyVisual();
        }
        level2_lockedSlots.Clear();
    }
}

private void L3_UpdateBossUI()
{
    if (bossHpText)
    {
        bossHpText.text = $"Hydra HP: {Mathf.Max(0, level3_bossHP)}/{level3_bossMaxHP}";
    }
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
                var s = bm.grid[r, c];
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
            slot.Flash(new Color(0.7f, 0.7f, 1f, 1f), 1, 0.06f);
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
                var s = bm.grid[r, c];
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
            var buff = new L3Zone { rect = L3_RandomRectInBoard(bm, level3_zoneSize), isBuff = true, end = Time.unscaledTime + level3_fieldEffectDurationSec };
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
    // NOTE: RectInt(x=row, y=col, width=heightInRows, height=widthInCols)
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

        // ลบตัวอักษรบนบอร์ดหรือจากเบนช์ หรือการ์ด
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
            var s = bm.grid[r, c];
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

private void TriggerStageFail(string reason)
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
// =============================== LevelManager.cs - Additional Code ================================

private void SetPanicBgm(float remainingTime)
{
    if (!panicBgmActive && remainingTime <= panicTimeThresholdSec)
    {
        panicBgmActive = true;
        BgmPlayer.Instance?.PlayPanicBGM(); // Assuming you have a BgmPlayer instance to manage panic BGM
    }
    else if (panicBgmActive && remainingTime > panicTimeThresholdSec)
    {
        panicBgmActive = false;
        BgmPlayer.Instance?.StopPanicBGM();
    }
}

private void UpdateLevelText()
{
    var cfg = currentLevelConfig;
    if (cfg == null) return;

    if (levelText) levelText.text = $"Level {cfg.levelIndex}";
}

private void SetupUIForLevel(int levelIndex)
{
    var cfg = levels[levelIndex];
    if (cfg == null) return;

    if (levelText)
    {
        levelText.text = $"Level {cfg.levelIndex}";
    }
    if (levelTimerText)
    {
        levelTimerText.color = _timerDefaultColor;
    }
    if (itProgressText && cfg.levelIndex == 1)
    {
        itProgressText.text = $"IT words: 0/{itWordsTargetLevel1}";
        itProgressText.gameObject.SetActive(true);
    }
    else
    {
        itProgressText.gameObject.SetActive(false);
    }

    // Update or reset level settings (timer, score requirements, etc.)
    levelTimeElapsed = 0f;
    levelTimeLimit = Mathf.Max(0f, currentLevelConfig.timeLimit);
    levelTimerRunning = false;
    UpdateLevelTimerText(levelTimeLimit);
}

private void UpdateLevelTimerText(float timeRemaining)
{
    if (levelTimerText != null)
    {
        int minutes = Mathf.FloorToInt(timeRemaining / 60);
        int seconds = Mathf.FloorToInt(timeRemaining % 60);
        levelTimerText.text = $"{minutes:00}:{seconds:00}";
    }
}

private void SetGameOver()
{
    if (isGameOver) return;

    isGameOver = true;
    StopLevelTimer();
    phase = GamePhase.GameOver;

    // Show game over UI and transition to next screen
    var panel = GameOverPanel.Instance;
    if (panel != null)
    {
        panel.Show("Game Over", "Do you want to retry?");
    }
}

private IEnumerator GoToNextLevel()
{
    yield return new WaitForSeconds(1.5f); // Small delay before transitioning
    if (currentLevel + 1 < levels.Length)
    {
        SetupLevel(currentLevel + 1);
    }
    else
    {
        ShowGameEndPanel();
    }
}

private void ShowGameEndPanel()
{
    var endPanel = GameEndPanel.Instance;
    if (endPanel != null)
    {
        endPanel.Show("You Win!", "Thanks for playing!");
    }
    else
    {
        Debug.LogError("GameEndPanel not found.");
    }
}

private void SetupLevel(int levelIndex)
{
    currentLevel = levelIndex;
    SetPhase(GamePhase.Setup);

    var cfg = levels[currentLevel];
    SetupUIForLevel(currentLevel);

    // Board and game object setups
    BoardManager.Instance?.GenerateBoard();
    TurnManager.Instance?.ResetForNewLevel();
    TileBag.Instance?.RefillTileBag();
    BenchManager.Instance?.RefillEmptySlots();

    // Level-specific setups
    if (cfg.levelIndex == 1) SetupLevel1();
    if (cfg.levelIndex == 2) SetupLevel2();
    if (cfg.levelIndex == 3) SetupLevel3();
}

private void SetupLevel1()
{
    Level1GarbledIT.Instance?.ClearAll();
    Level1GarbledIT.Instance?.Setup(currentLevelConfig);

    // IT word setup for level 1
    itWordsFound.Clear();
    wordRequestsDone = 0;

    if (itProgressText != null)
    {
        itProgressText.text = $"IT words: {itWordsFound.Count}/{itWordsTargetLevel1}";
    }
}

private void SetupLevel2()
{
    Level2Controller.Instance?.Setup();
    Level2_SpawnLockedSegments();
}

private void SetupLevel3()
{
    level3_bossHP = Mathf.Max(1, level3_bossMaxHP);
    level3_phaseChangeActive = false;
    level3_phaseTriggered = false;
    level3_sprintTriggered = false;

    L3_ClearZonesAndLocks();
    L3_UpdateBossUI();

    if (level3_enableBoss)
    {
        if (level3_conveyorRoutine == null) level3_conveyorRoutine = StartCoroutine(L3_ConveyorLoop());
        if (level3_lockRoutine == null) level3_lockRoutine = StartCoroutine(L3_LockWaveLoop());
        if (level3_fieldRoutine == null) level3_fieldRoutine = StartCoroutine(L3_FieldEffectsLoop());
        if (level3_deleteRoutine == null) level3_deleteRoutine = StartCoroutine(L3_DeleteActionLoop());
    }
}

private void SetPhase(GamePhase nextPhase)
{
    phase = nextPhase;
    if (nextPhase == GamePhase.Running)
    {
        StartLevelTimer();
    }
    else if (nextPhase == GamePhase.GameOver)
    {
        SetGameOver();
    }
}

private void StartLevelTimer()
{
    if (levelTimeLimit > 0f && !levelTimerRunning)
    {
        levelTimerRunning = true;
        timerStarted = true;
    }
}

private void StopLevelTimer()
{
    if (levelTimerRunning)
    {
        levelTimerRunning = false;
    }
}
// ============================== Level 3 – Black Hydra (Boss) ==============================

// This method is for updating the boss HP UI during the boss fight.
private void L3_UpdateBossUI()
{
    if (bossHpText != null)
    {
        bossHpText.text = $"Hydra HP: {Mathf.Max(0, level3_bossHP)}/{level3_bossMaxHP}";
    }
}

// This method handles the "Conveyor Shuffle" mechanic where letters in the board are shuffled.
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
                var s = bm.grid[r, c];
                if (s != null && s.HasLetterTile())
                {
                    var t = s.GetLetterTile();
                    if (t != null && !t.isLocked)
                    {
                        slots.Add(s);
                    }
                }
            }

        if (slots.Count < 2) continue;

        var tiles = new List<LetterTile>(slots.Count);
        foreach (var s in slots)
        {
            var tile = s.RemoveLetter();
            if (tile != null)
            {
                tiles.Add(tile);
            }
        }

        // Perform the shuffle
        int shift = Mathf.Max(1, level3_conveyorShift) % tiles.Count;
        if (shift > 0)
        {
            var rotated = new List<LetterTile>(tiles.Count);
            for (int i = 0; i < tiles.Count; i++)
            {
                rotated.Add(tiles[(i - shift + tiles.Count) % tiles.Count]);
            }
            tiles = rotated;
        }

        // Place shuffled tiles back into their slots
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var tile = tiles[i];
            if (slot != null && tile != null)
            {
                tile.transform.SetParent(slot.transform, false);
                var rt = tile.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale = Vector3.one;
                }
                else
                {
                    tile.transform.localPosition = Vector3.zero;
                    tile.transform.localScale = Vector3.one;
                }
                slot.Flash(new Color(0.7f, 0.7f, 1f, 1f), 1, 0.06f);
            }
        }
        UIManager.Instance?.ShowMessage("Conveyor Shuffle!", 1.5f);
    }
    level3_conveyorRoutine = null;
}

// This method handles locking the board at random intervals during the boss fight.
private IEnumerator L3_LockWaveLoop()
{
    while (!isGameOver && currentLevelConfig?.levelIndex == 3 && !level3_phaseChangeActive)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(10f, level3_lockWaveIntervalSec));
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) continue;

        // Lock random slots
        var all = new List<BoardSlot>();
        for (int r = 0; r < bm.rows; r++)
            for (int c = 0; c < bm.cols; c++)
            {
                var s = bm.grid[r, c];
                if (s != null && !s.IsLocked)
                {
                    all.Add(s);
                }
            }

        int lockCount = Mathf.Min(level3_lockCountPerWave, all.Count);
        for (int i = 0; i < lockCount; i++)
        {
            int idx = UnityEngine.Random.Range(0, all.Count);
            var s = all[idx];
            all.RemoveAt(idx);
            s.IsLocked = true;
            s.ApplyVisual();
            s.Flash(Color.gray, 2, 0.08f);
            level3_lockedByBoss.Add(s);
        }

        UIManager.Instance?.ShowMessage($"Hydra locks {lockCount} slots!", 1.5f);

        yield return new WaitForSecondsRealtime(Mathf.Max(3f, level3_lockDurationSec));
        // Unlock all locked slots
        foreach (var s in level3_lockedByBoss)
        {
            if (s != null)
            {
                s.IsLocked = false;
                s.ApplyVisual();
            }
        }
        level3_lockedByBoss.Clear();
    }
    level3_lockRoutine = null;
}

// This method handles the field effects during the boss fight (buffs and debuffs).
private IEnumerator L3_FieldEffectsLoop()
{
    while (!isGameOver && currentLevelConfig?.levelIndex == 3 && !level3_phaseChangeActive)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(8f, level3_fieldEffectIntervalSec));
        var bm = BoardManager.Instance;
        if (bm == null) continue;

        level3_activeZones.RemoveAll(z => Time.unscaledTime > z.end);

        // Spawn buff and debuff zones
        int toSpawnEach = Mathf.Max(1, level3_zonesPerType);
        for (int t = 0; t < toSpawnEach; t++)
        {
            var buff = new L3Zone { rect = L3_RandomRectInBoard(bm, level3_zoneSize), isBuff = true, end = Time.unscaledTime + level3_fieldEffectDurationSec };
            var debuff = new L3Zone { rect = L3_RandomRectInBoard(bm, level3_zoneSize), isBuff = false, end = Time.unscaledTime + level3_fieldEffectDurationSec };
            level3_activeZones.Add(buff);
            level3_activeZones.Add(debuff);
        }

        UIManager.Instance?.ShowMessage("Zones: Green x2 / Red x0.5", 1.8f);
    }
    level3_fieldRoutine = null;
}

// This method randomly generates a rectangular area on the board for field effects (buffs/debuffs).
private RectInt L3_RandomRectInBoard(BoardManager bm, int size)
{
    int w = Mathf.Clamp(size, 1, Mathf.Max(1, bm.cols));
    int h = Mathf.Clamp(size, 1, Mathf.Max(1, bm.rows));
    int x = UnityEngine.Random.Range(0, Mathf.Max(1, bm.rows - h + 1));
    int y = UnityEngine.Random.Range(0, Mathf.Max(1, bm.cols - w + 1));
    return new RectInt(x, y, h, w); // x=row, y=col
}

// This method handles the boss’s random deletion mechanic for letters or cards.
private IEnumerator L3_DeleteActionLoop()
{
    while (!isGameOver && currentLevelConfig?.levelIndex == 3 && !level3_phaseChangeActive)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(5f, level3_deleteActionIntervalSec));

        bool tryLetters = UnityEngine.Random.value < 0.6f;
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

// This method deletes random letters from the board during the boss fight.
private bool L3_DeleteLettersFromBoard(int count)
{
    var bm = BoardManager.Instance;
    if (bm == null || bm.grid == null) return false;

    var filled = new List<BoardSlot>();
    for (int r = 0; r < bm.rows; r++)
        for (int c = 0; c < bm.cols; c++)
        {
            var s = bm.grid[r, c];
            if (s != null && s.HasLetterTile())
            {
                var t = s.GetLetterTile();
                if (t != null && !t.isLocked)
                {
                    filled.Add(s);
                }
            }
        }

    if (filled.Count == 0) return false;

    int k = Mathf.Clamp(count, 1, filled.Count);
    for (int i = 0; i < k; i++)
    {
        int idx = UnityEngine.Random.Range(0, filled.Count);
        var s = filled[idx];
        filled.RemoveAt(idx);
        var t = s.RemoveLetter();
        if (t != null) SpaceManager.Instance.RemoveTile(t);
        s.Flash(Color.red, 2, 0.08f);
    }
    UIManager.Instance?.ShowMessage($"Hydra deletes {k} letters on board!", 1.4f);
    return true;
}

// This method clears all active zones and locks created by the boss during the fight.
private void L3_ClearZonesAndLocks()
{
    level3_activeZones.Clear();
    foreach (var s in level3_lockedByBoss)
    {
        if (s != null)
        {
            s.IsLocked = false;
            s.ApplyVisual();
        }
    }
    level3_lockedByBoss.Clear();
}

private void TriggerStageFail(string reason)
{
    if (isGameOver || phase == GamePhase.GameOver) return;

    isGameOver = true;
    StopLevelTimer();
    StopAllLoops();
    phase = GamePhase.GameOver;

    // Trigger failure and handle UI transition here
    PauseManager.I?.ClosePause();
    BgmPlayer.I?.DuckAndStop(0.18f);
    SfxPlayer.I?.StopAllAndClearBank();

    // Show failure panel
    var panel = StageFailPanel.Instance ?? FindObjectOfType<StageFailPanel>(true);
    if (panel) panel.Show("Stage Fail", "Return to Main Menu?");
    else
    {
        Debug.LogError("StageFailPanel not found under Main Canvas.");
        PauseManager.I?.Btn_ReturnToMainMenu();
    }
}
