using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpecialSlotData
{
    public int row;
    public int col;
    public SlotType type;
}

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    [Header("Board Size")]
    public int rows = 15;
    public int cols = 15;

    [Header("Slot Visual")]
    public float slotSize = 64f;      // ขนาดช่อง
    public float slotGap = 4f;       // ระยะห่างช่อง (0 = ชิดกัน)

    [Header("Board Position (เพิ่ม)")]
    public Vector2 boardOffset = Vector2.zero; // เลื่อนบอร์ด (x=ขวา, y=ขึ้น)

    [Header("Prefabs / Parents")]
    public GameObject boardSlotPrefab;
    public RectTransform boardParent;          // RectTransform เท่านั้น

    [Header("Special Slots")]
    public List<SpecialSlotData> specials = new List<SpecialSlotData>();  // ← เพิ่มตรงนี้


    [System.Serializable]
    public class SpecialSlotData
    {
        public int row;
        public int col;
        public SlotType type;
        [Tooltip("จำนวนมานาที่จะได้เมื่อวางตัวอักษรที่นี่")]
        public int manaGain = 0;
    }
    [HideInInspector] public int manaGain;

    [HideInInspector] public BoardSlot[,] grid;
    public int targetedFluxRemaining = 0;
    // ── เพิ่ม fields สำหรับเก็บ backup ก่อนเปลี่ยนเป็น all-random-special ──
    private List<BackupSlot> backupSlots = new List<BackupSlot>();
    private bool isAllRandomActive = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start() => GenerateBoard();

    // ---------- MAIN ---------- //
    public void GenerateBoard()
    {
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

        // คำนวณจุดเริ่มซ้าย-บน (Pivot อยู่กลาง จึงเลื่อนครึ่งหนึ่ง)
        float startX = -totalW / 2f;
        float startY = totalH / 2f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // หา type พิเศษ
                SlotType st = SlotType.Normal;
                int manaGain = 0;
                foreach (var sp in specials)
                    if (sp.row == r && sp.col == c)
                    {
                        st = sp.type;
                        manaGain = sp.manaGain;      // ← รับค่า manaGain
                        break;
                    }

                // Instantiate
                var go = Instantiate(boardSlotPrefab, boardParent);
                var rt = go.GetComponent<RectTransform>();

                float posX = startX + c * (slotSize + slotGap) + slotSize / 2f;
                float posY = startY - r * (slotSize + slotGap) - slotSize / 2f;
                rt.sizeDelta = new Vector2(slotSize, slotSize);
                rt.anchoredPosition = new Vector2(posX, posY);

                var slot = go.GetComponent<BoardSlot>();
                slot.Setup(r, c, st, manaGain);
                grid[r, c] = slot;
            }
        }
    }
    public void AddRandomSpecialSlots(int count)
    {
        // สร้างลิสต์เก็บพิกัด slot ปกติที่ยังไม่เป็น special
        List<(int r, int c)> normals = new List<(int, int)>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot.type == SlotType.Normal)
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

            // กำหนด special type แบบสุ่ม (ตัวอย่าง: DL, TL, DW, TW)
            SlotType newType;
            int roll = Random.Range(0, 4);
            switch (roll)
            {
                case 0: newType = SlotType.DoubleLetter; break;
                case 1: newType = SlotType.TripleLetter; break;
                case 2: newType = SlotType.DoubleWord; break;
                default: newType = SlotType.TripleWord; break;
            }

            // เปลี่ยน type ใน data และอัพเดตสี
            var slot = grid[rr, cc];
            slot.type = newType;
            slot.ApplyVisual();

            // ถ้าอยากเก็บข้อมูลใน specials list ด้วย ให้เพิ่มโค้ดนี้:
            specials.Add(new SpecialSlotData { row = rr, col = cc, type = newType, manaGain = 0 });
        }
    }
    public void StartTargetedFlux(int count)
    {
        // กำหนดจำนวนช่องที่ต้องเลือก
        targetedFluxRemaining = count;
        // แสดงข้อความแจ้งให้ผู้เล่นคลิก (UIManager ใช้ ShowMessage ธรรมดาได้)
        UIManager.Instance.ShowMessage($"Targeted Flux: เลือก {count} ช่องบนบอร์ด", 2f);
    }
    public void HandleTargetedFluxClick(int row, int col)
    {
        if (targetedFluxRemaining <= 0) return;

        var slot = grid[row, col];
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

            // ถ้าอยากเก็บใน specials list ด้วยก็เพิ่ม data ลงไป
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
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot.HasLetterTile())
                {
                    var tile = slot.RemoveLetter();
                    Destroy(tile.gameObject);
                }
            }
        }
        UIManager.Instance.ShowMessage("BOARD CLEARED!", 1.5f);
    }
    public void ActivateAllRandomSpecial(float duration)
    {
        if (isAllRandomActive) return;

        // เก็บ backup ของทุกช่องปัจจุบัน (row, col, type, manaGain)
        backupSlots.Clear();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                backupSlots.Add(new BackupSlot
                {
                    row = r,
                    col = c,
                    type = slot.type,
                    manaGain = slot.manaGain
                });
            }
        }

        // สุ่มเปลี่ยนทุกช่องใน board เป็น special type ใหม่แบบสุ่ม
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                // สุ่ม special type (DoubleLetter / TripleLetter / DoubleWord / TripleWord)
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
                slot.manaGain = 0; // สมมติช่องพิเศษชั่วคราวไม่ให้มานาเพิ่ม
            }
        }

        isAllRandomActive = true;
        // เริ่ม Coroutine เพื่อ revert หลังผ่านไป duration วินาที
        StartCoroutine(RevertAllRandomSpecialAfter(duration));
        UIManager.Instance.ShowMessage("All Random Special – ทุกช่องกลายเป็นพิเศษชั่วคราว!", 2f);
    }
    private IEnumerator RevertAllRandomSpecialAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        // คืนค่าทุกช่องกลับตาม backup
        foreach (var b in backupSlots)
        {
            var slot = grid[b.row, b.col];
            slot.type = b.type;
            slot.manaGain = b.manaGain;
            slot.ApplyVisual();
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
        if (row < 0 || row >= rows ||
            col < 0 || col >= cols)
            return null;
        return grid[row, col];
    }

    public void LockRandomSlot()
    {
        List<BoardSlot> unlockable = new List<BoardSlot>();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (!slot.IsLocked && !slot.HasLetterTile())
                {
                    unlockable.Add(slot);
                }
            }
        }

        if (unlockable.Count == 0) return;

        var randomSlot = unlockable[Random.Range(0, unlockable.Count)];
        randomSlot.Lock();
        Debug.Log($"🔒 ล็อกช่อง ({randomSlot.row}, {randomSlot.col}) แล้ว");
    }
}
