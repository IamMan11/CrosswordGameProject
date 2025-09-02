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
    public TMP_Text timerText;         // legacy
    public TMP_Text levelTimerText;    // จับเวลารวมของด่าน
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

    // ===== Level 1 – IT words requirement (ของเดิม) =====
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

    // ===== Level 2 – เพิ่มเฉพาะส่วนนี้ =====
    [Header("Level 2 – Triangle Objective")]
    [Tooltip("เปิดใช้เงื่อนไขสามเหลี่ยมในด่าน 2")]
    public bool level2_useTriangleObjective = true;

    [Tooltip("พิกัดเป้าหมาย 3 จุด (row,col) ต้องเชื่อมถึงกันด้วยตัวอักษรบนบอร์ด")]
    public Vector2Int[] level2_triangleTargets = new Vector2Int[]
    {
        new Vector2Int(2,2),
        new Vector2Int(2,12),
        new Vector2Int(12,7)
    };

    [Tooltip("เช็คความสมบูรณ์ของสามเหลี่ยมทุก ๆ กี่วินาที (ลดภาระ CPU)")]
    public float level2_triangleCheckPeriod = 0.5f;

    // cache
    private bool  level2_triangleComplete = false;
    private float level2_triangleCheckTimer = 0f;

    [Header("Level 2 – Periodic X2 Zones (3x3)")]
    [Tooltip("เปิดสุ่มโซนคูณคำ x2 (3x3) เป็นระยะในด่าน 2")]
    public bool level2_enablePeriodicX2Zones = true;

    [Tooltip("ทุก ๆ กี่วินาทีจะสุ่มโซนใหม่ (เช่น 180 = 3 นาที)")]
    public float level2_x2IntervalSec = 180f;

    [Tooltip("แต่ละรอบสุ่มกี่โซน")]
    public int level2_x2ZonesPerWave = 2;

    [Tooltip("โซนคูณอยู่ได้นานกี่วินาทีต่อรอบ")]
    public float level2_x2ZoneDurationSec = 30f;

    [Tooltip("ชนิดช่องพิเศษที่ใช้เป็นโซนคูณ (แนะนำ DoubleWord)")]
    public SlotType level2_multiplierSlotType = SlotType.DoubleWord;

    private Coroutine level2_x2Routine;
    private readonly List<(Vector2Int pos, SlotType prevType, int prevMana)> level2_activeZoneChanges
        = new List<(Vector2Int, SlotType, int)>();

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

        // เดินเวลาเมื่อเริ่มแล้ว และไม่ถูก pause เท่านั้น
        if (timerStarted && !timerPaused)
        {
            levelTimeElapsed += Time.unscaledDeltaTime;

            if (cfg.timeLimit > 0f)
            {
                // โหมดนับถอยหลัง
                float remaining = Mathf.Max(0f, levelTimeLimit - levelTimeElapsed);
                UpdateLevelTimerText(remaining);
                if (remaining <= 0f)
                {
                    StopLevelTimer();
                    GameOver(false);   // ❌ หมดเวลา
                    return;
                }
            }
            else
            {
                // โหมดนับขึ้น
                UpdateLevelTimerText(levelTimeElapsed);
            }
        }

        // ====== Level 2 tick (เพิ่มเฉพาะส่วนนี้) ======
        if (cfg.levelIndex == 2)
        {
            // เช็คสามเหลี่ยมแบบ throttle
            if (level2_useTriangleObjective && level2_triangleTargets != null && level2_triangleTargets.Length >= 3)
            {
                level2_triangleCheckTimer += Time.unscaledDeltaTime;
                if (level2_triangleCheckTimer >= level2_triangleCheckPeriod)
                {
                    level2_triangleCheckTimer = 0f;
                    level2_triangleComplete = CheckTriangleComplete();
                }
            }

            // เริ่ม/หยุด routine โซน x2 อัตโนมัติ
            if (level2_enablePeriodicX2Zones && level2_x2Routine == null)
                level2_x2Routine = StartCoroutine(Level2_PeriodicX2Zones());
        }

        // ✅ เงื่อนไขผ่านด่าน
        if (CheckWinConditions(cfg))
        {
            AnnounceLevelComplete();
            _ = StartCoroutine(GoToNextLevel());
        }
    }

    /// <summary>ให้ TurnManager เรียกเมื่อคะแนน/จำนวนคำเปลี่ยน</summary>
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

    // ===== ใช้เฉพาะด่าน 1 ของเดิม: รับ “คำที่ยืนยันแล้ว” เพื่ออัปเดตจำนวน IT-words =====
    public void RegisterConfirmedWords(IEnumerable<string> words)
    {
        if (phase == GamePhase.GameOver || words == null) return;
        if (GetCurrentConfig()?.levelIndex != 1) return;        // ใช้เฉพาะด่าน 1

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

        // 🟢 ด่าน 1: ต้องมีคำ IT ถึงเป้า
        if (cfg.levelIndex == 1)
        {
            if (itWordsFound.Count < itWordsTargetLevel1) return false;
        }

        // 🟣 ด่าน 2: สามเหลี่ยมต้องครบ (ถ้าเปิดใช้)
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
        if (timerText) timerText.gameObject.SetActive(false); // legacy UI

        // ตั้งค่าเวลาเลเวล (เริ่มจริงเมื่อ OnFirstConfirm)
        levelTimeElapsed = 0f;
        levelTimeLimit = Mathf.Max(0f, cfg.timeLimit);
        levelTimerRunning = false;
        timerStarted = false;
        timerPaused = false;
        UpdateLevelTimerText(levelTimeLimit > 0 ? levelTimeLimit : 0f);

        // รีเซ็ต progress คำ IT เฉพาะด่าน 1 (ของเดิม)
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

        // รีเซ็ตของเลเวล 2 (เพิ่มเฉพาะส่วนนี้)
        level2_triangleComplete = false;
        level2_triangleCheckTimer = 0f;
        Level2_RevertAllZones(); // กันโซนค้างจากด่านก่อน

        if (BoardManager.Instance != null) BoardManager.Instance.GenerateBoard();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ResetForNewLevel();
            if (TileBag.Instance != null) TileBag.Instance.RefillTileBag();
            TurnManager.Instance.UpdateBagUI();
            if (BenchManager.Instance != null) BenchManager.Instance.RefillEmptySlots();
            TurnManager.Instance.UpdateBagUI();
        }

        Debug.Log(
            $"▶ เริ่มด่าน {cfg.levelIndex} | เวลา: {cfg.timeLimit}s | Score target: {cfg.requiredScore}"
        );
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

        Debug.Log(win ? "🎉 ชนะทุกด่าน" : "💀 แพ้เพราะหมดเวลา");
        // TODO: เปิด GameOver/Shop UI ตามเกมของคุณ
    }

    private void StopAllLoops()
    {
        // หยุดคอร์รุตีนของเลเวล 2 ถ้ามี
        if (level2_x2Routine != null) { StopCoroutine(level2_x2Routine); level2_x2Routine = null; }
        Level2_RevertAllZones();
    }

    // ------------------------------ Helpers (ของเดิม) ------------------------------
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

    private void SetPhase(GamePhase next) => phase = next;

    private static string Normalize(string s) =>
        (s ?? string.Empty).Trim().ToLowerInvariant();

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
    // Level 2: Triangle Objective + Periodic X2 Zones
    // ==============================

    /// <summary>
    /// เช็คว่าจุดทั้งสามใน level2_triangleTargets “เชื่อมถึงกัน” ผ่านตัวอักษรบนบอร์ดหรือไม่
    /// เงื่อนไข: ช่องเป้าหมายทั้ง 3 ต้องมีตัวอักษรอยู่ และเส้นทางเชื่อมต่อผ่านช่องที่มีตัวอักษร (4 ทิศ)
    /// </summary>
    private bool CheckTriangleComplete()
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return false;
        if (level2_triangleTargets == null || level2_triangleTargets.Length < 3) return false;

        // แปลงเป็นภายในขอบเขต และตรวจว่ามีตัวอักษรบนทั้ง 3 จุด
        var targets = new List<Vector2Int>();
        foreach (var v in level2_triangleTargets)
        {
            int r = v.x, c = v.y;
            if (r < 0 || r >= bm.rows || c < 0 || c >= bm.cols) return false;
            var slot = bm.grid[r, c];
            if (slot == null || !slot.HasLetterTile()) return false; // ต้องมีตัวอักษรบนตำแหน่งเป้าหมาย
            targets.Add(new Vector2Int(r, c));
        }

        // BFS เริ่มจากจุดที่ 1 ผ่าน “ช่องที่มีตัวอักษร”
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
                if (s == null || !s.HasLetterTile()) continue; // เดินผ่านเฉพาะช่องที่มีตัวอักษร

                visited[nr, nc] = true;
                q.Enqueue(new Vector2Int(nr, nc));
            }
        }

        // จุดเป้าหมายที่ 2 และ 3 ต้องถูกเยี่ยมถึงด้วย
        return visited[targets[1].x, targets[1].y] && visited[targets[2].x, targets[2].y];
    }

    private IEnumerator Level2_PeriodicX2Zones()
    {
        while (!isGameOver && GetCurrentConfig() != null && GetCurrentConfig().levelIndex == 2)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, level2_x2IntervalSec));

            // สุ่มโซนใหม่
            ApplyX2ZonesOnce(
                zones: Mathf.Max(1, level2_x2ZonesPerWave),
                duration: Mathf.Max(5f, level2_x2ZoneDurationSec)
            );

            // รอให้หมดอายุ (ภายใน ApplyX2ZonesOnce จะเริ่มคอร์รุตีน revert ให้เอง)
        }
        level2_x2Routine = null;
    }

    private void ApplyX2ZonesOnce(int zones, float duration)
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return;

        // เคลียร์ของเก่าก่อนเพื่อกันชนกัน
        Level2_RevertAllZones();

        int rows = bm.rows, cols = bm.cols;
        int attempts = 0, maxAttempts = 200;

        var chosenCenters = new List<Vector2Int>();

        while (chosenCenters.Count < zones && attempts++ < maxAttempts)
        {
            int r = Random.Range(1, rows - 1);  // เพื่อให้ 3x3 ไม่ล้นขอบ
            int c = Random.Range(1, cols - 1);

            // กันซ้อนกับศูนย์กลางเดิมเกินไป
            bool tooClose = chosenCenters.Any(cc => Mathf.Abs(cc.x - r) + Mathf.Abs(cc.y - c) < 3);
            if (tooClose) continue;

            chosenCenters.Add(new Vector2Int(r, c));
        }

        foreach (var center in chosenCenters)
        {
            // ทำ 3x3
            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int rr = center.x + dr, cc = center.y + dc;
                if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) continue;

                var slot = bm.grid[rr, cc];
                if (slot == null) continue;

                // เก็บของเดิมไว้ก่อน
                level2_activeZoneChanges.Add(
                    (new Vector2Int(rr, cc), slot.type, slot.manaGain)
                );

                // เปลี่ยนเป็นคูณคำ (ไม่ไปยุ่งคะแนนตัวอักษร)
                slot.type = level2_multiplierSlotType;
                // ไม่ยุ่ง manaGain เดิม (หรือจะตั้ง 0 ก็ได้ ถ้าอยาก)
                slot.ApplyVisual();
            }
        }

        if (level2_activeZoneChanges.Count > 0)
        {
            UIManager.Instance?.ShowMessage($"x2 Zones appeared!", 2f);
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
