using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Level3Controller
/// - แยกกลไกด่าน 3 (Boss Hydra) ออกจาก LevelManager ให้เป็นคลาสเฉพาะ
/// - รับบทเป็น orchestrator: จัดการ HP/Phase Change/Conveyor/Lock Wave/Field Effects/Random Delete
/// - มี API ให้ LevelManager เรียก: Setup, Tick, OnPlayerDealtWord, StopAllLoops
/// </summary>
public class Level3Controller : MonoBehaviour
{
    public static Level3Controller Instance { get; private set; }

    [Header("Enable")]
    [Tooltip("เปิด/ปิดระบบบอสของด่าน 3")]
    public bool level3_enableBoss = true;

    [Header("L3 – Boss")]
    public int   level3_bossMaxHP = 300;
    [Tooltip("เงื่อนไข critical ตามความยาวคำหลัก")]
    public int   level3_criticalLength = 7;
    [Tooltip("โบนัสความเสียหายเมื่อ critical (เช่น 0.25 = +25%)")]
    [Range(0, 3f)] public float level3_criticalBonus = 0.5f;

    [Header("L3 – Conveyor Shuffle")]
    public float level3_conveyorIntervalSec = 65f;
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
    public TMP_Text bossHpText; // ใส่ในอินสเปกเตอร์ถ้ามี

    // --------- internals ---------
    private int  hp;
    private bool phaseChangeActive;
    private bool phaseTriggered;
    private bool sprintTriggered;

    private Coroutine coConveyor;
    private Coroutine coLockWave;
    private Coroutine coField;
    private Coroutine coDelete;

    private readonly List<BoardSlot> lockedByBoss = new List<BoardSlot>();

    private struct L3Zone { public RectInt rect; public bool isBuff; public float end; }
    private readonly List<L3Zone> activeZones = new List<L3Zone>();

    // puzzle ระหว่าง vanish (1/3 → 2/3 → 3/3)
    private int        puzzleStage;        // 0=off, 1..3 active
    private Vector2Int puzzleA, puzzleB;
    public  float      puzzleCheckPeriod = 0.5f;
    private float      puzzleCheckTimer  = 0f;

    // คูลดาวน์สุ่มลบ
    private float nextLetterDeleteTime = 0f;
    private float nextCardDeleteTime   = 0f;

    // === Events (relay ไป UI อื่น ถ้าเกมคุณมี) ===
    public event Action<int>    OnBossDeleteBenchRequest;
    public event Action         OnBossDeleteRandomCardRequest;
    public event Action<float>  OnBossLockCardSlotRequest;

    // =========================================================

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ---------------------------------------------------------
    // Public API for LevelManager
    // ---------------------------------------------------------

    /// <summary>เรียกตอนเริ่ม Level 3</summary>
    public void Setup()
    {
        // ธีม/อัปเกรด (เหมือนของเดิมใน LevelManager)
        Debug.Log("[Level3] Apply theme: black-purple background, Hydra behind board.");
        var prog = PlayerProgressSO.Instance?.data;
        if (prog != null) TurnManager.Instance?.UpgradeMaxMana(prog.maxMana);

        hp = Mathf.Max(1, level3_bossMaxHP);
        phaseChangeActive = false;
        phaseTriggered    = false;
        sprintTriggered   = false;
        ClearZonesAndLocks();
        UpdateBossUI();

        if (level3_enableBoss)
        {
            if (coConveyor == null) coConveyor = StartCoroutine(ConveyorLoop());
            if (coLockWave == null) coLockWave = StartCoroutine(LockWaveLoop());
            if (coField   == null) coField    = StartCoroutine(FieldEffectsLoop());
            if (coDelete  == null) coDelete   = StartCoroutine(DeleteActionLoop());
        }
    }

