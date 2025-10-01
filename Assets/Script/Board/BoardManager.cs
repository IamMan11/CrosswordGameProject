using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//
// Top-level data for special slots (keep only one definition in the project)
//
[System.Serializable]
public class SpecialSlotData
{
    public int row;
    public int col;
    public SlotType type;
    [Tooltip("จำนวนมานาที่จะได้เมื่อวางตัวอักษรที่นี่")] public int manaGain = 0;
    [Tooltip("รูปไอคอนสำหรับช่องนี้ (จะทับไอคอนอัตโนมัติของ DL/TL/DW/TW ถ้ากำหนด)")]
    public Sprite sprite;
}

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    // ---------- Config ----------
    private struct TempSlot
    {
        public int row, col;
        public SlotType prevType;
        public int prevMana;
        public Sprite prevIcon;
    }
    private readonly List<TempSlot> _tempSlotsThisTurn = new();

    [Header("Board Size")]
    [Min(1)] public int rows = 15;
    [Min(1)] public int cols = 15;

    [Header("Slot Visual")]
    [Tooltip("ขนาดช่อง (พิกเซล)")] public float slotSize = 64f;
    [Tooltip("ระยะห่างระหว่างช่อง")] public float slotGap = 4f;

    [Header("Board Position")]
    [Tooltip("เลื่อนตำแหน่งบอร์ดสัมพัทธ์กับกึ่งกลาง (x=ขวา, y=ขึ้น)")]
    public Vector2 boardOffset = Vector2.zero;

    [Header("Prefabs / Parents")]
    public GameObject boardSlotPrefab;
    public RectTransform boardParent;

    [Header("Special Type Sprites (runtime)")]
    public Sprite doubleLetterSprite;
    public Sprite tripleLetterSprite;
    public Sprite doubleWordSprite;
    public Sprite tripleWordSprite;

    [Header("Auto Generate")]
    public bool autoGenerateOnStart = false;

    [Header("Special Slots (fixed)")]
    public List<SpecialSlotData> specials = new();

    [Header("Center Slot")]
    public Sprite centerSlotSprite;

    [Header("Highlight UI")]
    // legacy
    [HideInInspector] public int manaGain;

    // ---------- Runtime ----------
    [HideInInspector] public BoardSlot[,] grid;
    public int targetedFluxRemaining = 0;

    private readonly List<BackupSlot> backupSlots = new();
    private bool isAllRandomActive = false;

    public int RowCount => grid != null ? grid.GetLength(0) : rows;
    public int ColCount => grid != null ? grid.GetLength(1) : cols;

    private Sprite SpriteFor(SlotType t)
    {
        switch (t)
        {
            case SlotType.DoubleLetter: return doubleLetterSprite;
            case SlotType.TripleLetter: return tripleLetterSprite;
            case SlotType.DoubleWord: return doubleWordSprite;
            case SlotType.TripleWord: return tripleWordSprite;
            default: return null;
        }
    }

    // ---------- Unity ----------
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (autoGenerateOnStart) GenerateBoard();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        rows = Mathf.Max(1, rows);
        cols = Mathf.Max(1, cols);
        slotSize = Mathf.Max(1f, slotSize);
        slotGap = Mathf.Max(0f, slotGap);
    }
