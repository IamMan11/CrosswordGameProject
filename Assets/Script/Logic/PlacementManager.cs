using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

public enum Orient { Horizontal, Vertical }

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Colors")]
    public Color validColor   = new(0,1,0,0.25f);
    public Color invalidColor = new(1,0,0,0.25f);

    [Header("Debug")]
    public bool debug = true;

    private BoardSlot startSlot;
    private Orient orient = Orient.Horizontal;

    // preview
    private readonly List<BoardSlot> currentPreview = new();
    private bool previewIsValid = false;
    bool _isRefreshing = false;

    // สำหรับ Undo (ในโหมดเล่นคนเดียวอาจไม่ใช้ แต่เผื่อไว้)
    private readonly List<(LetterTile tile, BoardSlot slot)> lastPlacedTiles = new();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            orient = orient == Orient.Horizontal ? Orient.Vertical : Orient.Horizontal;
            RefreshPreview();
        }
    }

    // ---------------- PUBLIC API ----------------
    public void HoverSlot(BoardSlot slot)
    {
        if (_isRefreshing) return;        // กัน re-entrant
        if (startSlot == slot) return;    // เมาส์อยู่ช่องเดิม ไม่ต้องคำนวณซ้ำ
        startSlot = slot;
        RefreshPreview();
    }

    public void CancelPlacement()
    {
        // 1) รวบรวม tile ทั้งหมดที่ต้องคืน (Space + ที่เพิ่งวางลงบอร์ดในเทิร์นนี้)
        var spaceTiles = SpaceManager.Instance.GetPreparedTiles();                  // จาก Space
        var boardTiles = lastPlacedTiles.Select(p => p.tile).ToList();              // ที่เพิ่งย้ายไปบอร์ด
        var all = new List<LetterTile>(spaceTiles.Count + boardTiles.Count);
        all.AddRange(spaceTiles);
        all.AddRange(boardTiles);

        // 2) หา bench slots ว่างจากซ้าย→ขวา
        var empties = new List<Transform>();
        foreach (var t in SpaceManager.Instance.benchSlots)                         // อาศัยลิสต์ Bench ของ SpaceManager
            if (t.childCount == 0) empties.Add(t);

        // 3) จับคู่ 1:1 แล้ว "บินกลับ"
        int n = Mathf.Min(all.Count, empties.Count);
        for (int i = 0; i < n; i++)
        {
            var tile = all[i];
            var slot = empties[i];
            tile.FlyTo(slot);                                                       // <<< อนิเมชันบินกลับ
        }

        // 4) Fallback (กรณีพิเศษถ้าช่องว่างไม่พอ)
        for (int i = n; i < all.Count; i++)
            SpaceManager.Instance.RemoveTile(all[i]);                               // snap กลับแบบเดิม

        lastPlacedTiles.Clear();
        ClearPreview();
        startSlot = null;
    }


    public void TryPlaceFromSlot(BoardSlot clicked)
    {
        HoverSlot(clicked);   // รีเฟรช preview ให้ตรงตำแหน่งล่าสุด
        TryPlace();
    }

    // =========================================================
    //                      PREVIEW LOGIC
    // =========================================================
    void RefreshPreview()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            ClearPreview();
            previewIsValid = true;
            if (startSlot == null) return;

            var tiles = SpaceManager.Instance.GetPreparedTiles();

            // ถ้ามี Blank ที่ยังไม่เลือก ให้เปิด AlphabetPicker แล้ว “ยกเลิกการวาง” รอบนี้
            var unresolved = tiles.FirstOrDefault(t => t && t.IsBlank && !t.IsBlankResolved);
            if (unresolved)
            {
                UIManager.Instance.ShowMessage("เลือกตัวอักษรให้ไทล์ Blank ก่อนวาง", 1.5f);
                BlankPopup.Show(ch => unresolved.ResolveBlank(ch));  // ← เปลี่ยนที่นี่
                return;
            }
            int need = tiles.Count;
            if (need == 0) return;

            int r = startSlot.row;
            int c = startSlot.col;
            int dr = (orient == Orient.Vertical)   ? 1 : 0;
            int dc = (orient == Orient.Horizontal) ? 1 : 0;

            int placed = 0, steps = 0;
            int maxSteps = BoardManager.Instance.rows * BoardManager.Instance.cols + 5;

            while (placed < need && steps < maxSteps)
            {
                if (!InBounds(r, c)) { previewIsValid = false; break; }

                var s = BoardManager.Instance.GetSlot(r, c);

                // ไฮไลต์เฉพาะช่องว่าง (ข้ามช่องที่มีตัวอักษรแล้ว)
                if (!s.HasLetterTile() && !s.IsLocked)
                {
                    currentPreview.Add(s);
                    placed++;
                }

                r += dr; c += dc; steps++; // เดินไปข้างหน้าทุกครั้ง
            }

            if (placed < need) previewIsValid = false;

            var col = previewIsValid ? validColor : invalidColor;
            foreach (var s in currentPreview) s.ShowPreview(col);
        }
        finally { _isRefreshing = false; }
    }


    public void ClearPreview()
    {
        foreach (var s in currentPreview) s.HidePreview();
        currentPreview.Clear();
    }

    bool InBounds(int r, int c)
        => r >= 0 && r < BoardManager.Instance.rows &&
           c >= 0 && c < BoardManager.Instance.cols;

    // =========================================================
    //                       PLACE LOGIC
    // =========================================================
    public void TryPlace()
    {
        if (!previewIsValid)                     { if(debug) Debug.Log("❌ preview invalid"); return; }

        List<LetterTile> tiles = SpaceManager.Instance.GetPreparedTiles();
        int tileIdx = 0;

        lastPlacedTiles.Clear();

        foreach (var slot in currentPreview)
        {
            // ช่องนี้มีตัวอยู่แล้ว → ข้าม
            if (slot.HasLetterTile()) continue;

            if (tileIdx >= tiles.Count)
            {   // ปกติไม่ควรเกิดเพราะเช็ก quota ไปแล้ว
                previewIsValid = false;
                break;
            }

            LetterTile tile = tiles[tileIdx++];
            MoveTileToSlot(tile, slot);
            lastPlacedTiles.Add((tile, slot));
        }

        if (tileIdx != tiles.Count)
        {   // วางได้ไม่ครบ
            if(debug) Debug.Log("❌ some tiles could not be placed");
            return;
        }

        ClearPreview();
        startSlot = null;
        
        TurnManager.Instance.EnableConfirm();
    }
    void MoveTileToSlot(LetterTile tile, BoardSlot slot)
    {
        // บินเข้าไป
        tile.FlyTo(slot.transform);

        SfxPlayer.Play(SfxId.TileSnap);   // ★ เสียงล็อกไทล์ลงบอร์ด (คลิกวาง)

        // กันไอคอนบังไทล์ + เคลียร์สถานะ
        if (slot.icon != null) slot.icon.transform.SetAsFirstSibling();
        tile.IsInSpace = false;
    }

    // ---------------- helper ----------------
}
