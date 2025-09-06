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
/// หมายเหตุ:
/// - คงพฤติกรรมเดิมทั้งหมด (ชื่อเมธอด/ฟิลด์เดิม) แต่เพิ่มกัน NRE/ขอบเขต และคำอธิบาย
/// </summary>
public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Colors")]
    public Color validColor   = new(0, 1, 0, 0.25f);
    public Color invalidColor = new(1, 0, 0, 0.25f);

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

            // ถ้ามี Blank ที่ยังไม่เลือก -> เปิด AlphabetPicker และยกเลิกการวางรอบนี้
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

            if (need < minNewTiles)
                previewIsValid = false; // ให้เห็นตำแหน่ง แต่อย่าวางจริง

            int r = startSlot.row;
            int c = startSlot.col;
            int dr = (orient == Orient.Vertical)   ? 1 : 0;
            int dc = (orient == Orient.Horizontal) ? 1 : 0;

            int placed = 0, steps = 0;
            int maxSteps = boardMgr.rows * boardMgr.cols + 5; // กันลูปผิดพลาด

            while (placed < need && steps < maxSteps)
            {
                if (!InBounds(r, c)) { previewIsValid = false; break; }

                var s = boardMgr.GetSlot(r, c);
                if (s == null) { previewIsValid = false; break; }

                // ไฮไลต์เฉพาะช่องว่างและไม่ถูกล็อก
                if (!s.HasLetterTile() && !s.IsLocked)
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
    /// - สำเร็จ: ล้างพรีวิว, เคลียร์ startSlot, เปิดปุ่ม Confirm
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

        int tileIdx = 0;
        lastPlacedTiles.Clear();

        foreach (var slot in currentPreview)
        {
            if (slot == null) { previewIsValid = false; break; }

            // ข้ามช่องที่มีตัวอยู่แล้ว (เช่น ตัวเก่า)
            if (slot.HasLetterTile()) continue;

            if (tileIdx >= tiles.Count)
            {
                // ปกติไม่ควรเกิด เพราะคำนวณพรีวิวตาม need ไปแล้ว
                previewIsValid = false;
                break;
            }

            var tile = tiles[tileIdx++];
            if (tile == null) { previewIsValid = false; break; }

            MoveTileToSlot(tile, slot);
            lastPlacedTiles.Add((tile, slot));
        }

        // หากยังเหลือไทล์ไม่ได้วาง แสดงว่าวางไม่ครบ
        if (tileIdx != tiles.Count)
        {
            if (debug) Debug.Log("❌ some tiles could not be placed");
            return;
        }

        ClearPreview();
        startSlot = null;
        turnMgr.EnableConfirm();
    }

    /// <summary>
    /// ย้ายไทล์ 1 ตัวไปยังสลอตบนบอร์ด (รวมเสียง/เลเยอร์ไอคอน/สถานะพื้นที่)
    /// </summary>
    void MoveTileToSlot(LetterTile tile, BoardSlot slot)
    {
        tile.FlyTo(slot.transform);                       // อนิเมชันบินเข้า
        SfxPlayer.Play(SfxId.TileSnap);                   // เสียงล็อก

        // ให้ไอคอนพื้น (เช่น Center icon) ลงไปอยู่ใต้สุด ไม่บังไทล์
        if (slot.icon != null) slot.icon.transform.SetAsFirstSibling();

        tile.IsInSpace = false;
    }

    #endregion
    // =====================================================================
}
