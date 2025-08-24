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
    void MoveTileToSlot(LetterTile tile, BoardSlot slot)
    {
        tile.transform.SetParent(slot.transform, false);
        tile.transform.localPosition = Vector3.zero;

        // ⬇️ เดิมเคย fix เป็น index 1; เปลี่ยนเป็นอยู่ "บนสุด"
        tile.transform.SetAsLastSibling();

        // กันพลาด: ย้ำให้ไอคอนอยู่ล่างสุดเสมอ (ถ้ามี)
        if (slot.icon != null) slot.icon.transform.SetAsFirstSibling();

        tile.IsInSpace = false;

        RectTransform rtTile = tile.GetComponent<RectTransform>();
        RectTransform rtSlot = slot.GetComponent<RectTransform>();
        rtTile.anchorMin = rtTile.anchorMax = new Vector2(0.5f, 0.5f);
        rtTile.pivot     = new Vector2(0.5f, 0.5f);
        rtTile.sizeDelta = rtSlot.sizeDelta;
        rtTile.localScale = Vector3.one;
        if (tile.GetData().letter == "BLANK")
        {
            var letters = Enumerable.Range('A',26).Select(c=>(char)c+"").ToArray();
            string newL = letters[Random.Range(0,letters.Length)];
            tile.GetData().letter = newL;
            tile.letterText.text  = newL;
            tile.scoreText.text   = "0";  // คะแนน 0 
        }
    }

    public void CancelPlacement()
    {
        foreach (var tile in SpaceManager.Instance.GetPreparedTiles())
            SpaceManager.Instance.RemoveTile(tile);

        foreach (var (tile, _) in lastPlacedTiles)
            SpaceManager.Instance.RemoveTile(tile);

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

    // ---------------- helper ----------------
}