#endif

    // ====================================================================== //
    //                           Board Generation
    // ====================================================================== //
    public void GenerateBoard()
    {
        if (boardParent == null || boardSlotPrefab == null)
        {
            Debug.LogError("[BoardManager] Missing boardParent or boardSlotPrefab.");
            return;
        }

        foreach (Transform child in boardParent) Destroy(child.gameObject);

        grid = new BoardSlot[rows, cols];

        float totalW = cols * slotSize + (cols - 1) * slotGap;
        float totalH = rows * slotSize + (rows - 1) * slotGap;

        boardParent.sizeDelta = new Vector2(totalW, totalH);
        boardParent.pivot = new Vector2(0.5f, 0.5f);
        boardParent.anchorMin = boardParent.anchorMax = new Vector2(0.5f, 0.5f);
        boardParent.anchoredPosition = boardOffset;

        float startX = -totalW / 2f;
        float startY = totalH / 2f;

        int centerR = rows / 2;
        int centerC = cols / 2;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                SlotType st = SlotType.Normal;
                int manaGainLocal = 0;
                Sprite overlaySprite = null;

                foreach (var sp in specials)
                {
                    if (sp == null) continue;
                    if (sp.row == r && sp.col == c)
                    {
                        st = sp.type;
                        manaGainLocal = sp.manaGain;
                        if (sp.sprite) overlaySprite = sp.sprite;
                        break;
                    }
                }

                if (r == centerR && c == centerC && centerSlotSprite != null)
                    overlaySprite = centerSlotSprite;

                var go = Instantiate(boardSlotPrefab, boardParent);
                var rt = go.GetComponent<RectTransform>();

                float posX = startX + c * (slotSize + slotGap) + slotSize / 2f;
                float posY = startY - r * (slotSize + slotGap) - slotSize / 2f;

                if (rt)
                {
                    rt.sizeDelta = new Vector2(slotSize, slotSize);
                    rt.anchoredPosition = new Vector2(posX, posY);
                }

                var slot = go.GetComponent<BoardSlot>();
                if (!slot)
                {
                    Debug.LogError("[BoardManager] boardSlotPrefab missing BoardSlot component.");
                    Destroy(go);
                    continue;
                }

                slot.Setup(r, c, st, manaGainLocal, overlaySprite);
                grid[r, c] = slot;
            }
    }

    // ====================================================================== //
    //                      Specials: Random / Targeted Flux
    // ====================================================================== //
    public void AddRandomSpecialSlots(int count)
    {
        if (grid == null) { Debug.LogWarning("[BoardManager] grid is null."); return; }

        var normals = new List<(int r, int c)>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (grid[r, c] && grid[r, c].type == SlotType.Normal)
                    normals.Add((r, c));

        count = Mathf.Min(count, normals.Count);

        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, normals.Count);
            var (rr, cc) = normals[idx];
            normals.RemoveAt(idx);

            var slot = grid[rr, cc];
            if (!slot) continue;

            SlotType newType = RandomSpecialType();
            slot.type = newType;
            slot.ApplyVisual();
            slot.SetIcon(SpriteFor(newType));

            specials.Add(new SpecialSlotData { row = rr, col = cc, type = newType, manaGain = 0 });
        }

    }

    public void AddRandomSpecialSlotsTemporary(int count)
    {
        if (grid == null) return;

        var normals = new List<(int r, int c)>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (grid[r, c] && grid[r, c].type == SlotType.Normal)
                    normals.Add((r, c));

        count = Mathf.Min(count, normals.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, normals.Count);
            var (rr, cc) = normals[idx];
            normals.RemoveAt(idx);

            var slot = grid[rr, cc];
            if (!slot) continue;

            _tempSlotsThisTurn.Add(new TempSlot
            {
                row = rr,
                col = cc,
                prevType = slot.type,
                prevMana = slot.manaGain,
                prevIcon = slot.icon ? slot.icon.sprite : null
            });

            var newType = RandomSpecialType();
            slot.type = newType;
            slot.manaGain = 0;
            slot.ApplyVisual();
            slot.SetIcon(SpriteFor(newType));
        }
    }

    public void RevertTempSpecialsThisTurn()
    {
        if (grid == null || _tempSlotsThisTurn.Count == 0) { _tempSlotsThisTurn.Clear(); return; }

        foreach (var t in _tempSlotsThisTurn)
        {
            if (!InBounds(t.row, t.col)) continue;
            var slot = grid[t.row, t.col];
            if (!slot) continue;

            slot.type = t.prevType;
            slot.manaGain = t.prevMana;
            slot.ApplyVisual();
            slot.SetIcon(t.prevIcon);
        }
        _tempSlotsThisTurn.Clear();
    }

    public void StartTargetedFlux(int count)
    {
        targetedFluxRemaining = Mathf.Max(0, count);
        UIManager.Instance?.ShowMessage($"Targeted Flux: เลือก {targetedFluxRemaining} ช่องบนบอร์ด", 2f);
    }

    public void HandleTargetedFluxClick(int row, int col)
    {
        if (targetedFluxRemaining <= 0) return;
        if (grid == null) return;
        if (!InBounds(row, col)) return;

        var slot = grid[row, col];
        if (!slot) return;

        if (slot.type == SlotType.Normal && !slot.HasLetterTile())
        {
            SlotType newType = RandomSpecialType();

            slot.type = newType;
            slot.ApplyVisual();
            slot.SetIcon(SpriteFor(newType));
            specials.Add(new SpecialSlotData { row = row, col = col, type = newType, manaGain = 0 });

            targetedFluxRemaining--;
            UIManager.Instance?.ShowMessage($"เลือกช่อง ({row},{col}) เป็น {newType}", 1.5f);

            if (targetedFluxRemaining == 0)
            {
                UIManager.Instance?.ShowMessage("Targeted Flux: เสร็จสิ้นการเลือกช่อง!", 2f);
            }
        }
        else
        {
            UIManager.Instance?.ShowMessage("ช่องนี้ไม่สามารถเปลี่ยนเป็น special ได้", 1.5f);
        }
    }

    // ====================================================================== //
    //                              Utilities
    // ====================================================================== //
    public void CleanSlate()
    {
        if (grid == null) return;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot != null && slot.HasLetterTile())
                {
                    var tile = slot.RemoveLetter();
                    if (tile) Destroy(tile.gameObject);
                }
            }
        UIManager.Instance?.ShowMessage("BOARD CLEARED!", 1.5f);
    }

    public void ActivateAllRandomSpecial(float duration)
    {
        if (isAllRandomActive) return;
        if (grid == null) return;

        backupSlots.Clear();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (!slot) continue;
                backupSlots.Add(new BackupSlot
                {
                    row = r,
                    col = c,
                    type = slot.type,
                    manaGain = slot.manaGain,
                    iconSprite = slot.icon ? slot.icon.sprite : null
                });
            }

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (!slot) continue;

                slot.type = RandomSpecialType();
                slot.ApplyVisual();
                slot.manaGain = 0;
                slot.SetIcon(SpriteFor(slot.type));
            }

        isAllRandomActive = true;
        StartCoroutine(RevertAllRandomSpecialAfter(duration));
        UIManager.Instance?.ShowMessage("All Random Special – ทุกช่องกลายเป็นพิเศษชั่วคราว!", 2f);
    }

    private IEnumerator RevertAllRandomSpecialAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (grid != null)
        {
            int centerR = rows / 2;
            int centerC = cols / 2;

            foreach (var b in backupSlots)
            {
                if (!InBounds(b.row, b.col)) continue;
                var slot = grid[b.row, b.col];
                if (!slot) continue;

                slot.type = b.type;
                slot.manaGain = b.manaGain;
                slot.ApplyVisual();
                slot.SetIcon(b.iconSprite);

                if (b.type == SlotType.Normal)
                {
                    if (centerSlotSprite && b.row == centerR && b.col == centerC)
                        slot.SetIcon(centerSlotSprite);
                    else
                        slot.SetIcon(null);
                }
                else
                {
                    slot.SetIcon(SpriteFor(b.type));
                }
            }
        }

        backupSlots.Clear();
        isAllRandomActive = false;
        UIManager.Instance?.ShowMessage("All Random Special หมดเวลา – คืนสภาพเดิมแล้ว", 2f);
    }

    public BoardSlot GetSlot(int row, int col)
    {
        if (grid == null) return null;
        if (!InBounds(row, col)) return null;
        return grid[row, col];
    }

    public void LockRandomSlot()
    {
        if (grid == null) return;

        var unlockable = new List<BoardSlot>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot && !slot.IsLocked && !slot.HasLetterTile())
                    unlockable.Add(slot);
            }

        if (unlockable.Count == 0) return;
        var randomSlot = unlockable[Random.Range(0, unlockable.Count)];
        randomSlot.Lock();
    }

    // ====================================================================== //
    //                          Highlight Helpers
    // ====================================================================== //
    // หา RectTransform ของ CardSlot หลายช่อง
    private List<RectTransform> FindCardSlotRects(IEnumerable<int> indicesOrNull = null)
    {
        var list = new List<RectTransform>();

#if UNITY_2023_1_OR_NEWER
    var all = Object.FindObjectsByType<CardSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = GameObject.FindObjectsOfType<CardSlotUI>(true);
#endif

        if (all != null && all.Length > 0)
        {
            if (indicesOrNull == null)
            {
                foreach (var s in all)
                {
                    var rt = s.GetComponent<RectTransform>();
                    if (rt) list.Add(rt);
                }
            }
            else
            {
                var want = new HashSet<int>(indicesOrNull);
                foreach (var s in all)
                {
                    if (!want.Contains(s.slotIndex)) continue;
                    var rt = s.GetComponent<RectTransform>();
                    if (rt) list.Add(rt);
                }
            }
        }
        else
        {
            // Fallback: ชื่อ GameObject เช่น "Cardslot1".."Cardslot4"
            if (indicesOrNull == null)
            {
                for (int i = 1; i <= 8; i++)
                {
                    var go = GameObject.Find($"Cardslot{i}");
                    if (go) { var rt = go.GetComponent<RectTransform>(); if (rt) list.Add(rt); }
                }
            }
            else
            {
                foreach (var idx in indicesOrNull)
                {
                    var go = GameObject.Find($"Cardslot{idx + 1}");
                    if (go) { var rt = go.GetComponent<RectTransform>(); if (rt) list.Add(rt); }
                }
            }
        }

        return list;
    }

    private RectTransform FindCardSlotRect(int idx)
    {
#if UNITY_2023_1_OR_NEWER
    foreach (var s in Object.FindObjectsByType<CardSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        if (s.slotIndex == idx) return s.GetComponent<RectTransform>();
#else
        foreach (var s in GameObject.FindObjectsOfType<CardSlotUI>(true))
            if (s.slotIndex == idx) return s.GetComponent<RectTransform>();
#endif
        // Fallback: ถ้าไม่ได้ใช้ CardSlotUI ให้ลองตามชื่อ GameObject เช่น "Cardslot1".."Cardslot4"
        var go = GameObject.Find($"Cardslot{idx + 1}");
        return go ? go.GetComponent<RectTransform>() : null;
    }

    // ไฮไลท์เป็น “บล็อกสี่เหลี่ยม” และใช้กับ spotlight ได

    public List<RectTransform> CollectCellRectsRect(int r0, int c0, int r1, int c1)
    {
        var list = new List<RectTransform>();
        if (grid == null) return list;
        if (r0 > r1) (r0, r1) = (r1, r0);
        if (c0 > c1) (c0, c1) = (c1, c0);
        r0 = Mathf.Clamp(r0, 0, rows - 1);
        r1 = Mathf.Clamp(r1, 0, rows - 1);
        c0 = Mathf.Clamp(c0, 0, cols - 1);
        c1 = Mathf.Clamp(c1, 0, cols - 1);

        for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
            {
                var slot = grid[r, c]; if (!slot) continue;
                var rt = slot.GetComponent<RectTransform>(); if (rt) list.Add(rt);
            }
        return list;
    }

    // ---------- Helpers ----------
    private struct BackupSlot
    {
        public int row, col;
        public SlotType type;
        public int manaGain;
        public Sprite iconSprite;
    }

    private bool InBounds(int r, int c) => r >= 0 && r < rows && c >= 0 && c < cols;

    private SlotType RandomSpecialType()
    {
        int roll = Random.Range(0, 4);
        switch (roll)
        {
            case 0: return SlotType.DoubleLetter;
            case 1: return SlotType.TripleLetter;
            case 2: return SlotType.DoubleWord;
            default: return SlotType.TripleWord;
        }
    }
}