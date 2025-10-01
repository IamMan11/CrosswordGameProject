// Level2Controller.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Level2Controller : MonoBehaviour
{
    public static Level2Controller Instance { get; private set; }

    private bool triangleComplete;
    private float triangleCheckTimer;
    private static int _zoneFreezeDepth = 0;

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

    public void Setup()
    {
        triangleComplete = false;
        triangleCheckTimer = 0f;

        ClearAllOverlays();
        triNodes.Clear(); triAllCells.Clear();

        var lm = LevelManager.Instance;
        if (lm?.currentLevelConfig?.levelIndex != 2) return;

        if (lm.level2_useTriangleObjective)
        {
            GenerateTriangleNodes(lm.level2_triangleNodeSize, lm.level2_triangleMinManhattanGap);
            PaintTriangleNodesIdle(lm.level2_triangleIdleColor);
            UpdateTriangleColors(lm.level2_triangleIdleColor, lm.level2_triangleLinkedColor);
        }

        // reset routine/zone เก่า
        if (zoneRoutine != null) { StopCoroutine(zoneRoutine); zoneRoutine = null; }
        RevertAllZones();

        // ✅ โชว์ x2 โซนตั้งแต่ก่อนเริ่มจับเวลา (แต่ยังไม่ start นับหมดเวลา)
        if (lm.level2_enablePeriodicX2Zones)
            ApplyX2ZonesOnce(lm.level2_x2ZonesPerWave, lm.level2_x2ZoneDurationSec, scheduleRevert:false);
    }


    public void OnTimerStart()
    {
        var lm = LevelManager.Instance;
        if (lm?.currentLevelConfig?.levelIndex != 2) return;

        if (lm.level2_enablePeriodicX2Zones && zoneRoutine == null)
        {
            // ✅ เริ่ม “นับหมดเวลา” ให้ wave แรกที่โชว์ไว้ตั้งแต่ Setup
            if (zoneBackups.Count > 0)
                StartCoroutine(RevertZonesAfter(lm.level2_x2ZoneDurationSec));

            // ✅ เริ่มลูป แต่ "รอ interval ก่อน" ค่อยสุ่ม wave ถัดไป
            zoneRoutine = StartCoroutine(PeriodicX2Zones(
                lm.level2_x2ZonesPerWave,
                lm.level2_x2ZoneDurationSec,
                lm.level2_x2IntervalSec
            ));
        }
    }

    public void Tick(float dt)
    {
        var lm = LevelManager.Instance;
        if (lm?.currentLevelConfig?.levelIndex != 2) return;

        if (lm.level2_useTriangleObjective)
        {
            triangleCheckTimer += dt;
            if (triangleCheckTimer >= Mathf.Max(0.1f, lm.level2_triangleCheckPeriod))
            {
                triangleCheckTimer = 0f;
                triangleComplete = CheckTriangleComplete();
                UpdateTriangleColors(lm.level2_triangleIdleColor, lm.level2_triangleLinkedColor);
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
        if (!lm.level2_useTriangleObjective) return;

        UpdateTriangleColors(lm.level2_triangleIdleColor, lm.level2_triangleLinkedColor);
        var ok = CheckTriangleComplete();
        triangleComplete = ok;                     // <<< เพิ่มบรรทัดนี้
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
    IEnumerator PeriodicX2Zones(int zonesPerWave, float zoneDurationSec, float intervalSec)
    {
        while (LevelManager.Instance?.currentLevelConfig?.levelIndex == 2)
        {
            // นับเองแบบ per-frame และ "หักเวลาเฉพาะตอนที่ไม่ถูกแช่"
            float remain = Mathf.Max(1f, intervalSec);
            while (remain > 0f)
            {
                while (PauseManager.IsPaused) yield return null;
                if (!IsZoneTimerFrozen())
                    remain -= Time.unscaledDeltaTime;  // หยุดเดินเมื่อกำลังคิดคะแนน/ถูก freeze
                yield return null;
            }

            // เผื่อเข้า freeze ตอนนับเหลือ 0 พอดี → รอให้พ้นก่อนค่อยสปอว์น
            while (IsZoneTimerFrozen()) yield return null;

            ApplyX2ZonesOnce(zonesPerWave, zoneDurationSec, scheduleRevert:true);
        }
        zoneRoutine = null;
    }
    void ApplyX2ZonesOnce(int zones, float duration, bool scheduleRevert = true)
    {
        var lm = LevelManager.Instance;
        var bm = BoardManager.Instance; if (bm?.grid == null || lm == null) return;
        if (bm.rows < 3 || bm.cols < 3) return;

        RevertAllZones();

        int rows = bm.rows, cols = bm.cols;
        int requiredCheby = Mathf.Max(3, lm.level2_zoneMinCenterCheby);

        var centers = new List<Vector2Int>();
        for (int pass = 0; pass < 3 && centers.Count < zones; pass++)
        {
            int attempts = 0, maxAttempts = 400;
            while (centers.Count < zones && attempts++ < maxAttempts)
            {
                int r = Random.Range(1, rows - 1);
                int c = Random.Range(1, cols - 1);

                // ✅ กัน “ติด/แตะกัน” ด้วย Chebyshev distance
                bool tooClose = centers.Any(cc => Mathf.Max(Mathf.Abs(cc.x - r), Mathf.Abs(cc.y - c)) < requiredCheby);
                if (tooClose) continue;

                centers.Add(new Vector2Int(r, c));
            }

            // ถ้าหาที่ว่างไม่พอ ให้ผ่อนระยะลงทีละ 1 (แต่ไม่ต่ำกว่า 3)
            requiredCheby = Mathf.Max(3, requiredCheby - 1);
        }

        if (centers.Count == 0) return;

        int zoneWordMul   = ScoreManager.EffectiveWordMulFor(lm.level2_multiplierSlotType);
        int zoneLetterMul = ScoreManager.EffectiveLetterMulFor(lm.level2_multiplierSlotType);

        foreach (var center in centers)
        {
            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int rr = center.x + dr, cc = center.y + dc;
                if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) continue;
                var slot = bm.grid[rr, cc]; if (!slot) continue;

                // backup ไว้เผื่อคืน (type ไม่ถูกแก้อยู่แล้ว แต่เก็บไว้ไม่เสียหาย)
                zoneBackups.Add((new Vector2Int(rr, cc), slot.type, slot.manaGain));

                // ❌ ไม่เปลี่ยนชนิดช่อง ไม่ใส่ไอคอน Wx2/Wx3 เพิ่ม
                // ✅ ใส่ตัวคูณ “ชั่วคราว” จากโซนเท่านั้น
                slot.SetTempMultipliers(zoneLetterMul, zoneWordMul);

                // ✅ ลงโอเวอร์เลย์ให้เห็นโซน
                slot.SetZoneOverlayTop(lm.level2_zoneOverlayColor);
            }
        }

        UIManager.Instance?.ShowMessage("x2 Zones appeared!", 2f);

        // เริ่มนับหมดเวลาหรือไม่ (ตอน Setup = false)
        if (scheduleRevert)
            StartCoroutine(RevertZonesAfter(duration));
    }
    public static bool IsTriangleCell(int r, int c)
    {
        var inst = Instance;
        if (inst == null) return false;
        return inst.triAllCells.Contains(new Vector2Int(r, c));
    }
    // เรียกจากที่อื่นเพื่อสั่ง "แช่แข็ง/คลาย" ตัวจับเวลาโซน
    public static void SetZoneTimerFreeze(bool on)
    {
        _zoneFreezeDepth = on ? _zoneFreezeDepth + 1 : Mathf.Max(0, _zoneFreezeDepth - 1);
    }

    // เช็กแบบกันพลาด: ถ้า freeze ไว้, หรือกำลัง Scoring, หรือ timeScale==0 → ถือว่า "ต้องหยุดนับ"
    public static bool IsZoneTimerFrozen()
    {
        if (_zoneFreezeDepth > 0) return true;
        if (TurnManager.Instance?.IsScoringAnimation ?? false) return true;
        if (Time.timeScale == 0f) return true;
        return false;
    }


    IEnumerator RevertZonesAfter(float duration)
    {
        float remain = Mathf.Max(0f, duration);
        while (remain > 0f)
        {
            if (!IsZoneTimerFrozen())
                remain -= Time.unscaledDeltaTime;   // หักเฉพาะตอนที่ไม่ถูกแช่ไว้

            yield return null;
        }

        RevertAllZones();
        UIManager.Instance?.ShowMessage("x2 Zones ended", 1.5f);
    }

    void RevertAllZones()
    {
        if (zoneBackups.Count == 0) { RedrawTriangleOverlays(); return; }

        var bm = BoardManager.Instance; if (bm?.grid == null) { zoneBackups.Clear(); return; }

        foreach (var it in zoneBackups)
        {
            var v = it.pos;
            if (v.x < 0 || v.y < 0 || v.x >= bm.rows || v.y >= bm.cols) continue;
            var s = bm.grid[v.x, v.y]; if (s == null) continue;

            s.type = it.prevType; 
            s.manaGain = it.prevMana;
            s.ClearTempMultipliers();              // ✅ ล้างตัวคูณจากโซน
            s.ApplyVisual();
        }
        zoneBackups.Clear();

        ClearAllOverlays();
        RedrawTriangleOverlays();
    }

    void RedrawTriangleOverlays()
    {
        var lm = LevelManager.Instance; if (lm == null) return;
        if (!lm.level2_useTriangleObjective) return;
        UpdateTriangleColors(lm.level2_triangleIdleColor, lm.level2_triangleLinkedColor);
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
