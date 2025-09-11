using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระดับบน (Top-level) สำหรับเก็บข้อมูลช่องพิเศษจาก Inspector
/// หมายเหตุ: มีคลาสชื่อ SpecialSlotData ซ้อนอยู่ใน BoardManager ด้วย (เวอร์ชันเดิม)
/// เราคงทั้งสองไว้เพื่อไม่ให้พังความเข้ากันได้ย้อนหลัง
/// </summary>
[System.Serializable]
public class SpecialSlotData
{
    public int row;
    public int col;
    public SlotType type;

    [Tooltip("จำนวนมานาที่จะได้เมื่อวางตัวอักษรที่นี่")]
    public int manaGain = 0;
}

/// <summary>
/// BoardManager
/// - สร้างและดูแลกริด BoardSlot (rows x cols) บน RectTransform เป้าหมาย
/// - รองรับการกำหนด “ช่องพิเศษ” รายจุดจาก Inspector
/// - มีเครื่องมือเสริม: เติม Special แบบสุ่ม, Targeted Flux (เลือกจุดให้เป็นพิเศษ),
///   ล้างบอร์ด, โหมด All-Random-Special ชั่วคราว แล้วคืนค่าเดิม
/// 
/// ข้อควรจำ:
/// - โค้ดคง public fields/methods เดิมทั้งหมดเพื่อไม่ให้กระทบสคริปต์อื่น
/// - เพิ่มคอมเมนต์/เช็ก null/ขอบเขต กัน NRE และพฤติกรรมไม่คาดคิด
/// </summary>
public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    // -------------------- Config: Board --------------------
    [Header("Board Size")]
    [Min(1)] public int rows = 15;
    [Min(1)] public int cols = 15;

    [Header("Slot Visual")]
    [Tooltip("ขนาดช่อง (พิกเซล)")] public float slotSize = 64f;
    [Tooltip("ระยะห่างระหว่างช่อง (0 = ชิดกัน)")] public float slotGap = 4f;

    [Header("Board Position")]
    [Tooltip("เลื่อนตำแหน่งบอร์ดสัมพัทธ์กับกึ่งกลาง (x=ขวา, y=ขึ้น)")]
    public Vector2 boardOffset = Vector2.zero;

    [Header("Prefabs / Parents")]
    [Tooltip("Prefab ของ BoardSlot (ต้องมีคอมโพเนนต์ BoardSlot)")]
    public GameObject boardSlotPrefab;
    [Tooltip("RectTransform ที่จะวางกริดของบอร์ด")]
    public RectTransform boardParent;

    // -------------------- Specials --------------------
    [Header("Special Slots")]
    [Tooltip("กำหนดช่องพิเศษแบบตายตัวจาก Inspector (ใช้คลาสระดับบน)")]
    public List<SpecialSlotData> specials = new List<SpecialSlotData>();

    [Header("Center Slot")]
    [Tooltip("รูปไอคอนสำหรับช่องกึ่งกลาง (ปล่อยว่างได้)")]
    public Sprite centerSlotSprite;

    // (ของเดิม) คลาสซ้อนชื่อเดียวกัน — คงไว้ไม่ลบ เพื่อความเข้ากันได้ย้อนหลัง
    [System.Serializable]
    public class SpecialSlotData
    {
        public int row;
        public int col;
        public SlotType type;

        [Tooltip("จำนวนมานาที่จะได้เมื่อวางตัวอักษรที่นี่")]
        public int manaGain = 0;

        [Tooltip("รูปไอคอนสำหรับช่องพิเศษนี้ (ปล่อยว่างได้)")]
        public Sprite sprite;
    }

    // (ของเดิม) ตัวแปรค้าง—คงไว้เพื่อไม่ให้โค้ดเก่าเสีย
    [HideInInspector] public int manaGain;

    // -------------------- Runtime State --------------------
    [HideInInspector] public BoardSlot[,] grid;

    /// <summary>จำนวนที่ยังต้องเลือกสำหรับโหมด Targeted Flux (0 = ปิด)</summary>
    public int targetedFluxRemaining = 0;

    // Backup สำหรับโหมด All-Random-Special
    private readonly List<BackupSlot> backupSlots = new List<BackupSlot>();
    private bool isAllRandomActive = false;

    // Helper properties (อ่านอย่างเดียว)
    public int RowCount => grid != null ? grid.GetLength(0) : rows;
    public int ColCount => grid != null ? grid.GetLength(1) : cols;

    // -------------------- Unity Lifecycle --------------------
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        GenerateBoard();
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
    #region Board Generation

    /// <summary>
    /// สร้างกริดบอร์ดใหม่ทั้งหมดจากค่ากำหนดปัจจุบัน
    /// - เคลียร์ของเดิมใน boardParent
    /// - คำนวณขนาดบอร์ดรวม (สำหรับ sizeDelta ของ parent)
    /// - วาง BoardSlot เป็นกริด และกำหนดช่องพิเศษ/ไอคอนช่องกลาง
    /// </summary>
    public void GenerateBoard()
    {
        // กันพังถ้าไม่ได้ผูกใน Inspector
        if (boardParent == null || boardSlotPrefab == null)
        {
            Debug.LogError("[BoardManager] Missing boardParent or boardSlotPrefab.");
            return;
        }

        // ทำความสะอาดลูกเดิมทั้งหมด
        foreach (Transform child in boardParent)
            Destroy(child.gameObject);

        grid = new BoardSlot[rows, cols];

        // คำนวณขนาดรวมเพื่อเซ็ต sizeDelta ให้พอดี และวางให้อยู่กึ่งกลาง
        float totalW = cols * slotSize + (cols - 1) * slotGap;
        float totalH = rows * slotSize + (rows - 1) * slotGap;

        boardParent.sizeDelta = new Vector2(totalW, totalH);
        boardParent.pivot = new Vector2(0.5f, 0.5f);
        boardParent.anchorMin = boardParent.anchorMax = new Vector2(0.5f, 0.5f);
        boardParent.anchoredPosition = boardOffset;

        // ตำแหน่งเริ่ม (ซ้าย-บน) เมื่อ pivot อยู่กึ่งกลาง
        float startX = -totalW / 2f;
        float startY =  totalH / 2f;

        // หาศูนย์กลาง (ถ้าเป็นเลขคู่จะปัดลง)
        int centerR = rows / 2;
        int centerC = cols / 2;

        // สร้างกริด
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // ค่าเริ่มต้นของช่อง
                SlotType st = SlotType.Normal;
                int manaGainLocal = 0;
                Sprite overlaySprite = null;

                // ตรวจว่าจุดนี้กำหนดเป็น special ไว้ไหม (ใช้ลิสต์ซึ่งอาจมาจากทั้งสองคลาส)
                // หมายเหตุ: คงลูปเช็คทีละรายการเพื่อคงพฤติกรรมเหมือนเดิม (กรณีมีรายการซ้ำ)
                foreach (var sp in specials)
                {
                    if (sp == null) continue;
                    if (sp.row == r && sp.col == c)
                    {
                        st = sp.type;
                        manaGainLocal = sp.manaGain;
                        // ถ้าผูกรูปไว้ (ในเวอร์ชัน nested class) ให้ใช้ด้วย
                        // ตัวระดับบนไม่มี sprite → จะเป็น null
                        if (sp is BoardManager.SpecialSlotData nested && nested.sprite != null)
                            overlaySprite = nested.sprite;
                        break;
                    }
                }

                // ช่องกลาง (ให้ทับค่ารูปถ้ามี)
                if (r == centerR && c == centerC && centerSlotSprite != null)
                {
                    overlaySprite = centerSlotSprite;
                }

                // สร้าง GameObject ของช่อง
                var go = Instantiate(boardSlotPrefab, boardParent);
                var rt = go.GetComponent<RectTransform>();

                // คำนวณตำแหน่ง Local ให้แต่ละช่อง (อ้างอิงกึ่งกลาง)
                float posX = startX + c * (slotSize + slotGap) + slotSize / 2f;
                float posY = startY - r * (slotSize + slotGap) - slotSize / 2f;

                if (rt != null) // กัน Prefab ไม่มี RectTransform
                {
                    rt.sizeDelta = new Vector2(slotSize, slotSize);
                    rt.anchoredPosition = new Vector2(posX, posY);
                }

                var slot = go.GetComponent<BoardSlot>();
                if (slot == null) // กัน Prefab ผิดชนิด
                {
                    Debug.LogError("[BoardManager] boardSlotPrefab missing BoardSlot component.");
                    Destroy(go);
                    continue;
                }

                // เซ็ตอัพข้อมูลเริ่มต้นของช่อง
                slot.Setup(r, c, st, manaGainLocal, overlaySprite);
                grid[r, c] = slot;
            }
        }
    }

    #endregion
    // ====================================================================== //
    #region Specials: Random / Targeted Flux

    /// <summary>
    /// สุ่มเปลี่ยนช่องปกติให้กลายเป็นพิเศษจำนวน count ช่อง (ไม่ทับช่องพิเศษเดิม)
    /// </summary>
    public void AddRandomSpecialSlots(int count)
    {
        if (grid == null) { Debug.LogWarning("[BoardManager] grid is null."); return; }

        // รวบรวมช่องที่เป็น Normal ทั้งหมด
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

        // ปรับจำนวนไม่ให้เกินปริมาณช่องปกติ
        count = Mathf.Min(count, normals.Count);

        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, normals.Count);
            var (rr, cc) = normals[idx];
            normals.RemoveAt(idx);

            SlotType newType = RandomSpecialType();

            var slot = grid[rr, cc];
            if (slot == null) continue; // กัน null
            slot.type = newType;
            slot.ApplyVisual();

            // บันทึกไว้ใน specials (ใช้ชนิดที่มี manaGain ได้)
            specials.Add(new SpecialSlotData { row = rr, col = cc, type = newType, manaGain = 0 });
        }
    }

    /// <summary>เริ่มโหมด Targeted Flux (ให้ผู้เล่นคลิกเลือกช่องจำนวน count ช่อง)</summary>
    public void StartTargetedFlux(int count)
    {
        targetedFluxRemaining = Mathf.Max(0, count);
        UIManager.Instance?.ShowMessage($"Targeted Flux: เลือก {targetedFluxRemaining} ช่องบนบอร์ด", 2f);
    }

    /// <summary>
    /// ให้เรียกจาก BoardSlot เมื่อคลิก (ในโหมด Targeted Flux)
    /// - เปลี่ยนช่อง Normal ที่ว่าง ให้เป็นพิเศษแบบสุ่ม
    /// - ลดตัวนับ targetedFluxRemaining จนเหลือ 0 จะจบโหมด
    /// </summary>
    public void HandleTargetedFluxClick(int row, int col)
    {
        if (targetedFluxRemaining <= 0) return;
        if (grid == null) return;
        if (!InBounds(row, col)) return;

        var slot = grid[row, col];
        if (slot == null) return;

        if (slot.type == SlotType.Normal && !slot.HasLetterTile())
        {
            SlotType newType = RandomSpecialType();

            slot.type = newType;
            slot.ApplyVisual();
            specials.Add(new SpecialSlotData { row = row, col = col, type = newType, manaGain = 0 });

            targetedFluxRemaining--;
            UIManager.Instance?.ShowMessage($"เลือกช่อง ({row},{col}) เป็น {newType}", 1.5f);

            if (targetedFluxRemaining == 0)
                UIManager.Instance?.ShowMessage("Targeted Flux: เสร็จสิ้นการเลือกช่อง!", 2f);
        }
        else
        {
            UIManager.Instance?.ShowMessage("ช่องนี้ไม่สามารถเปลี่ยนเป็น special ได้", 1.5f);
        }
    }

    #endregion
    // ====================================================================== //
    #region Board Utilities

    /// <summary>
    /// ลบตัวอักษรทุกตัวบนบอร์ด (ไม่ทำลายสลอต) แล้วโชว์ข้อความ
    /// </summary>
    public void CleanSlate()
    {
        if (grid == null) return;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot != null && slot.HasLetterTile())
                {
                    var tile = slot.RemoveLetter();
                    if (tile != null) Destroy(tile.gameObject); // กัน NRE
                }
            }
        }
        UIManager.Instance?.ShowMessage("BOARD CLEARED!", 1.5f);
    }

    /// <summary>
    /// เปลี่ยน “ทุกช่อง” ให้เป็นช่องพิเศษแบบสุ่มชั่วคราว พร้อมแบ็คอัพค่าเดิมไว้
    /// ครบกำหนดเวลาแล้วคืนค่าเดิมทั้งหมด
    /// </summary>
    public void ActivateAllRandomSpecial(float duration)
    {
        if (isAllRandomActive) return;
        if (grid == null) return;

        // เก็บสำรองค่าปัจจุบันทุกช่อง
        backupSlots.Clear();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot == null) continue;
                backupSlots.Add(new BackupSlot
                {
                    row = r,
                    col = c,
                    type = slot.type,
                    manaGain = slot.manaGain
                });
            }
        }

        // เปลี่ยนทุกช่องเป็น special แบบสุ่ม (ไม่แจกมานาในโหมดชั่วคราว)
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot == null) continue;

                slot.type = RandomSpecialType();
                slot.ApplyVisual();
                slot.manaGain = 0;
            }
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
            foreach (var b in backupSlots)
            {
                if (!InBounds(b.row, b.col)) continue;
                var slot = grid[b.row, b.col];
                if (slot == null) continue;

                slot.type = b.type;
                slot.manaGain = b.manaGain;
                slot.ApplyVisual();
            }
        }

        backupSlots.Clear();
        isAllRandomActive = false;
        UIManager.Instance?.ShowMessage("All Random Special หมดเวลา – คืนสภาพเดิมแล้ว", 2f);
    }

    /// <summary>คืน BoardSlot ที่ตำแหน่ง (row,col) ถ้าอยู่นอกขอบจะคืน null</summary>
    public BoardSlot GetSlot(int row, int col)
    {
        if (grid == null) return null;
        if (!InBounds(row, col)) return null;
        return grid[row, col];
    }

    /// <summary>
    /// ล็อก 1 ช่องแบบสุ่มจากช่องที่ยังไม่ล็อกและยังไม่มีตัวอักษร (กัน Null)
    /// </summary>
    public void LockRandomSlot()
    {
        if (grid == null) return;

        List<BoardSlot> unlockable = new List<BoardSlot>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var slot = grid[r, c];
                if (slot != null && !slot.IsLocked && !slot.HasLetterTile())
                    unlockable.Add(slot);
            }
        }

        if (unlockable.Count == 0) return;

        var randomSlot = unlockable[Random.Range(0, unlockable.Count)];
        randomSlot.Lock();
    }

    #endregion
    // ====================================================================== //
    #region Helpers

    private struct BackupSlot
    {
        public int row;
        public int col;
        public SlotType type;
        public int manaGain;
    }

    /// <summary>เช็กตำแหน่งว่าอยู่ในกรอบบอร์ดหรือไม่</summary>
    private bool InBounds(int r, int c) => r >= 0 && r < rows && c >= 0 && c < cols;

    /// <summary>สุ่มชนิดของช่องพิเศษ 1 แบบ (DL/TL/DW/TW) แบบสม่ำเสมอ</summary>
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

    #endregion
}