    /// <summary>เรียกทุกเฟรมจาก LevelManager เฉพาะตอนอยู่ด่าน 3</summary>
    public void Tick(float unscaledDeltaTime)
    {
        if (!level3_enableBoss) return;
        if (phaseChangeActive && puzzleStage > 0)
        {
            puzzleCheckTimer += unscaledDeltaTime;
            if (puzzleCheckTimer >= puzzleCheckPeriod)
            {
                puzzleCheckTimer = 0f;
                if (CheckPuzzleConnected(puzzleA, puzzleB))
                {
                    puzzleStage++;
                    if (puzzleStage <= 3)
                    {
                        float frac = puzzleStage / 3f; // 1/3 → 2/3 → 3/3
                        ResetBoardToFill(frac);
                        PickPuzzlePoints(); // จุดใหม่ทุกขั้น
                        UIManager.Instance?.ShowMessage($"Puzzle stage {puzzleStage}/3", 2f);
                    }
                    if (puzzleStage > 3)
                    {
                        // บอสกลับมาเดินกลไกปกติ
                        phaseChangeActive = false;
                        UIManager.Instance?.ShowMessage("Hydra reappears!", 2f);
                        if (coConveyor == null) coConveyor = StartCoroutine(ConveyorLoop());
                        if (coLockWave == null) coLockWave = StartCoroutine(LockWaveLoop());
                        if (coField   == null) coField    = StartCoroutine(FieldEffectsLoop());
                        if (coDelete  == null) coDelete   = StartCoroutine(DeleteActionLoop());
                    }
                }
            }
        }
    }

