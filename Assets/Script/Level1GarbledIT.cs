// ===============================
// Level1GarbledIT.cs  (cleaned)
// รับผิดชอบระบบ "Garbled IT" ทั้งหมดของด่าน 1
// - สุ่มคำ IT X คำ วางแบบไม่ติดกัน (แนวนอน/แนวตั้ง)
// - ทำ BG ช่องเป็นสีพิเศษ + วาดกรอบรอบชุด
// - สลับตัวอักษรในชุด (คลิกสลับ หรือเรียก TrySwapIfGarbledPair จากระบบลาก)
// - หลังคิดคะแนนหลัก: ตรวจเฉพาะชุดที่ผู้เล่น "แตะ" แล้วเท่านั้น
//   ถ้าถูก → ปลดล็อค/ล้างวิชวล  |  ถ้าผิด → หักแต้มรวมตัวอักษร + สลับใหม่
// - ป้องกันการเรียก Setup ซ้ำ, เก็บกวาด reference ที่ถูก Destroy แล้ว
// ===============================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection; // สำหรับ auto fill จาก TileBag
using System.Linq;
using UnityEngine.UI;

public class Level1GarbledIT : MonoBehaviour
{
    class GarbledSet {
    public string target;
    public List<BoardSlot> slots = new();
    public LineRenderer outline;      // เดิม
    public RectTransform uiOutline;   // ใหม่
    public bool touched, solved;
    }
    public static Level1GarbledIT Instance { get; private set; }

    // ---------- Refs / Config ----------
    [Header("Refs")]
    [Tooltip("BoardManager ของซีน")]
    public BoardManager board;
    [Tooltip("Prefab ตัวอักษร (LetterTile)")]
    public LetterTile letterTilePrefab;
    [Tooltip("ฐานข้อมูลตัวอักษรที่ใช้สร้างคำ (A–Z ฯลฯ)")]
    public LetterData[] letterDatabase;
    [Tooltip("พาเรนต์สำหรับวาดกรอบ (LineRenderer)")]
    public Transform outlineParent;
    [Header("Outline (UI)")]
    public bool outlineUseUI = true;
    [Min(1)] public int outlineWidthPx = 8;
    // ใหม่
    [Min(0)] public int outlinePaddingPx = 4;

    [Header("Auto Fill From TileBag")]
    [Tooltip("ถ้าตั้งค่า จะดึง LetterData อัตโนมัติจาก TileBag ถ้า list ว่าง")]
    public bool autoFillFromTileBag = true;
    [Tooltip("ถ้าอยากระบุ TileBag เอง (ไม่ใช้ Singleton)")]
    public TileBag tileBagOverride;

    [Header("Interaction")]
    [Tooltip("ให้คลิก 2 ช่อง (ในชุดเดียวกัน) เพื่อสลับตัวอักษร")]
    public bool enableClickSwap = false;

    [Header("Outline")]
    [Min(0.001f)] public float outlineWidth = 0.025f;
    [Min(0.0f)]   public float outlinePadding = 0.25f;
    BoardSlot _dragStart;

    // ---------- Runtime / State ----------
    public bool IsActive { get; private set; } = false;
    bool _initializedThisLevel = false;

    // สี/ค่าจาก LevelConfig จะถูกคงไว้ที่นี่เพื่อไม่ต้องอ้าง LevelManager ขณะรัน
    Color _colSlotBg, _colOutlineDefault, _colOutlineTouched;
    readonly List<GarbledSet> _sets = new();
    readonly Dictionary<BoardSlot, GarbledSet> _slot2set = new();
    BoardSlot _pendingSwap;                  // สำหรับคลิก-สลับ
    static Material _lineMat;                // แชร์ material กรอบ

