// Level2Controller.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
public class Level2Controller : MonoBehaviour
{
    public static Level2Controller Instance { get; private set; }
    // ================== Inspector – Level 2 Settings ==================
    [Header("Level 2 – Triangle")]
    [SerializeField] public bool  L2_useTriangleObjective = true;
    [SerializeField][Min(1)] public int L2_triangleNodeSize = 1;
    [SerializeField][Min(2)] public int L2_triangleMinManhattan = 6;
    [SerializeField] public float L2_triangleCheckPeriod = 0.5f;
    [SerializeField] public Color L2_triangleIdleColor = new Color32(40,40,40,200);
    [SerializeField] public Color L2_triangleLinkedColor = new Color32(30,180,60,200);

    [Header("Level 2 – Locked Board")]
    [SerializeField] public bool  L2_enableLockedBoard = true;
    [SerializeField] public int   L2_lockedCount = 3;
    [SerializeField] public Vector2Int L2_requiredLenRange = new Vector2Int(3, 7);

    [Header("Level 2 – Bench Issue")]
    [SerializeField] public bool  L2_enableBenchIssue = true;
    [SerializeField][Min(0)] public int   L2_benchIssueCount = 2;
    [SerializeField] public float L2_benchIssueIntervalSec = 60f;
    [SerializeField] public float L2_benchIssueDurationSec = 60f;
    [SerializeField] public float L2_benchIssueOverlaySec = 2f;
    [SerializeField] public int   L2_benchZeroPerMove = 2;
    [SerializeField] public int   L2_benchPenaltyPerWord = 0;
    [SerializeField] public Color L2_benchIssueOverlayColor = new Color(0f,0f,0f,0.55f);

    [Header("Level 2 – Theme & Rewards")]
    [SerializeField] public bool  L2_applyThemeOnStart = true;
    [SerializeField] public bool  L2_grantWinRewards = true;
    [SerializeField] public int   L2_winCogCoin = 1;
    [SerializeField] public string L2_nextFloorClue = "—";

    [Header("Level 2 – Periodic 2x2 Zones (3x3 board groups)")]
    [SerializeField] public bool  L2_enablePeriodicX2Zones = true;
    [SerializeField] public float L2_x2IntervalSec = 180f;
    [SerializeField] public int   L2_x2ZonesPerWave = 2;
    [SerializeField] public float L2_zoneDurationSec = 30f;
    [SerializeField][Min(3)] public int L2_zoneMinCenterCheby = 4;
    [SerializeField] public SlotType L2_multiplierSlotType = SlotType.TripleWord;
    [SerializeField] public Color L2_zoneOverlayColor = new Color(0.2f,0.9f,0.2f,0.28f);
    public enum MultiplierSlotType { None, DoubleLetter, TripleLetter, DoubleWord, TripleWord }

    [Header("Level 2 – Locked Segments")]
    [SerializeField] public bool  L2_enableLockedSegments = true;
    [SerializeField][Min(1)] public int   L2_lockedSegmentLength = 4;
    [SerializeField][Min(1)] public int   L2_lockedSegmentCount = 3;
    [SerializeField] public Color L2_lockedOverlayColor = new Color(0f,0f,0f,0.55f);

    // ======== (ถ้ามี) อ้างอิง UI/ระบบอื่น ๆ ========
    [Header("UI (Optional)")]
    [SerializeField] public TMP_Text objectiveText;
    // ==== Internals ====
    private readonly Dictionary<BoardSlot,int> _lockedSlotsByLen = new();  // Locked Board
    private bool _benchIssueActive;                   // Bench Issue (interval)
    private float _benchIssueEndTime;
    private Coroutine _benchIssueRoutine;
    private Coroutine _zoneRoutine;
    private readonly List<(Vector2Int pos, SlotType prevType, int prevMana)> _zoneBackups = new();
    private readonly List<BoardSlot> _lockedSegmentSlots = new();
    private string _lastPenalizedWord = "";
    private static int _zoneFreezeDepth = 0;