    /// <summary>
    /// ให้ LevelManager เรียกเมื่อผู้เล่นยืนยันคำ (ย้ายมาจาก LevelManager.Level3_OnPlayerDealtWord)
    /// </summary>
    public void OnPlayerDealtWord(int placedCount, int placedLettersDamageSum, int mainWordLen, List<Vector2Int> placedCoords)
    {
        if (!level3_enableBoss) return;
        var cfg = LevelManager.Instance?.currentLevelConfig;
        if (cfg == null || cfg.levelIndex != 3) return;
        if (LevelManager.Instance.phase != LevelManager.GamePhase.Running) return;
        if (phaseChangeActive) return; // vanish อยู่ – ยังตีบอสไม่ได้

        int sum = Mathf.Max(0, placedLettersDamageSum);
        if (sum <= 0 || placedCount <= 0) return;

        // best-of-N roll
        int draws = Mathf.Max(1, placedCount);
        int best = 0;
        for (int i = 0; i < draws; i++)
        {
            int roll = UnityEngine.Random.Range(1, sum + 1); // inclusive
            if (roll > best) best = roll;
        }
        float dmg = best;

        // Field Effects
        bool hitBuff = false, hitDebuff = false;
        if (placedCoords != null && placedCoords.Count > 0 && activeZones.Count > 0)
        {
            foreach (var z in activeZones)
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

        hp = Mathf.Max(0, hp - final);
        UpdateBossUI();
        UIManager.Instance?.ShowMessage($"🗡 Hydra -{final}", 1.5f);

        // Thresholds
        int hp50 = Mathf.CeilToInt(level3_bossMaxHP * level3_phaseChangeHPPercent);
        int hp25 = Mathf.CeilToInt(level3_bossMaxHP * level3_sprintHPPercent);

        if (!phaseTriggered && hp <= hp50)
        {
            phaseTriggered = true;
            StartCoroutine(StartPhaseChange());
        }

        if (!sprintTriggered && hp <= hp25)
        {
            sprintTriggered = true;
            // บีบเวลาให้เหลือ 3:00 (ถ้าน้อยกว่านี้ก็เซ็ตเป็น 3:00)
            LevelManager.Instance.levelTimeLimit =
                LevelManager.Instance.levelTimeElapsed + Mathf.Max(0f, level3_sprintRemainingSec);
            LevelManager.Instance.UpdateLevelTimerText(level3_sprintRemainingSec);
            UIManager.Instance?.ShowMessage("⏱ Hydra enraged: time set to 3:00!", 2.5f);
        }

        if (hp <= 0)
        {
            // ชนะตามสเป็คเดิม
            LevelManager.Instance.GameOver(true);
        }
    }

    public void StopAllLoops()
    {
        if (coConveyor != null) { StopCoroutine(coConveyor); coConveyor = null; }
        if (coLockWave != null) { StopCoroutine(coLockWave); coLockWave = null; }
        if (coField   != null) { StopCoroutine(coField);     coField    = null; }
        if (coDelete  != null) { StopCoroutine(coDelete);    coDelete   = null; }
        ClearZonesAndLocks();
    }

    // ---------------------------------------------------------
    // Internals (เดิมย้ายออกมาจาก LevelManager)
    // ---------------------------------------------------------

    private void UpdateBossUI()
    {
        if (bossHpText) bossHpText.text = $"Hydra HP: {Mathf.Max(0, hp)}/{level3_bossMaxHP}";
    }

    private IEnumerator StartPhaseChange()
    {
        if (phaseChangeActive) yield break;

        // หยุดคลื่น/โซน/ลบต่าง ๆ ชั่วคราว
        StopAllLoops();
        ClearZonesAndLocks();

        phaseChangeActive = true;
        puzzleStage = 1;

        // + เวลา 7:30 (เพราะ levelTimeLimit = เวลาสูงสุดนับจากเริ่ม)
        LevelManager.Instance.levelTimeLimit += Mathf.Max(0f, level3_phaseTimeBonusSec);

        // reset board → เหลือเติม 1/3
        ResetBoardToFill(1f/3f);
        PickPuzzlePoints();

        UIManager.Instance?.ShowMessage("Hydra vanished! Connect the two points (1/3 → 3/3).", 3f);
        yield break;
    }

    private void ResetBoardToFill(float fraction)
    {
        var bm = BoardManager.Instance; if (bm == null) return;

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

    private void PickPuzzlePoints()
    {
        var bm = BoardManager.Instance; if (bm == null) return;

        var candidates = new List<Vector2Int>();
        for (int r = 0; r < bm.rows; r++)
            for (int c = 0; c < bm.cols; c++)
                if (bm.grid[r, c] != null && bm.grid[r, c].HasLetterTile())
                    candidates.Add(new Vector2Int(r, c));

        if (candidates.Count < 2) { puzzleA = Vector2Int.zero; puzzleB = Vector2Int.zero; return; }

        puzzleA = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        puzzleB = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        UIManager.Instance?.UpdateTriangleHint(false); // reuse indicator ถ้าต้องการ
    }

    private bool CheckPuzzleConnected(Vector2Int a, Vector2Int b)
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

    private IEnumerator ConveyorLoop()
    {
        while (!LevelManager.Instance.isGameOver &&
               LevelManager.Instance.currentLevelConfig?.levelIndex == 3 &&
               !phaseChangeActive)
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
                    if (t.isLocked) continue;
                    slots.Add(s);
                }
            if (slots.Count < 2) continue;

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
        coConveyor = null;
    }

    private IEnumerator LockWaveLoop()
    {
        while (!LevelManager.Instance.isGameOver &&
               LevelManager.Instance.currentLevelConfig?.levelIndex == 3 &&
               !phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(10f, level3_lockWaveIntervalSec));
            var bm = BoardManager.Instance; if (bm == null || bm.grid == null) continue;

            // สุ่มล็อกช่องที่ยังว่างและไม่ใช่ center/triangle (อ้างอิงข้อจำกัดเดิม)
            var all = new List<BoardSlot>();
            for (int r = 0; r < bm.rows; r++)
                for (int c = 0; c < bm.cols; c++)
                {
                    var s = bm.grid[r,c];
                    if (!s) continue;
                    if (s.IsLocked) continue;
                    if (s.HasLetterTile()) continue;
                    if (Level2Controller.IsTriangleCell(r, c)) continue; // อย่าทับโหนดสามเหลี่ยม
                    all.Add(s);
                }

            int want = Mathf.Clamp(level3_lockCountPerWave, 0, all.Count);
            var picked = new List<BoardSlot>();
            for (int i = 0; i < want; i++)
            {
                int idx = UnityEngine.Random.Range(0, all.Count);
                picked.Add(all[idx]); all.RemoveAt(idx);
            }

            foreach (var s in picked)
            {
                if (!s) continue;
                s.IsLocked = true;
                s.SetLockedVisual(true, new Color(0f,0f,0f,0.55f));
                lockedByBoss.Add(s);
            }
            UIManager.Instance?.ShowMessage($"Hydra locks {picked.Count} cells!", 1.4f);

            // ปลดหลังครบเวลา
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, level3_lockDurationSec));
            foreach (var s in picked)
            {
                if (!s) continue;
                s.IsLocked = false;
                s.SetLockedVisual(false);
            }
            lockedByBoss.RemoveAll(x => x == null || !x.IsLocked);
        }
        coLockWave = null;
    }

    private IEnumerator FieldEffectsLoop()
    {
        while (!LevelManager.Instance.isGameOver &&
               LevelManager.Instance.currentLevelConfig?.levelIndex == 3 &&
               !phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(10f, level3_fieldEffectIntervalSec));
            var bm = BoardManager.Instance; if (bm == null || bm.grid == null) continue;

            int rows = bm.rows, cols = bm.cols;
            int size = Mathf.Max(2, level3_zoneSize);

            void SpawnZones(bool isBuff, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    int r = UnityEngine.Random.Range(0, Mathf.Max(1, rows - size + 1));
                    int c = UnityEngine.Random.Range(0, Mathf.Max(1, cols - size + 1));
                    var rect = new RectInt(c, r, size, size); // RectInt ใช้ (x=col, y=row)
                    activeZones.Add(new L3Zone { rect = rect, isBuff = isBuff, end = Time.unscaledTime + Mathf.Max(3f, level3_fieldEffectDurationSec) });

                    // บอกภาพ overlay ชั่วคราว (เลือกใช้ API บน BoardSlot เองถ้าต้องการ)
                    for (int rr = r; rr < r + size; rr++)
                        for (int cc = c; cc < c + size; cc++)
                        {
                            var s = bm.grid[rr, cc];
                            if (!s) continue;
                            if (isBuff) s.SetZoneOverlayTop(new Color(0f, 0.7f, 0f, 0.28f));
                            else       s.SetTriangleOverlay(new Color(0.7f, 0f, 0f, 0.28f));
                        }
                }
            }

            // สร้าง buff/debuff อย่างละ n โซน
            SpawnZones(true , Mathf.Max(1, level3_zonesPerType));
            SpawnZones(false, Mathf.Max(1, level3_zonesPerType));

            // รอหมดอายุแล้วเคลียร์ overlay
            yield return new WaitForSecondsRealtime(Mathf.Max(3f, level3_fieldEffectDurationSec));
            // clear ที่หมดอายุ และเคลียร์ overlay บนบอร์ด
            var keep = new List<L3Zone>();
            foreach (var z in activeZones)
            {
                if (Time.unscaledTime <= z.end) { keep.Add(z); continue; }
                for (int rr = z.rect.y; rr < z.rect.y + z.rect.height; rr++)
                    for (int cc = z.rect.x; cc < z.rect.x + z.rect.width; cc++)
                    {
                        var s = bm.grid[rr, cc];
                        if (s) s.ClearZoneOverlay();
                    }
            }
            activeZones.Clear();
            activeZones.AddRange(keep);
        }
        coField = null;
    }

    private IEnumerator DeleteActionLoop()
    {
        while (!LevelManager.Instance.isGameOver &&
               LevelManager.Instance.currentLevelConfig?.levelIndex == 3 &&
               !phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(10f, level3_deleteActionIntervalSec));

            // สลับสุ่มว่าจะลบจากบอร์ดหรือเล่นงาน Bench/Card
            bool tryLetters = Time.unscaledTime >= nextLetterDeleteTime;
            bool tryCards   = Time.unscaledTime >= nextCardDeleteTime;

            if (tryLetters && DeleteLettersFromBoard(level3_deleteBoardCount))
                nextLetterDeleteTime = Time.unscaledTime + level3_deleteLettersCooldownSec;

            else if (tryCards)
            {
                // ส่ง event ไป UI/ระบบการ์ด (ถ้าอยากให้มี)
                OnBossDeleteBenchRequest?.Invoke(level3_deleteBenchCount);
                OnBossDeleteRandomCardRequest?.Invoke();
                OnBossLockCardSlotRequest?.Invoke(level3_cardSlotLockDurationSec);
                UIManager.Instance?.ShowMessage("Hydra interferes with your hand!", 1.4f);
                nextCardDeleteTime = Time.unscaledTime + level3_deleteCardsCooldownSec;
            }
        }
        coDelete = null;
    }

    private bool DeleteLettersFromBoard(int count)
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

    private void ClearZonesAndLocks()
    {
        activeZones.Clear();
        foreach (var s in lockedByBoss)
        {
            if (s) { s.IsLocked = false; s.ApplyVisual(); }
        }
        lockedByBoss.Clear();
    }
}