    // ---------- Unity ----------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (_lineMat == null) _lineMat = new Material(Shader.Find("Sprites/Default"));
    }

    void OnDisable() => ClearAll();

    void Update()
    {
        if (!IsActive) return;

        PruneAllSets(); // กัน reference ที่ถูก Destroy
        for (int i = 0; i < _sets.Count; i++)
        {
            var s = _sets[i];
            if (s != null && !s.solved && s.outline != null)
                RefreshOutlinePositions(s);
        }
    }

    // =========================================================
    //                      Public API
    // =========================================================

    /// <summary>เรียกตอนเริ่มด่าน: เซ็ตอัพระบบจาก LevelConfig</summary>
    public void Setup(LevelConfig cfg)
    {
        ClearAll();

        // เก็บค่าสีไว้ใช้ภายหลัง ไม่ต้องอ้าง LevelManager ตอนรัน
        _colSlotBg        = cfg.level1_garbledSlotBg;
        _colOutlineDefault = cfg.level1_outlineDefaultColor;
        _colOutlineTouched = cfg.level1_outlineTouchedColor;

        // เงื่อนไขเปิด/ปิดระบบ
        IsActive = cfg.level1_enableGarbledIT;
        if (!IsActive) return;

        if (autoFillFromTileBag && (letterDatabase == null || letterDatabase.Length == 0))
            TryAutoFillLetterDatabaseFromTileBag();

        if (board == null || letterTilePrefab == null || letterDatabase == null || letterDatabase.Length == 0)
        {
            Debug.LogWarning("[GarbledIT] Missing refs/letterDatabase — ปิดระบบในด่านนี้");
            IsActive = false;
            return;
        }

        // สุ่มเลือกคำ IT (ไม่ซ้ำ) แล้วลองวาง
        var pool = new List<string>(cfg.level1_itWords.Length);
        foreach (var s in cfg.level1_itWords)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            var w = s.Trim().ToUpperInvariant();
            if (!pool.Contains(w)) pool.Add(w);
        }
        if (pool.Count == 0) { IsActive = false; return; }

        int want = Mathf.Clamp(cfg.level1_garbledCount, 1, pool.Count);
        int fail = 0;

        for (int i = 0; i < want && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            string w = pool[idx];
            pool.RemoveAt(idx);

            if (!TryPlaceWordSet(w, cfg)) fail++;
        }

        if (_sets.Count == 0)
        {
            Debug.LogWarning("[GarbledIT] ไม่สามารถวางคำได้เลย — ปิดระบบในด่านนี้");
            IsActive = false;
            return;
        }

        if (fail > 0) Debug.Log($"[GarbledIT] วางไม่ได้ {fail} คำ (พื้นที่ไม่พอ/ชนเงื่อนไขเว้นระยะ)");
        _initializedThisLevel = true;
    }
    // === Public helpers ===
    public bool IsGarbledSlot(BoardSlot slot)
    {
        if (!IsActive || slot == null) return false;
        return _slot2set.TryGetValue(slot, out var set) && set != null && !set.solved;
    }
    public bool IsGarbledTile(LetterTile t)
    {
        if (!IsActive || t == null) return false;
        var slot = t.transform ? t.transform.GetComponentInParent<BoardSlot>() : null;
        return IsGarbledSlot(slot);
    }
    // อยู่ชุด Garbled เดียวกันและยังไม่ solved ไหม
    public bool AreInSameActiveSet(BoardSlot a, BoardSlot b)
    {
        if (!IsActive || a == null || b == null) return false;
        if (!_slot2set.TryGetValue(a, out var sa)) return false;
        if (!_slot2set.TryGetValue(b, out var sb)) return false;
        return sa == sb && !sa.solved;
    }

    // ทำเครื่องหมายว่าชุดนี้ถูกแตะแล้ว (เปลี่ยนสีกรอบ)
    public void MarkSetTouched(BoardSlot anySlotInSet)
    {
        if (!IsActive || anySlotInSet == null) return;
        if (_slot2set.TryGetValue(anySlotInSet, out var set) && set != null && !set.solved)
        {
            set.touched = true;
            SetOutlineColor(set, _colOutlineTouched);
        }
    }

    /// <summary>ให้ TurnManager เรียกหลัง “คิดคะแนนหลัก” เสร็จในแต่ละ Confirm</summary>
    public IEnumerator ProcessAfterMainScoring()
    {
        if (!IsActive) yield break;
        PruneAllSets();

        // ตรวจเฉพาะชุดที่ผู้เล่น "แตะ" แล้ว และยังไม่ solved
        for (int i = 0; i < _sets.Count; i++)
        {
            var set = _sets[i];
            if (set == null || set.solved || !set.touched) continue;

            bool correct = string.Equals(BuildCurrent(set), set.target, System.StringComparison.Ordinal);
            if (correct)
            {
                // ✔ สำเร็จ: ปลดล็อค + ล้างวิชวล
                set.solved = true;
                for (int k = 0; k < set.slots.Count; k++)
                {
                    var slot = set.slots[k]; if (slot == null) continue;
                    slot.ClearSpecialBg();
                    var tile = slot.GetLetterTile();
                    if (tile != null) tile.isLocked = false;
                }
                SetOutlineColor(set, new Color(0, 0, 0, 0));
                if (set.outline != null) Destroy(set.outline.gameObject);
                UIToast($"แก้คำ {set.target} ถูกต้อง!", Color.cyan);
            }
            else
            {
                // ✖ ผิด: หักแต้มรวมคะแนนตัวอักษรทั้งชุด แล้วสุ่มสลับใหม่
                int penalty = 0;
                for (int k = 0; k < set.slots.Count; k++)
                {
                    var tile = set.slots[k]?.GetLetterTile();
                    var data = (tile != null) ? tile.GetData() : null;
                    if (data != null) penalty += Mathf.Max(0, data.score);
                }
                if (penalty > 0) TurnManager.Instance?.AddScore(-penalty);

                ShuffleTilesInSet(set);
                set.touched = false; // รีเซ็ต: รอบถัดไปจะไม่ถูกหักจนกว่าจะสลับใหม่
                SetOutlineColor(set, _colOutlineDefault);
                UIToast($"คำ {set.target} ยังไม่ถูก -{penalty}", Color.red);
            }

            yield return new WaitForSecondsRealtime(0.12f);
        }
    }

    /// <summary>สำหรับระบบลาก-วาง (ถ้ามี): จะยอมสลับก็ต่อเมื่อ A และ B อยู่ในชุดเดียวกัน</summary>
    public bool TrySwapIfGarbledPair(BoardSlot a, BoardSlot b)
    {
        if (!IsActive || a == null || b == null) return false;
        if (!_slot2set.TryGetValue(a, out var sa)) return false;
        if (!_slot2set.TryGetValue(b, out var sb)) return false;
        if (sa != sb || sa.solved) return false;

        var ta = a.GetLetterTile();
        var tb = b.GetLetterTile();
        if (ta == null || tb == null) return false;

        a.ForcePlaceLetter(tb);
        b.ForcePlaceLetter(ta);

        sa.touched = true;
        SetOutlineColor(sa, _colOutlineTouched);
        return true;
    }
    public void BeginDrag(BoardSlot s)
    {
        if (!IsActive || s == null) { _dragStart = null; return; }
        _dragStart = IsGarbledSlot(s) ? s : null;
    }
    public void EndDrag(BoardSlot s)
    {
        if (!IsActive || _dragStart == null || s == null) { _dragStart = null; return; }
        // สลับได้เฉพาะถ้าอยู่ “ชุดเดียวกัน” และยังไม่ solved
        TrySwapIfGarbledPair(_dragStart, s);
        _dragStart = null;
    }

    /// <summary>สำหรับโหมดคลิก-สลับ (เรียกจาก BoardSlot ก่อนลอจิกอื่น)</summary>
    public bool HandleClickSlot(BoardSlot slot)
    {
        if (!IsActive || !enableClickSwap || slot == null) return false;
        if (!_slot2set.TryGetValue(slot, out var set) || set.solved) return false;

        if (_pendingSwap == null) { _pendingSwap = slot; return true; }          // เลือกครั้งแรก
        if (_pendingSwap == slot) { _pendingSwap = null; return true; }          // คลิกซ้ำ = ยกเลิก

        // ต่างช่อง → ต้องเป็นชุดเดียวกัน
        if (_slot2set.TryGetValue(_pendingSwap, out var set2) && set2 == set)
        {
            var a = _pendingSwap; var b = slot;
            _pendingSwap = null;

            var ta = a.GetLetterTile();
            var tb = b.GetLetterTile();
            if (ta != null && tb != null)
            {
                a.ForcePlaceLetter(tb);
                b.ForcePlaceLetter(ta);
                set.touched = true;
                SetOutlineColor(set, _colOutlineTouched);
            }
            return true;
        }
        else
        {
            _pendingSwap = slot; // ย้าย selection ไปยังอีกชุด
            return true;
        }
    }

    /// <summary>ลบกรอบ/เคลียร์สเตททั้งหมด (เรียกอัตโนมัติใน OnDisable และก่อน Setup รอบใหม่)</summary>
    public void ClearAll()
    {
        for (int i = 0; i < _sets.Count; i++)
        {
            var s = _sets[i];
            if (s != null && s.outline != null) Destroy(s.outline.gameObject);
            if (s != null && s.uiOutline != null) Destroy(s.uiOutline.gameObject);
        }
        _sets.Clear();
        _slot2set.Clear();
        _pendingSwap = null;
        _initializedThisLevel = false;
        IsActive = false;
    }

    // =========================================================
    //                      Internal
    // =========================================================

    void UIToast(string msg, Color c)
    {
        // ไม่ผูกตรงกับ LevelManager เพื่อ decouple — ถ้ามี UIManager ก็ใช้, ไม่งั้น log
        if (UIManager.Instance != null) UIManager.Instance.ShowFloatingToast(msg, c, 2f);
        else Debug.Log(msg);
    }

    string BuildCurrent(GarbledSet set)
    {
        // อ่านตัวอักษรปัจจุบันจากช่อง (uppercase)
        var buf = new char[set.slots.Count];
        for (int i = 0; i < set.slots.Count; i++)
        {
            var t = set.slots[i]?.GetLetterTile();
            var d = (t != null) ? t.GetData() : null;
            buf[i] = (d != null && !string.IsNullOrEmpty(d.letter))
                     ? char.ToUpperInvariant(d.letter[0])
                     : '_';
        }
        return new string(buf);
    }

    bool TryPlaceWordSet(string wordUpper, LevelConfig cfg)
    {
        if (string.IsNullOrEmpty(wordUpper) || wordUpper.Length < 2) return false;

        bool horiz = (Random.value < 0.5f);
        int R = board.rows, C = board.cols;

        for (int tries = 0; tries < cfg.level1_placeMaxRetries; tries++)
        {
            int r = Random.Range(0, R);
            int c = Random.Range(0, C);

            if (horiz)
            {
                if (c + wordUpper.Length > C) continue;
            }
            else
            {
                if (r + wordUpper.Length > R) continue;
            }

            // ตรวจซ้อน/ระยะห่าง
            var slots = new List<BoardSlot>(wordUpper.Length);
            bool ok = true;
            for (int i = 0; i < wordUpper.Length; i++)
            {
                int rr = r + (horiz ? 0 : i);
                int cc = c + (horiz ? i : 0);
                var s = board.GetSlot(rr, cc);
                if (s == null) { ok = false; break; }
                if (s.HasLetterTile()) { ok = false; break; }
                if (!IsFarEnoughFromOtherSets(rr, cc, cfg.level1_minGapBetweenSets)) { ok = false; break; }
                if (!IsOutsideCenterBox(rr, cc, cfg)) { ok = false; break; }
                slots.Add(s);
            }
            if (!ok) continue;

            // วางคำตรงก่อน แล้วค่อยสลับให้มั่ว
            var set = new GarbledSet { target = wordUpper, slots = slots, touched = false, solved = false };
            SpawnLockedWord(set);
            DrawOutline(set, _colOutlineDefault);
            _sets.Add(set);
            for (int i = 0; i < slots.Count; i++) _slot2set[slots[i]] = set;
            return true;
        }
        return false;
    }
    bool IsOutsideCenterBox(int r, int c, LevelConfig cfg)
    {
        if (cfg == null || !cfg.level1_reserveCenterBox) return true;
        if (board == null) return true;

        int half = Mathf.Max(0, cfg.level1_centerBoxHalfExtent); // 1 => 3x3
        if (half == 0) return true;

        int centerR = board.rows / 2;
        int centerC = board.cols / 2;

        // ถ้าบอร์ดเล็กกว่ากรอบที่ต้องการ ให้ยอมผ่าน (กันวางไม่สำเร็จถาวร)
        if (board.rows < half * 2 + 1 || board.cols < half * 2 + 1) return true;

        // นอกกรอบ = ระยะ Chebyshev > half
        return Mathf.Abs(r - centerR) > half || Mathf.Abs(c - centerC) > half;
    }

    bool IsFarEnoughFromOtherSets(int r, int c, int minGap)
    {
        foreach (var kv in _slot2set)
        {
            var s = kv.Key;
            if (s == null) continue;
            int dr = Mathf.Abs(s.row - r);
            int dc = Mathf.Abs(s.col - c);
            if (Mathf.Max(dr, dc) < minGap) return false; // ระยะ Chebyshev
        }
        return true;
    }

    void SpawnLockedWord(GarbledSet set)
    {
        for (int i = 0; i < set.target.Length; i++)
        {
            var slot = set.slots[i];
            var data = FindLetterData(set.target[i]);
            if (data == null) continue;

            var tile = Instantiate(letterTilePrefab);
            tile.Setup(data);
            tile.isLocked = true;

            slot.ForcePlaceLetter(tile);
            slot.SetSpecialBg(_colSlotBg); // BG พิเศษ (ดำ)
        }
        ShuffleTilesInSet(set); // ทำให้ไม่เรียงถูกตั้งแต่แรก
    }

    void ShuffleTilesInSet(GarbledSet set)
    {
        var tiles = new List<LetterTile>(set.slots.Count);
        for (int i = 0; i < set.slots.Count; i++)
            tiles.Add(set.slots[i].GetLetterTile());

        // Fisher–Yates
        for (int i = 0; i < tiles.Count; i++)
        {
            int j = Random.Range(i, tiles.Count);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }

        for (int i = 0; i < tiles.Count; i++)
            set.slots[i].ForcePlaceLetter(tiles[i]);

        // เผื่อสุ่มแล้วตรงพอดี — บิดเล็กน้อย
        if (string.Equals(BuildCurrent(set), set.target, System.StringComparison.Ordinal) && tiles.Count >= 2)
            set.slots[0].SwapWith(set.slots[1]);
    }

    LetterData FindLetterData(char chUpper)
    {
        char u = char.ToUpperInvariant(chUpper);
        for (int i = 0; i < letterDatabase.Length; i++)
        {
            var d = letterDatabase[i];
            if (d == null || string.IsNullOrEmpty(d.letter)) continue;
            if (char.ToUpperInvariant(d.letter[0]) == u) return d;
        }
        return null;
    }

    void DrawOutline(GarbledSet set, Color col)
    {
        // ใช้ UI ถ้า outlineParent เป็น RectTransform
        if (outlineUseUI && outlineParent is RectTransform prt)
        {
            var root = new GameObject($"GarbledOutlineUI_{set.target}", typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.SetParent(prt, false);

            Image MakeEdge(string name)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                img.color = col;
                go.transform.SetParent(rt, false);
                return img;
            }

            // 4 เส้นกรอบ
            var top    = MakeEdge("Top");    top.rectTransform.anchorMin    = new Vector2(0,1);
                                            top.rectTransform.anchorMax    = new Vector2(1,1);
                                            top.rectTransform.sizeDelta    = new Vector2(0, outlineWidthPx);
            var bottom = MakeEdge("Bottom"); bottom.rectTransform.anchorMin = new Vector2(0,0);
                                            bottom.rectTransform.anchorMax = new Vector2(1,0);
                                            bottom.rectTransform.sizeDelta = new Vector2(0, outlineWidthPx);
            var left   = MakeEdge("Left");   left.rectTransform.anchorMin   = new Vector2(0,0);
                                            left.rectTransform.anchorMax   = new Vector2(0,1);
                                            left.rectTransform.sizeDelta   = new Vector2(outlineWidthPx, 0);
            var right  = MakeEdge("Right");  right.rectTransform.anchorMin  = new Vector2(1,0);
                                            right.rectTransform.anchorMax  = new Vector2(1,1);
                                            right.rectTransform.sizeDelta  = new Vector2(outlineWidthPx, 0);

            set.uiOutline = rt;      // ใช้ UI
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            set.outline   = null;    // ไม่ใช้ LineRenderer ในโหมดนี้
            RefreshOutlinePositions(set);
            return;
        }

        // ========== ของเดิม (LineRenderer) ==========
        var go = new GameObject($"GarbledOutline_{set.target}", typeof(LineRenderer));
        if (outlineParent != null) go.transform.SetParent(outlineParent, false);
        var lr = go.GetComponent<LineRenderer>();
        lr.loop = true; lr.positionCount = 4; lr.useWorldSpace = true;
        lr.widthMultiplier = outlineWidth; lr.material = _lineMat;    // เดิม :contentReference[oaicite:3]{index=3}
        set.outline = lr;
        SetOutlineColor(set, col);
        RefreshOutlinePositions(set);
    }

    void SetOutlineColor(GarbledSet set, Color col)
    {
        if (set?.outline != null) { set.outline.startColor = col; set.outline.endColor = col; }
        if (set?.uiOutline != null)
            foreach (var img in set.uiOutline.GetComponentsInChildren<Image>())
                img.color = col;
    }

    void RefreshOutlinePositions(GarbledSet set)
    {
        if (set == null) return;
        set.slots.RemoveAll(s => s == null || s.transform == null);
        if (set.slots.Count == 0)
        {
            if (set.outline   != null) Destroy(set.outline.gameObject);
            if (set.uiOutline != null) Destroy(set.uiOutline.gameObject);
            return;
        }

        // ---- โหมด UI: ใช้มุมของ RectTransform ทุกช่อง ----
        if (set.uiOutline != null && outlineParent is RectTransform prt)
        {
            var canvas = prt.GetComponentInParent<Canvas>();
            var cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? canvas.worldCamera : null;

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            var corners = new Vector3[4];
            for (int i = 0; i < set.slots.Count; i++)
            {
                var srt = set.slots[i].GetComponent<RectTransform>();
                if (srt == null) continue;

                // ดึงมุมโลกของช่อง
                srt.GetWorldCorners(corners);
                for (int k = 0; k < 4; k++)
                {
                    // แปลงโลก -> โลคอลของ Outline Parent
                    Vector2 local;
                    var screen = RectTransformUtility.WorldToScreenPoint(cam, corners[k]);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(prt, screen, cam, out local);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
            }

            // ขยายด้วย padding แบบพิกเซล
            var pad = (float)outlinePaddingPx;
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);

            var size   = max - min;
            var center = (min + max) * 0.5f;

            // ให้กรอบควบคุมด้วย anchoredPosition/sizeDelta (anchors ที่จุดกึ่งกลาง)
            var rt = set.uiOutline;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta        = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
            return;
        }


        // โหมด LineRenderer (เดิม)
    }

    void TryAutoFillLetterDatabaseFromTileBag()
    {
        var bag = tileBagOverride != null ? tileBagOverride : TileBag.Instance;
        if (bag == null) return;

        var list = new List<LetterData>();

        // ใช้ Reflection รองรับชื่อฟิลด์ที่ต่างกันเล็กน้อย
        var fi = bag.GetType().GetField("initialLetters",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi == null) return;

        var entries = fi.GetValue(bag) as System.Collections.IEnumerable;
        if (entries == null) return;

        foreach (var e in entries)
        {
            var fData = e.GetType().GetField("Data",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? e.GetType().GetField("data",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var ld = fData != null ? fData.GetValue(e) as LetterData : null;
            if (ld == null) continue;

            var letter = string.IsNullOrEmpty(ld.letter) ? "" : ld.letter.Trim();
            if (letter.Length == 0 || string.Equals(letter, "Blank", System.StringComparison.OrdinalIgnoreCase))
                continue;

            bool exists = list.Exists(x => string.Equals(x.letter, ld.letter, System.StringComparison.OrdinalIgnoreCase));
            if (!exists) list.Add(ld);
        }

        if (list.Count > 0) letterDatabase = list.ToArray();
    }

    void PruneAllSets()
    {
        // ลบ key ที่ตายใน dictionary
        var deadKeys = new List<BoardSlot>();
        foreach (var k in _slot2set.Keys) if (k == null) deadKeys.Add(k);
        for (int i = 0; i < deadKeys.Count; i++) _slot2set.Remove(deadKeys[i]);

        // ลบ slot ที่ตายในแต่ละ set และกำจัด set ว่าง
        for (int i = _sets.Count - 1; i >= 0; i--)
        {
            var set = _sets[i];
            if (set == null) { _sets.RemoveAt(i); continue; }

            set.slots.RemoveAll(s => s == null);
            if (set.slots.Count == 0)
            {
                if (set.outline != null) Destroy(set.outline.gameObject);
                _sets.RemoveAt(i);
            }
        }
    }
}
