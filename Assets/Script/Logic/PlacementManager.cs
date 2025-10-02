using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

/// <summary>
/// Orientation ของการวางตัวอักษรบนบอร์ด
/// </summary>
public enum Orient { Horizontal, Vertical }

/// <summary>
/// PlacementManager
/// - ดูแลพรีวิวจุดวาง (ไฮไลต์สีเขียว/แดง) จาก "จุดเริ่ม" + แนว (Horiz/Vert)
/// - ตรวจ tile Blank ที่ยังไม่ resolve → เปิดตัวเลือกอักษรก่อน
/// - วางจริง: ย้ายไทล์จาก Space ไปยัง Board ตามพรีวิว และเปิดปุ่ม Confirm
/// - ยกเลิกการวาง: บินไทล์กลับ Bench/Space ตามช่องว่าง
///
/// อัปเดต (2025-09-13):
/// - เพิ่มการ "วางทีละตัว" แบบหน่วงจังหวะ/รอเป็นตัวๆ ผ่านคอร์รูทีน PlacePairsSequentially
/// - ปรับ TryPlace ให้ใช้คอร์รูทีนแทนการวางพร้อมกันทั้งหมด
/// - ยังใช้ LetterTile.FlyTo(...) ในการบิน + ไล่สเกลให้พอดี Boardslot ขณะบิน
/// </summary>
public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Colors")]
    public Color validColor   = new(0, 1, 0, 0.25f);
    public Color invalidColor = new(1, 0, 0, 0.25f);

    [Header("Placement Stagger (ทีละตัว)")]
    [SerializeField] float placeStagger = 0.08f;    // หน่วงก่อนเริ่มตัวถัดไป (วินาที) แบบรัวๆ
    [SerializeField] bool  waitEachTile = false;    // true = รอให้ตัวก่อนหน้า "ลงจอด" (ประมาณจาก flyDurationHint) ก่อนเริ่มตัวถัดไป
    [SerializeField] float flyDurationHint = 0.35f; // เวลาโดยประมาณที่ LetterTile ใช้บิน (ถ้า LetterTile ตั้งค่าไม่เท่ากัน ปรับค่านี้ใน Inspector ได้)

    [Header("Debug")]
    public bool debug = true;

    // จุดเริ่มพรีวิว + แนว
    private BoardSlot startSlot;
    private Orient orient = Orient.Horizontal;

    // พรีวิวปัจจุบัน
    private readonly List<BoardSlot> currentPreview = new();
    private bool previewIsValid = false;

    // ป้องกันรีเฟรชซ้อน (re-entrant)
    bool _isRefreshing = false;

    // เก็บรายการที่ "วางใหม่ในเทิร์นนี้" (เผื่อกดยกเลิก)
    private readonly List<(LetterTile tile, BoardSlot slot)> lastPlacedTiles = new();

    // =====================================================================
    #region Unity

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Update()
    {
        // สลับแนวด้วยปุ่ม Q (ดีบัก/คีย์ลัด)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            orient = orient == Orient.Horizontal ? Orient.Vertical : Orient.Horizontal;
            RefreshPreview();
        }
    }

    #endregion
    // =====================================================================

    #region Public API (จาก BoardSlot / UI)
    public bool TrySwapGarbledPair(BoardSlot fromSlot, BoardSlot toSlot)
    {
        if (Level1GarbledIT.Instance == null) return false;
        return Level1GarbledIT.Instance.TrySwapIfGarbledPair(fromSlot, toSlot);
    }

    /// <summary>
    /// เรียกเมื่อเมาส์โฮเวอร์ BoardSlot (ให้เป็น "จุดเริ่ม" ใหม่ แล้วพรีวิว)
    /// </summary>
    public void HoverSlot(BoardSlot slot)
    {
        if (_isRefreshing) return;        // กัน re-entrant
        if (slot == null) return;
        if (startSlot == slot) return;    // จุดเดิม ไม่ต้องคำนวณซ้ำ

        startSlot = slot;
        RefreshPreview();
    }

    /// <summary>
    /// ยกเลิกการวาง: รวมไทล์ทั้งหมดที่เกี่ยวข้อง แล้วบินกลับช่องว่าง
    /// (ถ้าช่องว่างไม่พอ ใช้ fallback ของ SpaceManager)
    /// </summary>
    public void CancelPlacement()
    {
        var spaceMgr = SpaceManager.Instance;
        if (spaceMgr == null) { ClearPreview(); startSlot = null; lastPlacedTiles.Clear(); return; }

        // 1) ไทล์จาก Space + ไทล์ที่เพิ่งวางลงบอร์ด
        var spaceTiles = spaceMgr.GetPreparedTiles();
        var boardTiles = lastPlacedTiles.Select(p => p.tile).Where(t => t != null).ToList();
        var all = new List<LetterTile>(spaceTiles.Count + boardTiles.Count);
        all.AddRange(spaceTiles);
        all.AddRange(boardTiles);

        // 2) หา Bench slots ว่าง (ผ่านรายการที่ SpaceManager ถือไว้)
        var empties = new List<Transform>();
        if (spaceMgr.benchSlots != null)
        {
            foreach (var t in spaceMgr.benchSlots)
                if (t != null && t.childCount == 0) empties.Add(t);
        }

        // 3) จับคู่แล้วบินกลับ
        int n = Mathf.Min(all.Count, empties.Count);
        for (int i = 0; i < n; i++)
        {
            var tile = all[i];
            var slot = empties[i];
            if (tile != null && slot != null) tile.FlyTo(slot);
        }

        // 4) Fallback (ถ้าช่องว่างไม่พอ)
        for (int i = n; i < all.Count; i++)
            if (all[i] != null) spaceMgr.RemoveTile(all[i]);

        lastPlacedTiles.Clear();
        ClearPreview();
        startSlot = null;
    }

    /// <summary>
    /// กรณีคลิกช่องบนบอร์ดเพื่อวาง: โฮเวอร์→รีเฟรชพรีวิว→พยายามวาง
    /// </summary>
    public void TryPlaceFromSlot(BoardSlot clicked)
    {
        HoverSlot(clicked);
        TryPlace();
    }

    #endregion
    // =====================================================================

    #region Preview

    /// <summary>
    /// คำนวณพรีวิวตำแหน่งที่จะวางตามจุดเริ่ม (startSlot), แนว (orient) และจำนวนไทล์ใน Space
    /// - ถ้ามี Blank ที่ยังไม่ resolve: เปิดตัวเลือกอักษรและ "ไม่วาง" ในรอบนี้
    /// - ไฮไลต์ช่องว่างเท่านั้น (ข้ามช่องที่มีตัวแล้ว) และเซ็ตสี valid/invalid
    /// </summary>
    void RefreshPreview()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            ClearPreview();
            previewIsValid = true;
            if (startSlot == null) return;

            var spaceMgr = SpaceManager.Instance;
            var boardMgr = BoardManager.Instance;
            if (spaceMgr == null || boardMgr == null || boardMgr.grid == null) { previewIsValid = false; return; }

            var tiles = spaceMgr.GetPreparedTiles();
            var unresolved = tiles.FirstOrDefault(t => t && t.IsBlank && !t.IsBlankResolved);
            if (unresolved)
            {
                UIManager.Instance?.ShowMessage("เลือกตัวอักษรให้ไทล์ Blank ก่อนวาง", 1.5f);
                BlankPopup.Show(ch => unresolved.ResolveBlank(ch));
                return;
            }

            int need = tiles.Count;
            if (need == 0) return;

            int minNewTiles = (WordChecker.Instance != null)
                ? Mathf.Max(2, WordChecker.Instance.minWordLength)
                : 2;
            if (need < minNewTiles) previewIsValid = false;

            int r = startSlot.row;
            int c = startSlot.col;
            int dr = (orient == Orient.Vertical) ? 1 : 0;
            int dc = (orient == Orient.Horizontal) ? 1 : 0;

            // ---------- helpers ----------
            bool InBoundsLocal(int rr, int cc)
            {
                return rr >= 0 && rr < boardMgr.rows && cc >= 0 && cc < boardMgr.cols;
            }
            bool IsBlockedCell(int rr, int cc)
            {
                if (!InBoundsLocal(rr, cc)) return true;
                var s0 = boardMgr.GetSlot(rr, cc);
                if (s0 == null) return true;
                if (s0.IsLocked) return true;
                if (Level2Controller.IsTriangleCell(rr, cc)) return true; // ← บล็อกจุด triangle ทุกเลเวล
                return false;
            }
            // --------------------------------

            // ✅ ขยับ “จุดเริ่ม” ถ้าชี้โดนจุด triangle (ทำก่อนสร้างพรีวิว)
            if (Level2Controller.IsTriangleCell(r, c))
            {
                int fr = r + dr, fc = c + dc; // เดินหน้า
                int br = r - dr, bc = c - dc; // ถอยหลัง
                if (!IsBlockedCell(fr, fc)) { r = fr; c = fc; }
                else if (!IsBlockedCell(br, bc)) { r = br; c = bc; }
                else
                {
                    // ไปไม่ได้ทั้งคู่ → ทำพรีวิวเป็น invalid
                    previewIsValid = false;
                }
            }

            // สร้างพรีวิวตั้งแต่ r,c (ที่ถูกขยับแล้ว)
            int placed = 0, steps = 0;
            int maxSteps = boardMgr.rows * boardMgr.cols + 5;
            while (placed < need && steps < maxSteps)
            {
                if (!InBoundsLocal(r, c)) { previewIsValid = false; break; }
                var s = boardMgr.GetSlot(r, c);
                if (s == null) { previewIsValid = false; break; }

                if (!s.HasLetterTile() && !s.IsLocked && !Level2Controller.IsTriangleCell(r, c))
                {
                    currentPreview.Add(s);
                    placed++;
                }

                r += dr; c += dc; steps++;
            }
            if (placed < need) previewIsValid = false;

            var col = previewIsValid ? validColor : invalidColor;
            foreach (var s in currentPreview) s?.ShowPreview(col);
        }
        finally { _isRefreshing = false; }
    }


    /// <summary>ล้างไฮไลต์พรีวิวทั้งหมด</summary>
    public void ClearPreview()
    {
        foreach (var s in currentPreview) s?.HidePreview();
        currentPreview.Clear();
    }

    /// <summary>เช็กขอบเขตบอร์ด</summary>
    bool InBounds(int r, int c)
    {
        var bm = BoardManager.Instance;
        if (bm == null) return false;
        return r >= 0 && r < bm.rows && c >= 0 && c < bm.cols;
    }

    #endregion
    // =====================================================================

    #region Place

    /// <summary>
    /// วางจริง: ย้ายไทล์ตามพรีวิวทั้งหมดไปลงบอร์ด
    /// - หากพรีวิวไม่ valid หรือไทล์ไม่ครบ → ไม่ทำอะไร
    /// - สำเร็จ: ใช้คอร์รูทีนวางทีละตัว จากนั้นล้างพรีวิว, เคลียร์ startSlot, เปิดปุ่ม Confirm
    /// </summary>
    public void TryPlace()
    {
        if (!previewIsValid)
        {
            if (debug) Debug.Log("❌ preview invalid");
            return;
        }

        var spaceMgr = SpaceManager.Instance;
        var turnMgr  = TurnManager.Instance;
        if (spaceMgr == null || turnMgr == null) return;

        List<LetterTile> tiles = spaceMgr.GetPreparedTiles();
        if (tiles == null || tiles.Count == 0 || currentPreview.Count == 0) return;

        // จับคู่ "ไทล์จาก Space" → "สลอตว่างในพรีวิว" ตามลำดับ
        int tileIdx = 0;
        var pairs = new List<(LetterTile tile, BoardSlot slot)>();
        foreach (var slot in currentPreview)
        {
            if (slot == null) { previewIsValid = false; break; }
            if (slot.HasLetterTile()) continue; // ข้ามช่องที่มีตัวอยู่แล้ว (ตัวเก่า)
            if (tileIdx >= tiles.Count) { previewIsValid = false; break; }

            var tile = tiles[tileIdx++];
            if (tile == null) { previewIsValid = false; break; }

            pairs.Add((tile, slot));
        }

        if (!previewIsValid || tileIdx != tiles.Count)
        {
            if (debug) Debug.Log("❌ some tiles could not be placed");
            return;
        }

        // วางแบบ "ทีละตัว" ด้วยคอร์รูทีน
        StartCoroutine(PlacePairsSequentially(pairs));
    }

    /// <summary>
    /// ย้ายไทล์ 1 ตัวไปยังสลอตบนบอร์ด (รวมเสียง/เลเยอร์ไอคอน/สถานะพื้นที่)
    /// </summary>
    void MoveTileToSlot(LetterTile tile, BoardSlot slot)
    {
        tile.FlyTo(slot.transform);                       // อนิเมชันบินเข้า + ไล่สเกลให้พอดีช่อง (ทำใน LetterTile)
        SfxPlayer.Play(SfxId.TileSnap);                   // เสียงล็อก

        // ให้ไอคอนพื้น (เช่น Center icon) ลงไปอยู่ใต้สุด ไม่บังไทล์
        if (slot.icon != null) slot.icon.transform.SetAsFirstSibling();

        tile.IsInSpace = false;
    }

    /// <summary>
    /// วางแบบ "ทีละตัว" ด้วยการหน่วงจังหวะ หรือรอเป็นตัวๆ (ตามตัวเลือกใน Inspector)
    /// </summary>
    private IEnumerator PlacePairsSequentially(List<(LetterTile tile, BoardSlot slot)> pairs)
    {
        var turnMgr = TurnManager.Instance;

        // ✅ ปิดปุ่มไว้ทันทีตั้งแต่จะเริ่มวาง
        if (turnMgr && turnMgr.confirmBtn)
        {
            turnMgr.confirmBtn.interactable = false;
            var cg = turnMgr.confirmBtn.GetComponent<CanvasGroup>();
            if (cg) { cg.interactable = false; cg.blocksRaycasts = false; cg.alpha = 0.45f; }
        }
        lastPlacedTiles.Clear();

        for (int i = 0; i < pairs.Count; i++)
        {
            var (tile, slot) = pairs[i];
            if (tile == null || slot == null) continue;

            MoveTileToSlot(tile, slot);
            lastPlacedTiles.Add((tile, slot));

            if (i < pairs.Count - 1)
            {
                if (waitEachTile)
                    yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, flyDurationHint));
                else
                    yield return new WaitForSecondsRealtime(Mathf.Max(0f, placeStagger));
            }
        }

        // จบแล้วค่อยเคลียร์พรีวิว และเปิด Confirm
        ClearPreview();
        startSlot = null;
        turnMgr?.EnableConfirm();
    }

    #endregion
    // =====================================================================
}