    private bool triangleComplete;
    private float triangleCheckTimer;

    private readonly List<HashSet<Vector2Int>> triNodes = new();  // โหนด (size×size) จำนวน 3 ชุด
    private readonly HashSet<Vector2Int> triAllCells = new();

    private readonly List<(Vector2Int pos, SlotType prevType, int prevMana)> zoneBackups = new();
    private Coroutine zoneRoutine;
    // ใช้เดิน 4 ทิศ
    static readonly Vector2Int[] ORTHO = new[] {
        new Vector2Int(-1, 0), new Vector2Int(1, 0),
        new Vector2Int(0, -1), new Vector2Int(0, 1)
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    public (int min, int max) GetRequiredLenRange() => (L2_requiredLenRange.x, L2_requiredLenRange.y);
    public void Setup()
    {
        // ล้างของเก่า
        StopAllEffects();
        ClearAllOverlays();
        _lockedSlotsByLen.Clear();
        _lockedSegmentSlots.Clear();
        _benchIssueActive = false;
        _lastPenalizedWord = "";

        // Triangle
        if (L2_useTriangleObjective)
        {
            GenerateTriangleNodes(L2_triangleNodeSize, L2_triangleMinManhattan);
            PaintTriangleNodesIdle(L2_triangleIdleColor);
            UpdateTriangleColors(L2_triangleIdleColor, L2_triangleLinkedColor);
        }

        // Locked Segments (spawn ตอนเริ่มด่าน)
        if (L2_enableLockedSegments)
            SpawnLockedSegments();

        // Periodic x2 zones: โชว์ wave แรกไว้เลย (ยังไม่เริ่มนับหมดเวลา จนกว่า timer start)
        if (L2_enablePeriodicX2Zones)
            ApplyX2ZonesOnce(L2_x2ZonesPerWave, L2_zoneDurationSec, scheduleRevert:false);

        // Locked Board (seed ช่องที่จะปลดด้วยความยาวคำ)
        if (L2_enableLockedBoard)
            SeedLockedSlots();
    }

    public void OnTimerStart()
    {
        // เริ่มนับหมดเวลาให้ wave แรก ถ้ามี
        if (L2_enablePeriodicX2Zones)
        {
            if (_zoneBackups.Count > 0) StartCoroutine(RevertZonesAfter(L2_zoneDurationSec));
            if (_zoneRoutine == null)
                _zoneRoutine = StartCoroutine(PeriodicX2Zones(L2_x2ZonesPerWave, L2_zoneDurationSec, L2_x2IntervalSec));
        }

        // เริ่ม loop Bench Issue (interval)
        if (L2_enableBenchIssue && _benchIssueRoutine == null)
            _benchIssueRoutine = StartCoroutine(BenchIssueLoop());
    }
    public void StopAllEffects()
    {
        if (_zoneRoutine != null) { StopCoroutine(_zoneRoutine); _zoneRoutine = null; }
        if (_benchIssueRoutine != null) { StopCoroutine(_benchIssueRoutine); _benchIssueRoutine = null; }
        RevertAllZones();
        ClearLockedSegments();
    }

    // ==== Locked Board ====
    void SeedLockedSlots()
    {
        var bm = BoardManager.Instance; if (bm?.grid == null) return;

        var pool = new List<BoardSlot>();
        for (int r=0;r<bm.rows;r++)
        for (int c=0;c<bm.cols;c++)
        {
            var s = bm.grid[r,c];
            if (s == null || s.HasLetterTile() || s.IsLocked) continue;
            pool.Add(s);
        }
        int want = Mathf.Clamp(L2_lockedCount, 0, pool.Count);
        _lockedSlotsByLen.Clear();

        for (int i=0;i<want;i++)
        {
            int idx = Random.Range(0, pool.Count);
            var slot = pool[idx]; pool.RemoveAt(idx);

            int reqLen = Random.Range(L2_requiredLenRange.x, L2_requiredLenRange.y + 1);
            slot.IsLocked = true;
            slot.bg.color = new Color32(120,120,120,255);
            _lockedSlotsByLen[slot] = reqLen;
        }

        if (_lockedSlotsByLen.Count > 0)
            UIManager.Instance?.ShowMessage($"Board bugged: {_lockedSlotsByLen.Count} slots locked (unlock by word length)", 2f);
    }

    public void TryUnlockByWordLength()
    {
        if (!L2_enableLockedBoard || _lockedSlotsByLen.Count == 0) return;

        string main = TurnManager.Instance?.LastConfirmedWord ?? string.Empty;
        if (string.IsNullOrWhiteSpace(main)) return;

        int len = main.Trim().Length;
        if (len <= 0) return;

        var toUnlock = new List<BoardSlot>();
        foreach (var kv in _lockedSlotsByLen)
            if (kv.Value == len) toUnlock.Add(kv.Key);

        foreach (var s in toUnlock)
        {
            if (!s) continue;
            s.IsLocked = false;
            s.ApplyVisual();
            s.Flash(Color.green, 2, 0.08f);
            _lockedSlotsByLen.Remove(s);
        }
        if (toUnlock.Count > 0)
            UIManager.Instance?.ShowMessage($"Unlocked {toUnlock.Count} bugged slot(s) by length {len}", 2f);
    }

    // ==== Bench Issue (interval + per-confirm) ====
    IEnumerator BenchIssueLoop()
    {
        while (LevelManager.Instance?.GetCurrentLevelIndex() == 2 && !LevelManager.Instance.IsGameOver())
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(5f, L2_benchIssueIntervalSec));

            _benchIssueActive = true;
            _benchIssueEndTime = Time.unscaledTime + Mathf.Max(3f, L2_benchIssueDurationSec);
            _lastPenalizedWord = "";
            UIManager.Instance?.ShowMessage("Bench bug active: some bench letters give 0 score!", L2_benchIssueDurationSec);

            while (Time.unscaledTime < _benchIssueEndTime && !LevelManager.Instance.IsGameOver())
                yield return null;

            _benchIssueActive = false;
            UIManager.Instance?.ShowMessage("Bench bug ended.", 1.2f);
        }
        _benchIssueRoutine = null;
    }

    // per-confirm: ทำให้บางตัวที่เติมลง Bench “คะแนน = 0” หนึ่งเทิร์น
    public void TriggerBenchIssueAfterRefill()
    {
        if (!L2_enableBenchIssue) return;
        StartCoroutine(BenchIssueAfterRefillCo());
    }

    IEnumerator BenchIssueAfterRefillCo()
    {
        var bm = BenchManager.Instance;
        if (bm == null) yield break;

        while (bm.IsRefilling()) yield return null;
        yield return null;

        ScoreManager.ClearZeroScoreTiles();
        foreach (var t in bm.GetAllBenchTiles())
            t.SetBenchIssueOverlay(false);

        var pool = new List<LetterTile>(bm.GetAllBenchTiles());
        int pick = Mathf.Clamp(L2_benchIssueCount, 0, pool.Count);

        var chosen = new List<LetterTile>();
        for (int i=0;i<pick && pool.Count>0;i++)
        {
            int idx = Random.Range(0, pool.Count);
            chosen.Add(pool[idx]); pool.RemoveAt(idx);
        }

        if (chosen.Count > 0)
        {
            ScoreManager.MarkZeroScoreTiles(chosen);
            foreach (var t in chosen)
                t.SetBenchIssueOverlay(true, L2_benchIssueOverlayColor);
            UIManager.Instance?.ShowFloatingToast($"Bench issue: {chosen.Count} tile(s) score 0 next turn", Color.gray, 2f);
        }
    }

    public void TryApplyBenchPenalty()
    {
        if (!L2_enableBenchIssue || !_benchIssueActive) return;
        if (L2_benchPenaltyPerWord <= 0) return;

        string main = TurnManager.Instance?.LastConfirmedWord ?? string.Empty;
        if (string.IsNullOrWhiteSpace(main)) return;
        if (main.Equals(_lastPenalizedWord, System.StringComparison.OrdinalIgnoreCase)) return;

        TurnManager.Instance?.AddScore(-Mathf.Abs(L2_benchPenaltyPerWord));
        _lastPenalizedWord = main;
    }

    public bool IsBenchIssueActive() => _benchIssueActive;
    public int  SelectZeroCount(int placedCount)
    {
        if (!L2_enableBenchIssue || !_benchIssueActive) return 0;
        return Mathf.Clamp(L2_benchZeroPerMove, 0, Mathf.Max(0, placedCount));
    }

    // ==== Zones (3x3) ====
    IEnumerator PeriodicX2Zones(int zonesPerWave, float zoneDurationSec, float intervalSec)
    {
        while (LevelManager.Instance?.GetCurrentLevelIndex() == 2 && !LevelManager.Instance.IsGameOver())
        {
            float remain = Mathf.Max(1f, intervalSec);
            while (remain > 0f)
            {
                while (PauseManager.IsPaused) yield return null;
                if (!IsZoneTimerFrozen()) remain -= Time.unscaledDeltaTime;
                yield return null;
            }
            while (IsZoneTimerFrozen()) yield return null;
            ApplyX2ZonesOnce(zonesPerWave, zoneDurationSec, scheduleRevert:true);
        }
        _zoneRoutine = null;
    }

    void ApplyX2ZonesOnce(int zones, float duration, bool scheduleRevert=true)
    {
        var bm = BoardManager.Instance; if (bm?.grid == null) return;
        if (bm.rows < 3 || bm.cols < 3) return;

        RevertAllZones();

        int rows = bm.rows, cols = bm.cols;
        int requiredCheby = Mathf.Max(3, L2_zoneMinCenterCheby);

        var centers = new List<Vector2Int>();
        for (int pass=0; pass<3 && centers.Count<zones; pass++)
        {
            int attempts=0, maxAttempts=400;
            while (centers.Count < zones && attempts++ < maxAttempts)
            {
                int r = Random.Range(1, rows-1);
                int c = Random.Range(1, cols-1);
                bool tooClose = centers.Any(cc => Mathf.Max(Mathf.Abs(cc.x-r), Mathf.Abs(cc.y-c)) < requiredCheby);
                if (tooClose) continue;
                centers.Add(new Vector2Int(r,c));
            }
            requiredCheby = Mathf.Max(3, requiredCheby-1);
        }
        if (centers.Count == 0) return;

        int wordMul   = ScoreManager.EffectiveWordMulFor(L2_multiplierSlotType);
        int letterMul = ScoreManager.EffectiveLetterMulFor(L2_multiplierSlotType);

        foreach (var center in centers)
        {
            for (int dr=-1; dr<=1; dr++)
            for (int dc=-1; dc<=1; dc++)
            {
                int rr = center.x + dr, cc = center.y + dc;
                if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) continue;
                var slot = bm.grid[rr,cc]; if (!slot) continue;

                _zoneBackups.Add((new Vector2Int(rr,cc), slot.type, slot.manaGain));
                slot.SetTempMultipliers(letterMul, wordMul);
                slot.SetZoneOverlayTop(L2_zoneOverlayColor);
            }
        }

        UIManager.Instance?.ShowMessage("x2 Zones appeared!", 2f);
        if (scheduleRevert) StartCoroutine(RevertZonesAfter(duration));
    }

    IEnumerator RevertZonesAfter(float duration)
    {
        float remain = Mathf.Max(0f, duration);
        while (remain > 0f)
        {
            if (!IsZoneTimerFrozen()) remain -= Time.unscaledDeltaTime;
            yield return null;
        }
        RevertAllZones();
        UIManager.Instance?.ShowMessage("x2 Zones ended", 1.5f);
    }

    void RevertAllZones()
    {
        if (_zoneBackups.Count == 0) { RedrawTriangleOverlays(); return; }
        var bm = BoardManager.Instance; if (bm?.grid == null) { _zoneBackups.Clear(); return; }

        foreach (var it in _zoneBackups)
        {
            var v = it.pos;
            var s = bm.grid[v.x, v.y]; if (!s) continue;
            s.type = it.prevType;
            s.manaGain = it.prevMana;
            s.ClearTempMultipliers();
            s.ApplyVisual();
        }
        _zoneBackups.Clear();
        ClearAllOverlays();
        RedrawTriangleOverlays();
    }

    public static void SetZoneTimerFreeze(bool on)
    { _zoneFreezeDepth = on ? _zoneFreezeDepth+1 : Mathf.Max(0,_zoneFreezeDepth-1); }
    public static bool IsZoneTimerFrozen()
    {
        if (_zoneFreezeDepth > 0) return true;
        if (TurnManager.Instance?.IsScoringAnimation ?? false) return true;
        if (Time.timeScale==0f) return true;
        return false;
    }

    // ==== Locked Segments ====
    public void ClearLockedSegments()
    {
        if (_lockedSegmentSlots.Count == 0) return;
        foreach (var s in _lockedSegmentSlots)
            if (s)
            {
                s.IsLocked = false;
                s.SetLockedVisual(false);
                s.ApplyVisual();
            }
        _lockedSegmentSlots.Clear();
    }

    public void SpawnLockedSegments()
    {
        if (!L2_enableLockedSegments) return;

        var bm = BoardManager.Instance; if (bm?.grid == null) return;
        int rows=bm.rows, cols=bm.cols;
        int segLen = Mathf.Max(1, L2_lockedSegmentLength);
        int segCount = Mathf.Max(0, L2_lockedSegmentCount);

        int centerR = rows/2, centerC = cols/2;
        bool InCenter3x3(int r,int c)=> Mathf.Abs(r-centerR)<=1 && Mathf.Abs(c-centerC)<=1;

        Vector2Int[] ADJ8 = {
            new Vector2Int(-1,0), new Vector2Int(1,0),
            new Vector2Int(0,-1), new Vector2Int(0,1),
            new Vector2Int(-1,-1), new Vector2Int(-1,1),
            new Vector2Int(1,-1),  new Vector2Int(1,1),
        };
        bool NearTriangle8(int r,int c)
        {
            if (IsTriangleCell(r,c)) return true;
            for (int i=0;i<ADJ8.Length;i++)
            {
                int nr=r+ADJ8[i].x, nc=c+ADJ8[i].y;
                if (nr<0||nr>=rows||nc<0||nc>=cols) continue;
                if (IsTriangleCell(nr,nc)) return true;
            }
            return false;
        }
        bool HasLockedNeighbor8(int r,int c)
        {
            for (int i=0;i<ADJ8.Length;i++)
            {
                int nr=r+ADJ8[i].x, nc=c+ADJ8[i].y;
                if (nr<0||nr>=rows||nc<0||nc>=cols) continue;
                var ns=bm.grid[nr,nc];
                if (ns!=null && ns.IsLocked) return true;
            }
            return false;
        }

        int attemptsPerSeg = 200;
        for (int seg=0; seg<segCount; seg++)
        {
            bool placed=false;
            for (int attempt=0; attempt<attemptsPerSeg && !placed; attempt++)
            {
                bool vertical = Random.value<0.5f;
                int startR = vertical ? Random.Range(0, rows-segLen+1) : Random.Range(0, rows);
                int startC = vertical ? Random.Range(0, cols) : Random.Range(0, cols-segLen+1);

                var cand = new List<BoardSlot>();
                for (int k=0;k<segLen;k++)
                {
                    int r = startR + (vertical? k:0);
                    int c = startC + (vertical? 0:k);
                    var s = bm.grid[r,c];
                    if (!s || InCenter3x3(r,c) || s.IsLocked || s.HasLetterTile()
                        || HasLockedNeighbor8(r,c) || NearTriangle8(r,c))
                    { cand.Clear(); break; }
                    cand.Add(s);
                }

                if (cand.Count==segLen)
                {
                    foreach (var s in cand)
                    {
                        s.IsLocked = true;
                        s.SetLockedVisual(true, L2_lockedOverlayColor);
                        _lockedSegmentSlots.Add(s);
                    }
                    placed = true;
                }
            }
        }
    }
    public void Tick(float dt)
    {
        if (L2_useTriangleObjective)
        {
            triangleCheckTimer += dt;
            if (triangleCheckTimer >= Mathf.Max(0.1f, L2_triangleCheckPeriod))
            {
                triangleCheckTimer = 0f;
                triangleComplete = CheckTriangleComplete();
                UpdateTriangleColors(L2_triangleIdleColor, L2_triangleLinkedColor);
                UIManager.Instance?.UpdateTriangleHint(triangleComplete);
            }
        }
    }
    // โหนดถือว่า "ถูกแตะ" เมื่อมีตัวอักษรที่ล็อกแล้ว อยู่ในบล็อกหรือรอบบล็อก 1 ช่อง (4 ทิศ)
    bool NodeTouched(HashSet<Vector2Int> node)
    {
        var bm = BoardManager.Instance; if (bm?.grid == null) return false;

        foreach (var p in node)
        {
            // ในบล็อกเอง
            var inSlot = bm.grid[p.x, p.y];
            var inTile = inSlot ? inSlot.GetLetterTile() : null;
            if (inTile && inTile.isLocked) return true;

            // รอบบล็อก 1 ช่อง
            for (int k = 0; k < ORTHO.Length; k++)
            {
                int nr = p.x + ORTHO[k].x, nc = p.y + ORTHO[k].y;
                if (nr < 0 || nr >= bm.rows || nc < 0 || nc >= bm.cols) continue;

                var s = bm.grid[nr, nc];
                var t = s ? s.GetLetterTile() : null;
                if (t && t.isLocked) return true;
            }
        }
        return false;
    }
    public int NodeCount => triNodes.Count;

    public int GetTouchedNodeCount()
    {
        int cnt = 0;
        for (int i = 0; i < triNodes.Count; i++)
            if (NodeTouched(triNodes[i])) cnt++;
        return Mathf.Clamp(cnt, 0, 3);
    }

    // ---------- Triangle (size×size nodes) ----------
    void GenerateTriangleNodes(int size, int minManhattanGap)
    {
        var bm = BoardManager.Instance; if (bm?.grid == null) return;
        int rows = bm.rows, cols = bm.cols;
        size = Mathf.Max(1, size);
        int gap = Mathf.Max(0, minManhattanGap);

        triNodes.Clear();
        triAllCells.Clear();

        var chosen = new List<Vector2Int>();   // top-left ของแต่ละก้อน
        var taken  = new HashSet<Vector2Int>(); // เซลล์ที่ถูกใช้แล้ว (กันทับ/กันแตะ)

        // กันแตะ 8 ทิศ
        Vector2Int[] ADJ8 = new Vector2Int[] {
            new Vector2Int(-1, 0), new Vector2Int(1, 0),
            new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(-1,-1), new Vector2Int(-1, 1),
            new Vector2Int( 1,-1), new Vector2Int( 1, 1),
        };

        // ห้ามลงกลาง 3x3
        int centerR = rows / 2, centerC = cols / 2;
        bool InCenter3x3(int r, int c) => Mathf.Abs(r - centerR) <= 1 && Mathf.Abs(c - centerC) <= 1;

        bool BlockOk(int topR, int leftC)
        {
            for (int dr = 0; dr < size; dr++)
            for (int dc = 0; dc < size; dc++)
            {
                int rr = topR + dr, cc = leftC + dc;
                if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) return false;
                if (InCenter3x3(rr, cc)) return false;

                var s = bm.grid[rr, cc];
                if (s == null || s.IsLocked) return false;      // ห้ามลงบนท่อนล็อก

                // ห้าม “ติด” ท่อนล็อก 8 ทิศ
                for (int i = 0; i < ADJ8.Length; i++)
                {
                    int nr = rr + ADJ8[i].x, nc = cc + ADJ8[i].y;
                    if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                    var ns = bm.grid[nr, nc];
                    if (ns != null && ns.IsLocked) return false;
                }
            }
            return true;
        }

        // ห้ามทับ/ห้ามแตะก้อนที่เลือกไว้ก่อนหน้า (เช็กเป็นรายเซลล์)
        bool NoOverlapOrTouch(int topR, int leftC)
        {
            for (int dr = 0; dr < size; dr++)
            for (int dc = 0; dc < size; dc++)
            {
                var v = new Vector2Int(topR + dr, leftC + dc);
                if (taken.Contains(v)) return false; // ทับ

                for (int i = 0; i < ADJ8.Length; i++)           // แตะ 8 ทิศ
                    if (taken.Contains(v + ADJ8[i])) return false;
            }
            return true;
        }

        // สุ่มหาก้อนให้ครบ 3
        int safety = 1200;
        while (chosen.Count < 3 && safety-- > 0)
        {
            int r = UnityEngine.Random.Range(0, rows - size + 1);
            int c = UnityEngine.Random.Range(0, cols - size + 1);

            // กระจายตัวแบบแมนฮัตตันด้วย top-left (กันกระจุก)
            bool farEnough = chosen.All(p => Mathf.Abs(p.x - r) + Mathf.Abs(p.y - c) >= gap);
            if (!farEnough) continue;

            if (!BlockOk(r, c)) continue;
            if (!NoOverlapOrTouch(r, c)) continue;

            // ผ่านทั้งหมด → ยอมรับ
            chosen.Add(new Vector2Int(r, c));
            for (int dr = 0; dr < size; dr++)
                for (int dc = 0; dc < size; dc++)
                    taken.Add(new Vector2Int(r + dr, c + dc));
        }

        // ถ้ายังไม่ครบ 3 → กวาดทั้งกริดหาแบบเป็นระบบ
        for (int r = 0; chosen.Count < 3 && r <= rows - size; r++)
            for (int c = 0; chosen.Count < 3 && c <= cols - size; c++)
                if (BlockOk(r, c) && NoOverlapOrTouch(r, c))
                {
                    chosen.Add(new Vector2Int(r, c));
                    for (int dr = 0; dr < size; dr++)
                        for (int dc = 0; dc < size; dc++)
                            taken.Add(new Vector2Int(r + dr, c + dc));
                }

        // ประกอบชุดเซลล์จริง
        foreach (var tl in chosen.Take(3))
        {
            var set = new HashSet<Vector2Int>();
            for (int dr = 0; dr < size; dr++)
                for (int dc = 0; dc < size; dc++)
                {
                    int rr = tl.x + dr, cc = tl.y + dc;
                    set.Add(new Vector2Int(rr, cc));
                    triAllCells.Add(new Vector2Int(rr, cc));
                }
            triNodes.Add(set);
        }
    }

    void PaintTriangleNodesIdle(Color idleColor)
    {
        var bm = BoardManager.Instance; if (bm?.grid == null) return;
        foreach (var cell in triAllCells)
        {
            var s = bm.grid[cell.x, cell.y]; if (s == null) continue;
            s.SetTriangleOverlay(idleColor);
        }
    }

    void UpdateTriangleColors(Color idleColor, Color linkedColor)
    {
        var bm = BoardManager.Instance; if (bm?.grid == null) return;

        for (int i = 0; i < triNodes.Count; i++)
        {
            bool touched = NodeTouched(triNodes[i]);         // ✅ ไม่ต้องเชื่อมกับจุดอื่น
            var col = touched ? linkedColor : idleColor;

            foreach (var cell in triNodes[i])
            {
                var s = bm.grid[cell.x, cell.y]; if (s == null) continue;
                s.SetTriangleOverlay(col);
            }
        }
    }

    bool CheckTriangleComplete()
    {
        if (triNodes.Count < 3) return false;
        // ✅ ทุกโหนดมีตัวอักษรแตะในระยะ 1 ช่อง (หรือในตัวโหนดเอง)
        for (int i = 0; i < triNodes.Count; i++)
            if (!NodeTouched(triNodes[i])) return false;
        return true;
    }
    public void OnAfterLettersLocked()
    {
        var lm = LevelManager.Instance;
        if (lm?.currentLevelConfig?.levelIndex != 2) return;
        if (!L2_useTriangleObjective) return;

        UpdateTriangleColors(L2_triangleIdleColor, L2_triangleLinkedColor);
        var ok = CheckTriangleComplete();
        triangleComplete = ok;
        UIManager.Instance?.UpdateTriangleHint(ok);
    }

    bool IsNodePairConnected(HashSet<Vector2Int> A, HashSet<Vector2Int> B)
    {
        var bm = BoardManager.Instance; if (bm?.grid == null) return false;
        int rows = bm.rows, cols = bm.cols;

        // จุดยึด = ช่องที่มีตัวอักษร "ในบล็อก + รอบบล็อก 1 ช่อง"
        var anchorsA = CollectAnchors(A);
        var anchorsB = CollectAnchors(B);
        if (anchorsA.Count == 0 || anchorsB.Count == 0) return false;

        var targetSet = new HashSet<Vector2Int>(anchorsB);

        var q = new Queue<Vector2Int>();
        var visited = new bool[rows, cols];

        foreach (var a in anchorsA)
        {
            q.Enqueue(a);
            visited[a.x, a.y] = true;
        }

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (targetSet.Contains(cur)) return true; // แตะจุดยึดของอีกโหนดแล้ว

            for (int k = 0; k < ORTHO.Length; k++)
            {
                int nr = cur.x + ORTHO[k].x, nc = cur.y + ORTHO[k].y;
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                if (visited[nr, nc]) continue;

                var sl = bm.grid[nr, nc];
                if (sl == null || !sl.HasLetterTile()) continue; // เดินได้เฉพาะช่องที่มีตัวอักษร

                visited[nr, nc] = true;
                q.Enqueue(new Vector2Int(nr, nc));
            }
        }
        return false;
    }


    // ---------- Zones (3×3) ----------
    List<Vector2Int> CollectAnchors(HashSet<Vector2Int> node)
    {
        var bm = BoardManager.Instance;
        var anchors = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int>();
        if (bm?.grid == null) return anchors;

        void AddIfLetter(Vector2Int v)
        {
            if (v.x < 0 || v.x >= bm.rows || v.y < 0 || v.y >= bm.cols) return;
            if (!seen.Add(v)) return;
            var s = bm.grid[v.x, v.y];
            if (s != null && s.HasLetterTile()) anchors.Add(v);
        }

        foreach (var p in node)
        {
            // ในบล็อกเอง
            AddIfLetter(p);
            // รอบบล็อก 1 ช่อง (4 ทิศ)
            for (int k = 0; k < ORTHO.Length; k++)
                AddIfLetter(new Vector2Int(p.x + ORTHO[k].x, p.y + ORTHO[k].y));
        }
        return anchors;
    }
    public static bool IsTriangleCell(int r, int c)
    {
        var inst = Instance;
        if (inst == null) return false;
        return inst.triAllCells.Contains(new Vector2Int(r, c));
    }

    void RedrawTriangleOverlays()
    {
        if (!L2_useTriangleObjective) return;
        UpdateTriangleColors(L2_triangleIdleColor, L2_triangleLinkedColor);
    }

    void ClearAllOverlays()
    {
        var bm = BoardManager.Instance; if (bm?.grid == null) return;
        for (int r = 0; r < bm.rows; r++)
            for (int c = 0; c < bm.cols; c++)
                bm.grid[r, c]?.ClearZoneOverlay();
    }

    public bool IsTriangleComplete() => triangleComplete;
}
