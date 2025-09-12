// ==== NEW: Level1GarbledIT.cs ====
// วางไว้ที่ Assets/Script/Level/
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Level1GarbledIT : MonoBehaviour
{
    public static Level1GarbledIT Instance { get; private set; }

    [Header("Refs")]
    public BoardManager board;                  // อ้าง BoardManager
    public LetterTile   letterTilePrefab;       // Prefab ตัวอักษร
    public LetterData[] letterDatabase;         // DB map ตัวอักษร -> LetterData
    public Transform    outlineParent;          // Parent สำหรับเส้นกรอบ (ใส่เป็น GameObject ว่างบน Canvas/World ก็ได้)

    [Header("Runtime")]
    public bool enabledThisLevel = false;

    class GarbledSet
    {
        public string target;                   // คำเป้าหมาย (เช่น "CODE")
        public List<BoardSlot> slots = new();   // ช่องบนบอร์ด (เรียงตามลำดับอักษร)
        public LineRenderer outline;            // กรอบรอบชุด
        public bool touched = false;            // สลับมาแล้ว?
        public bool solved  = false;            // แก้สำเร็จแล้ว?
    }
    BoardSlot _pendingSwap;

    readonly List<GarbledSet> sets = new();
    readonly Dictionary<BoardSlot, GarbledSet> slot2set = new();

    void Awake()
    {
        Instance = this;
    }

    // ========= API ที่ LevelManager เรียกตอนเริ่มด่าน 1 =========
    public void Setup(LevelConfig cfg)
    {
        sets.Clear();
        slot2set.Clear();

        enabledThisLevel = cfg.level1_enableGarbledIT;
        if (!enabledThisLevel) return;
        if (!board || !letterTilePrefab || letterDatabase == null || letterDatabase.Length == 0)
        {
            Debug.LogWarning("[GarbledIT] Missing refs.");
            enabledThisLevel = false;
            return;
        }

        // สุ่มเลือกคำ IT
        var pool = new List<string>(cfg.level1_itWords.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (pool.Count == 0) { enabledThisLevel = false; return; }
        int need = Mathf.Clamp(cfg.level1_garbledCount, 1, pool.Count);

        // random words (ไม่ซ้ำ)
        for (int i = 0; i < need && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            string w = pool[idx].Trim();
            pool.RemoveAt(idx);

            if (!TryPlaceWordSetUpper(w, cfg)) {
                Debug.LogWarning($"[GarbledIT] วางคำไม่ได้: {w}");
            }
        }
    }

    // ========= ให้ TurnManager เรียกหลังคิดคะแนนหลักเสร็จ =========
    public IEnumerator ProcessAfterMainScoring(LevelConfig cfg)
    {
        if (!enabledThisLevel) yield break;

        // ตรวจเฉพาะชุดที่ผู้เล่นแตะแล้วเท่านั้น
        foreach (var set in sets.Where(s => s.touched && !s.solved))
        {
            string current = BuildCurrent(set).ToUpperInvariant();
            string target  = set.target.ToUpperInvariant();

            if (current == target)
            {
                // สำเร็จ: ปลดล็อค + ล้างวิชวล
                set.solved = true;
                foreach (var slot in set.slots)
                {
                    slot.ClearSpecialBg();      // ดูฟังก์ชันใน BoardSlot ด้านล่าง
                    var tile = slot.GetLetterTile();
                    if (tile) tile.isLocked = false; // ปลดล็อคให้กลายเป็นตัวปกติ
                }
                SetOutlineColor(set, new Color(0,0,0,0));
                if (set.outline) Destroy(set.outline.gameObject);
                LevelManager.Instance?.ShowToast($"แก้คำ {target} ถูกต้อง!", Color.cyan);
            }
            else
            {
                // ผิด: หักแต้มตามผลรวมคะแนนตัวอักษร แล้วสลับใหม่
                int penalty = 0;
                foreach (var slot in set.slots)
                {
                    var t = slot.GetLetterTile();
                    if (t != null && t.GetData() != null)
                        penalty += Mathf.Max(0, t.GetData().score);
                }
                TurnManager.Instance?.AddScore(-penalty);

                ShuffleTilesInSet(set);  // สุ่มลำดับใหม่
                set.touched = false;     // รีเซ็ตสถานะ เพื่อไม่หักซ้ำถ้าไม่แตะรอบหน้า
                SetOutlineColor(set, LevelManager.Instance.currentLevelConfig.level1_outlineDefaultColor);
                LevelManager.Instance?.ShowToast($"คำ {target} ยังไม่ถูก -{penalty}", Color.red);
            }

            // เว้นระยะนิดสำหรับเอฟเฟกต์/อ่าน UI
            yield return new WaitForSecondsRealtime(0.15f);
        }
    }

    // ========= ให้ PlacementManager เรียกเพื่อ “สลับ” ในชุดเดียวกัน =========
    public bool TrySwapIfGarbledPair(BoardSlot a, BoardSlot b)
    {
        if (!enabledThisLevel) return false;
        if (!slot2set.TryGetValue(a, out var sa)) return false;
        if (!slot2set.TryGetValue(b, out var sb)) return false;
        if (sa != sb || sa.solved) return false;

        var ta = a.GetLetterTile();
        var tb = b.GetLetterTile();
        if (!ta || !tb) return false;

        // Swap ตัวจริง
        a.ForcePlaceLetter(tb); // TODO: map ให้ตรงกับเมธอดวางในโปรเจกต์ (เช่น SetLetterTile/AttachTileToSlot)
        b.ForcePlaceLetter(ta);

        // Mark touched + เปลี่ยนกรอบเหลือง
        sa.touched = true;
        SetOutlineColor(sa, LevelManager.Instance.currentLevelConfig.level1_outlineTouchedColor);
        return true;
    }

    /// <summary>ให้ BoardSlot เรียกเมื่อถูกคลิก: จัดการเลือก/สลับในชุดเดียวกัน</summary>
    public bool HandleClickSlot(BoardSlot slot)
    {
        if (!enabledThisLevel || slot == null) return false;
        if (!slot2set.TryGetValue(slot, out var set) || set.solved) return false;

        // เลือกครั้งแรก
        if (_pendingSwap == null) { _pendingSwap = slot; return true; }

        // คลิกเดิม = ยกเลิก
        if (_pendingSwap == slot) { _pendingSwap = null; return true; }

        // ต่างช่อง → ต้องอยู่ชุดเดียวกัน
        if (slot2set.TryGetValue(_pendingSwap, out var set2) && set2 == set)
        {
            var a = _pendingSwap; var b = slot;
            _pendingSwap = null;

            var ta = a.GetLetterTile();
            var tb = b.GetLetterTile();
            if (ta && tb)
            {
                a.ForcePlaceLetter(tb);
                b.ForcePlaceLetter(ta);
                set.touched = true; // ทำกรอบเหลือง (เคยแตะแล้ว)
                SetOutlineColor(set, LevelManager.Instance.currentLevelConfig.level1_outlineTouchedColor);
            }
            return true;
        }
        else
        {
            _pendingSwap = slot; // คนละชุด → เปลี่ยน selection
            return true;
        }
    }

    // ========= ภายใน =========
    string BuildCurrent(GarbledSet set)
    {
        var chars = new List<char>(set.slots.Count);
        foreach (var s in set.slots)
        {
            var t = s.GetLetterTile();
            var d = (t != null) ? t.GetData() : null;
            chars.Add(d != null && d.letter.Length > 0 ? char.ToUpperInvariant(d.letter[0]) : '_');
        }
        return new string(chars.ToArray());
    }

    bool TryPlaceWordSetUpper(string wordLower, LevelConfig cfg)
    {
        string w = wordLower.Trim().ToUpperInvariant();
        if (w.Length < 2) return false;

        // เลือกแนวนอน/แนวตั้ง
        bool horiz = (Random.value < 0.5f);
        int R = board.rows, C = board.cols;

        // ลองสุ่มตำแหน่งเริ่ม
        for (int tries = 0; tries < cfg.level1_placeMaxRetries; tries++)
        {
            int r = Random.Range(0, R);
            int c = Random.Range(0, C);

            if (horiz)
            {
                if (c + w.Length > C) continue;
            }
            else
            {
                if (r + w.Length > R) continue;
            }

            // เช็คซ้อน/เว้นระยะจากชุดอื่น
            var slots = new List<BoardSlot>(w.Length);
            bool ok = true;
            for (int i = 0; i < w.Length; i++)
            {
                int rr = r + (horiz ? 0 : i);
                int cc = c + (horiz ? i : 0);
                var s = board.GetSlot(rr, cc);
                if (s == null) { ok = false; break; }
                if (s.HasLetterTile()) { ok = false; break; }               // ต้องวางบนช่องว่าง
                if (!IsFarEnoughFromOtherSets(rr, cc, cfg.level1_minGapBetweenSets)) { ok = false; break; }
                slots.Add(s);
            }
            if (!ok) continue;

            // วาง
            var set = new GarbledSet { target = w, slots = slots };
            SpawnLockedWord(set, cfg);
            DrawOutline(set, cfg.level1_outlineDefaultColor);
            sets.Add(set);
            foreach (var s in slots) slot2set[s] = set;
            return true;
        }
        return false;
    }

    bool IsFarEnoughFromOtherSets(int r, int c, int minGap)
    {
        foreach (var s in slot2set.Keys)
        {
            int dr = Mathf.Abs(s.row - r);
            int dc = Mathf.Abs(s.col - c);
            if (Mathf.Max(dr, dc) < minGap) return false; // Chebyshev gap
        }
        return true;
    }

    void SpawnLockedWord(GarbledSet set, LevelConfig cfg)
    {
        // วางตัวอักษร “ตรงตามคำ” ก่อน แล้วค่อยสลับให้มั่ว
        for (int i = 0; i < set.target.Length; i++)
        {
            var slot = set.slots[i];
            var data = FindLetterData(set.target[i]);
            if (data == null) { Debug.LogWarning($"[GarbledIT] no LetterData for {set.target[i]}"); continue; }

            var tile = Instantiate(letterTilePrefab);
            tile.Setup(data);                 // TODO: map ให้ตรงเมธอดในโปรเจกต์คุณ
            tile.isLocked = true;
            slot.ForcePlaceLetter(tile);        // TODO: map กับเมธอดวางลงช่อง

            // วิชวล: พื้นหลังดำ
            slot.SetSpecialBg(cfg.level1_garbledSlotBg);
        }
        ShuffleTilesInSet(set);
    }

    void ShuffleTilesInSet(GarbledSet set)
    {
        // สุ่มให้ไม่เรียงตรง target ถ้าเผลอตรง ให้สลับอีกที
        var tiles = set.slots.Select(s => s.GetLetterTile()).ToList();
        int n = tiles.Count;
        for (int i = 0; i < n; i++)
        {
            int j = Random.Range(i, n);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }
        // apply
        for (int i = 0; i < n; i++)
            set.slots[i].ForcePlaceLetter(tiles[i]);

        // ถ้าดันตรงเป้าพอดี ให้สลับ 2 ตัวเล็กน้อย
        if (BuildCurrent(set).ToUpperInvariant() == set.target.ToUpperInvariant() && n >= 2)
            set.slots[0].SwapWith(set.slots[1]); // TODO: helper ง่าย ๆ ถ้าไม่มี ใช้ ForcePlaceLetter สลับเองก็ได้
    }

    LetterData FindLetterData(char chUpper)
    {
        // DB เป็นตัวพิมพ์ใหญ่/เล็กต่างกันให้ normalize เอง
        foreach (var d in letterDatabase)
        {
            if (d == null || string.IsNullOrEmpty(d.letter)) continue;
            if (char.ToUpperInvariant(d.letter[0]) == chUpper) return d;
        }
        return null;
    }
    void SetOutlineColor(GarbledSet set, Color col)
    {
        if (set != null && set.outline != null)
        {
            set.outline.startColor = col;
            set.outline.endColor   = col;
        }
    }

    void DrawOutline(GarbledSet set, Color col)
    {
        // วาดกรอบสี่เหลี่ยมครอบ bounding box ชุด
        var go = new GameObject($"GarbledOutline_{set.target}", typeof(LineRenderer));
        if (outlineParent) go.transform.SetParent(outlineParent, false);
        var lr = go.GetComponent<LineRenderer>();
        lr.loop = true;
        lr.positionCount = 4;
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.025f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        set.outline = lr;

        SetOutlineColor(set, col);
        RefreshOutlinePositions(set);
    }

    void RefreshOutlinePositions(GarbledSet set)
    {
        var trs = set.slots.Select(s => s.transform.position);
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in trs)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
        }
        // ขยายขอบนิดหน่อย
        float pad = 0.25f;
        var p0 = new Vector3(minX - pad, minY - pad, 0);
        var p1 = new Vector3(maxX + pad, minY - pad, 0);
        var p2 = new Vector3(maxX + pad, maxY + pad, 0);
        var p3 = new Vector3(minX - pad, maxY + pad, 0);
        set.outline.SetPositions(new[] { p0, p1, p2, p3 });
    }

    void Update()
    {
        if (!enabledThisLevel) return;
        // เผื่อช่องขยับ ให้กรอบตาม
        foreach (var s in sets) if (s.outline && !s.solved) RefreshOutlinePositions(s);
    }
}
