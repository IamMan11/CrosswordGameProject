    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.EventSystems;

    public enum Orient { Horizontal, Vertical }

    public class PlacementManager : MonoBehaviour
    {
        public static PlacementManager Instance { get; private set; }

        [Header("Colors")]
        public Color validColor   = new(0,1,0,0.25f);
        public Color invalidColor = new(1,0,0,0.25f);

        [Header("Debug")]
        public bool debug = true;                 // เปิด/ปิด log ใน Inspector

        private BoardSlot startSlot;
        private Orient orient = Orient.Horizontal;
        private readonly List<BoardSlot> currentPreview = new();
        private readonly List<(LetterTile tile, BoardSlot slot)> lastPlacedTiles = new();
        private bool previewIsValid = false;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        void Update()
        {
            // Q สลับแนว
            if (Input.GetKeyDown(KeyCode.Q))
            {
                orient = orient == Orient.Horizontal ? Orient.Vertical : Orient.Horizontal;
                RefreshPreview();
            }
            // ❌ ไม่ต้องเช็กคลิกซ้ายที่นี่อีก คลิกถูกจัดการโดย BoardSlot.OnPointerClick
        }


        // ========== PUBLIC ==========
        public void HoverSlot(BoardSlot slot)
        {
            startSlot = slot;
            RefreshPreview();
        }

        public void CancelPlacement()
        {
            // 1) ใน Space
            foreach (var tile in SpaceManager.Instance.GetPreparedTiles())
                SpaceManager.Instance.RemoveTile(tile);

            // 2) ที่เพิ่งวางบนบอร์ด
            foreach (var (tile, _) in lastPlacedTiles)
                SpaceManager.Instance.RemoveTile(tile);   // RemoveTile มี AdjustSize อยู่แล้ว

            lastPlacedTiles.Clear();
            ClearPreview();
            startSlot = null; 
        }

        public void TryPlaceFromSlot(BoardSlot clickedSlot)
        {
            // ถ้าเมาส์ขยับไปช่องใหม่เร็ว ๆ ให้รีเฟรช preview ให้ตรง
            HoverSlot(clickedSlot);
            TryPlace();
        }

        // ========== PREVIEW ==========
        void RefreshPreview()
        {
            ClearPreview();
            previewIsValid = true;

            if (startSlot == null)          { if(debug) Debug.Log("❌ startSlot == null"); return; }

            List<LetterTile> tiles = SpaceManager.Instance.GetPreparedTiles();
            if (tiles.Count == 0)           { if(debug) Debug.Log("❌ No tiles prepared"); return; }

            if(debug) Debug.Log($"↻ Preview {orient}  start=({startSlot.row},{startSlot.col})  len={tiles.Count}");

            int r0 = startSlot.row;
            int c0 = startSlot.col;

            for (int i = 0; i < tiles.Count; i++)
            {
                int rr = r0 + (orient == Orient.Vertical   ? i : 0);
                int cc = c0 + (orient == Orient.Horizontal ? i : 0);

                if (!InBounds(rr, cc))
                {
                    if(debug) Debug.Log($"   ⛔ OutOfBounds ({rr},{cc})");
                    previewIsValid = false;
                    continue;                       // ไม่เก็บ slot ที่เกินขอบ
                }

                BoardSlot slot = BoardManager.Instance.grid[rr, cc];

                if (slot.HasLetterTile())
                {
                    if (debug) Debug.Log($"   ⛔ Slot occupied ({rr},{cc})");
                    previewIsValid = false;
                }


                currentPreview.Add(slot);
            }

            if (currentPreview.Count != tiles.Count)
            {
                if(debug) Debug.Log($"❌ previewCount({currentPreview.Count}) != tiles({tiles.Count})");
                previewIsValid = false;
            }

            Color c = previewIsValid ? validColor : invalidColor;
            foreach (var s in currentPreview) s.ShowPreview(c);

            if(debug) Debug.Log($"✓ previewIsValid = {previewIsValid}");
        }

        void ClearPreview()
        {
            foreach (var s in currentPreview) s.HidePreview();
            currentPreview.Clear();
        }

        bool InBounds(int r, int c)
            => r >= 0 && r < BoardManager.Instance.rows && c >= 0 && c < BoardManager.Instance.cols;

        // ========== PLACE ==========
        void TryPlace()
        {
            if (!previewIsValid)                 { if(debug) Debug.Log("❌ TryPlace: preview invalid"); return; }

            List<LetterTile> tiles = SpaceManager.Instance.GetPreparedTiles();
            if (tiles.Count != currentPreview.Count)
            {
                if(debug) Debug.Log($"❌ TryPlace: tiles({tiles.Count}) != preview({currentPreview.Count})");
                return;
            }

            if(debug) Debug.Log("✅ Placing tiles...");

            lastPlacedTiles.Clear();            // ⭐ บันทึกชุดล่าสุด
            for (int i = 0; i < tiles.Count; i++)
            {
                LetterTile tile = tiles[i];
                BoardSlot  slot = currentPreview[i];

                // ย้าย
                tile.transform.SetParent(slot.transform, false);
                tile.transform.localPosition = Vector3.zero;
                tile.transform.SetSiblingIndex(1);          // ต่อจาก Highlight
                tile.IsInSpace = false;

                // ปรับขนาดให้พอดีช่อง
                RectTransform rtTile = tile.GetComponent<RectTransform>();
                RectTransform rtSlot = slot.GetComponent<RectTransform>();
                rtTile.anchorMin = rtTile.anchorMax = new Vector2(0.5f, 0.5f);
                rtTile.pivot     = new Vector2(0.5f, 0.5f);
                rtTile.sizeDelta = rtSlot.sizeDelta;
                rtTile.localScale = Vector3.one;

                lastPlacedTiles.Add((tile, slot));          // ⭐ จำไว้สำหรับ Undo
            }
                if (TurnManager.Instance != null && lastPlacedTiles.Count > 0)
                    {
                        TurnManager.Instance.OnTilesPlaced(
                            lastPlacedTiles[0].slot,
                            orient,
                            new List<(LetterTile, BoardSlot)>(lastPlacedTiles)
                        );
                    }


            if(debug) Debug.Log("✓ Placement complete");
            ClearPreview();
            startSlot = null;
        }
    }
