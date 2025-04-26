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
    public float slotGap  = 4f;       // ระยะห่างช่อง (0 = ชิดกัน)

    [Header("Board Position (เพิ่ม)")]
    public Vector2 boardOffset = Vector2.zero; // เลื่อนบอร์ด (x=ขวา, y=ขึ้น)

    [Header("Prefabs / Parents")]
    public GameObject boardSlotPrefab;
    public RectTransform boardParent;          // RectTransform เท่านั้น

    [Header("Special Slots")]
    public List<SpecialSlotData> specials = new();

    [HideInInspector] public BoardSlot[,] grid;

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
        boardParent.pivot     = new Vector2(0.5f, 0.5f);
        boardParent.anchorMin = boardParent.anchorMax = new Vector2(0.5f, 0.5f);
        boardParent.anchoredPosition = boardOffset;

        // คำนวณจุดเริ่มซ้าย-บน (Pivot อยู่กลาง จึงเลื่อนครึ่งหนึ่ง)
        float startX = -totalW / 2f;
        float startY =  totalH / 2f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // หา type พิเศษ
                SlotType st = SlotType.Normal;
                foreach (var sp in specials)
                    if (sp.row == r && sp.col == c)
                    {
                        st = sp.type;
                        break;
                    }

                // Instantiate
                var go = Instantiate(boardSlotPrefab, boardParent);
                var rt = go.GetComponent<RectTransform>();

                float posX = startX + c * (slotSize + slotGap) + slotSize / 2f;
                float posY = startY - r * (slotSize + slotGap) - slotSize / 2f;
                rt.sizeDelta        = new Vector2(slotSize, slotSize);
                rt.anchoredPosition = new Vector2(posX, posY);

                var slot = go.GetComponent<BoardSlot>();
                slot.Setup(r, c, st);
                grid[r, c] = slot;
            }
        }
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
}
