using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpecialSlotData
{
    public int row;
    public int col;
    public SlotType type;
    // ✅ ใช้กับ specials ได้จริง
    [Tooltip("จำนวนมานาที่จะได้เมื่อวางตัวอักษรที่นี่")]
    public int manaGain = 0;
}

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    [Header("Board Size")]
    public int rows = 15;
    public int cols = 15;

    [Header("Slot Visual")]
    public float slotSize = 64f;      // ขนาดช่อง
    public float slotGap = 4f;        // ระยะห่างช่อง (0 = ชิดกัน)

    [Header("Board Position (เพิ่ม)")]
    public Vector2 boardOffset = Vector2.zero; // เลื่อนบอร์ด (x=ขวา, y=ขึ้น)

    [Header("Prefabs / Parents")]
    public GameObject boardSlotPrefab;
    public RectTransform boardParent;          // RectTransform เท่านั้น

    [Header("Special Slots")]
    public List<SpecialSlotData> specials = new List<SpecialSlotData>();  // ← ใช้ชนิดเดียวกับคลาสซ้อนด้านล่าง

    // ⬇️ เพิ่ม Sprite ช่องกลาง (เผื่ออนาคต)
    [Header("Center Slot")]
    public Sprite centerSlotSprite;

    // ----- (ของเดิม) คลาสซ้อนชื่อเดียวกัน — คงไว้ไม่ลบ -----
    [System.Serializable]
    public class SpecialSlotData
    {
        public int row;
        public int col;
        public SlotType type;
        [Tooltip("จำนวนมานาที่จะได้เมื่อวางตัวอักษรที่นี่")]
        public int manaGain = 0;
        // ⬇️ รูปสำหรับช่องพิเศษรายช่อง (ปล่อยว่างได้)
        [Tooltip("รูปไอคอนสำหรับช่องพิเศษนี้ (ปล่อยว่างได้)")]
        public Sprite sprite;
    }
    [HideInInspector] public int manaGain; // (ของเดิม) คงไว้

    [HideInInspector] public BoardSlot[,] grid;
    public int targetedFluxRemaining = 0;

    // ── backup สำหรับโหมด All-Random-Special ──
    private List<BackupSlot> backupSlots = new List<BackupSlot>();
    private bool isAllRandomActive = false;

    // ✅ helper properties สำหรับระบบด่าน/เทิร์น
    public int RowCount => grid != null ? grid.GetLength(0) : rows;
    public int ColCount => grid != null ? grid.GetLength(1) : cols;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start() => GenerateBoard();

    // ---------- MAIN ---------- //
    public void GenerateBoard()
    {
        // ✅ กันพังถ้าไม่ได้ผูกใน Inspector
        if (boardParent == null || boardSlotPrefab == null)
        {
            Debug.LogError("[BoardManager] Missing boardParent or boardSlotPrefab.");
            return;
        }

        // เคลียร์เก่า
        foreach (Transform child in boardParent) Destroy(child.gameObject);

        grid = new BoardSlot[rows, cols];

        float totalW = cols * slotSize + (cols - 1) * slotGap;
        float totalH = rows * slotSize + (rows - 1) * slotGap;

        // ตั้งขนาด parent ให้พอดี และจัดกึ่งกลาง
        boardParent.sizeDelta = new Vector2(totalW, totalH);
        boardParent.pivot = new Vector2(0.5f, 0.5f);
        boardParent.anchorMin = boardParent.anchorMax = new Vector2(0.5f, 0.5f);
        boardParent.anchoredPosition = boardOffset;

        // คำนวณจุดเริ่มซ้าย-บน (Pivot อยู่กลาง)
        float startX = -totalW / 2f;
        float startY =  totalH / 2f;

        int centerR = rows / 2; // ถ้าเป็นเลขคู่จะได้ศูนย์กลางแบบปัดลง
        int centerC = cols / 2;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // ค่าเริ่มต้น
                SlotType st = SlotType.Normal;
                int manaGainLocal = 0;
                Sprite overlaySprite = null; // เผื่ออนาคต (ตอนนี้ยังไม่ส่งเข้า Setup)

                // ช่องพิเศษจากลิสต์
                foreach (var sp in specials)
                {
                    if (sp.row == r && sp.col == c)
                    {
                        st = sp.type;
                        manaGainLocal = sp.manaGain;
                        if (sp.sprite != null) overlaySprite = sp.sprite;
                        break;
                    }
                }

                // ช่องกลาง (ให้ทับค่ารูปถ้ามี)
                if (r == centerR && c == centerC && centerSlotSprite != null)
                {
                    overlaySprite = centerSlotSprite;
                }

                var go = Instantiate(boardSlotPrefab, boardParent);
                var rt = go.GetComponent<RectTransform>();

                float posX = startX + c * (slotSize + slotGap) + slotSize / 2f;
                float posY = startY - r * (slotSize + slotGap) - slotSize / 2f;
                if (rt != null) // ✅ กัน prefab ไม่มี RectTransform (กรณีผิดพลาด)
                {
                    rt.sizeDelta = new Vector2(slotSize, slotSize);
                    rt.anchoredPosition = new Vector2(posX, posY);
                }

                var slot = go.GetComponent<BoardSlot>();
                if (slot == null) // ✅ กัน prefab ผิดชนิด
                {
                    Debug.LogError("[BoardManager] boardSlotPrefab missing BoardSlot component.");
                    Destroy(go);
                    continue;
                }

                // ✅ ยึดซิกเนเจอร์เดิมเพื่อไม่พังไฟล์อื่น
                slot.Setup(r, c, st, manaGainLocal);
                grid[r, c] = slot;
            }
        }
    }

    public void AddRandomSpecialSlots(int count)
    {
        // ✅ กัน grid ยังไม่ถูกสร้าง
        if (grid == null) { Debug.LogWarning("[BoardManager] grid is null."); return; }

        // สร้างลิสต์เก็บพิกัด slot ปกติที่ยังไม่เป็น special
        List<(int r, int c)> normals = new List<(int, int)>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot != null && slot.type == SlotType.Normal)
                    normals.Add((r, c));
            }
        }

        // ถ้าช่องปกติน้อยกว่าที่จะสุ่ม ให้ลด count ลง
        count = Mathf.Min(count, normals.Count);
        for (int i = 0; i < count; i++)
        {
            // เลือก index สุ่มใน normals
            int idx = Random.Range(0, normals.Count);
            var (rr, cc) = normals[idx];
            normals.RemoveAt(idx);

            // กำหนด special type แบบสุ่ม (DL/TL/DW/TW)
            SlotType newType;
            int roll = Random.Range(0, 4);
            switch (roll)
            {
                case 0: newType = SlotType.DoubleLetter; break;
                case 1: newType = SlotType.TripleLetter; break;
                case 2: newType = SlotType.DoubleWord; break;
                default: newType = SlotType.TripleWord; break;
            }

            // เปลี่ยน type และอัปเดตสี
            var slot = grid[rr, cc];
            if (slot == null) continue; // ✅ กัน null
            slot.type = newType;
            slot.ApplyVisual();

            // ✅ ใช้ชนิดเดียวกับลิสต์ (คลาสซ้อน)
            specials.Add(new SpecialSlotData { row = rr, col = cc, type = newType, manaGain = 0 });
        }
    }

    public void StartTargetedFlux(int count)
    {
        targetedFluxRemaining = count;
        UIManager.Instance.ShowMessage($"Targeted Flux: เลือก {count} ช่องบนบอร์ด", 2f);
    }

    public void HandleTargetedFluxClick(int row, int col)
    {
        if (targetedFluxRemaining <= 0) return;
        if (grid == null) return; // ✅
        if (row < 0 || row >= RowCount || col < 0 || col >= ColCount) return; // ✅

        var slot = grid[row, col];
        if (slot == null) return; // ✅

        // ถ้าช่องนี้ยังเป็น Normal (ไม่ใช่ special)
        if (slot.type == SlotType.Normal && !slot.HasLetterTile())
        {
            // เลือก special type แบบสุ่ม
            SlotType newType;
            int roll = Random.Range(0, 4);
            switch (roll)
            {
                case 0: newType = SlotType.DoubleLetter; break;
                case 1: newType = SlotType.TripleLetter; break;
                case 2: newType = SlotType.DoubleWord; break;
                default: newType = SlotType.TripleWord; break;
            }

            slot.type = newType;
            slot.ApplyVisual();

            // ✅ ใช้ชนิดเดียวกับลิสต์ (คลาสซ้อน)
            specials.Add(new SpecialSlotData { row = row, col = col, type = newType, manaGain = 0 });

            targetedFluxRemaining--;
            UIManager.Instance.ShowMessage($"เลือกช่อง ({row},{col}) เป็น {newType}", 1.5f);

            // ถ้าครบแล้ว ปิดโหมด
            if (targetedFluxRemaining == 0)
            {
                UIManager.Instance.ShowMessage("Targeted Flux: เสร็จสิ้นการเลือกช่อง!", 2f);
            }
        }
        else
        {
            UIManager.Instance.ShowMessage("ช่องนี้ไม่สามารถเปลี่ยนเป็น special ได้", 1.5f);
        }
    }

    public void CleanSlate()
    {
        if (grid == null) return; // ✅

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot != null && slot.HasLetterTile())
                {
                    var tile = slot.RemoveLetter();
                    if (tile != null) // ✅ กัน NRE
                        Destroy(tile.gameObject);
                }
            }
        }
        UIManager.Instance.ShowMessage("BOARD CLEARED!", 1.5f);
    }

    public void ActivateAllRandomSpecial(float duration)
    {
        if (isAllRandomActive) return;
        if (grid == null) return; // ✅

        // เก็บ backup ของทุกช่อง (row, col, type, manaGain)
        backupSlots.Clear();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot == null) continue; // ✅
                backupSlots.Add(new BackupSlot
                {
                    row = r,
                    col = c,
                    type = slot.type,
                    manaGain = slot.manaGain
                });
            }
        }

        // สุ่มเปลี่ยนทุกช่องเป็น special ใหม่
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot == null) continue; // ✅

                SlotType newType;
                int roll = Random.Range(0, 4);
                switch (roll)
                {
                    case 0: newType = SlotType.DoubleLetter; break;
                    case 1: newType = SlotType.TripleLetter; break;
                    case 2: newType = SlotType.DoubleWord; break;
                    default: newType = SlotType.TripleWord; break;
                }
                slot.type = newType;
                slot.ApplyVisual();
                slot.manaGain = 0; // โหมดชั่วคราวไม่แจกมานา
            }
        }

        isAllRandomActive = true;
        StartCoroutine(RevertAllRandomSpecialAfter(duration));
        UIManager.Instance.ShowMessage("All Random Special – ทุกช่องกลายเป็นพิเศษชั่วคราว!", 2f);
    }

    private IEnumerator RevertAllRandomSpecialAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (grid != null) // ✅
        {
            // คืนค่าทุกช่องกลับตาม backup
            foreach (var b in backupSlots)
            {
                if (b.row < 0 || b.row >= RowCount || b.col < 0 || b.col >= ColCount) continue; // ✅
                var slot = grid[b.row, b.col];
                if (slot == null) continue; // ✅
                slot.type = b.type;
                slot.manaGain = b.manaGain;
                slot.ApplyVisual();
            }
        }
        backupSlots.Clear();
        isAllRandomActive = false;
        UIManager.Instance.ShowMessage("All Random Special หมดเวลา – คืนสภาพเดิมแล้ว", 2f);
    }

    // ── struct ช่วยเก็บ backup แต่ละช่อง ──
    private struct BackupSlot
    {
        public int row;
        public int col;
        public SlotType type;
        public int manaGain;
    }

    /// <summary>
    /// คืน BoardSlot ตามแถวและคอลัมน์ (ถ้าอยู่นอกขอบจะคืน null)
    /// </summary>
    public BoardSlot GetSlot(int row, int col)
    {
        if (grid == null) return null; // ✅
        if (row < 0 || row >= rows ||
            col < 0 || col >= cols)
            return null;
        return grid[row, col];
    }

    public void LockRandomSlot()
    {
        if (grid == null) return; // ✅

        List<BoardSlot> unlockable = new List<BoardSlot>();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot != null && !slot.IsLocked && !slot.HasLetterTile()) // ✅ กัน null
                {
                    unlockable.Add(slot);
                }
            }
        }

        if (unlockable.Count == 0) return;

        var randomSlot = unlockable[Random.Range(0, unlockable.Count)];
        randomSlot.Lock();
    }
}
