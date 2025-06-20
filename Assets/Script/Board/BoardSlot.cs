using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public enum SlotType { Normal, DoubleLetter, TripleLetter, DoubleWord, TripleWord }

public class BoardSlot : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("UI")]
    public Image bg;                 // พื้นหลัง (Image หลักของ Prefab)
    public Image highlight;          // Image ลูกชื่อ “Highlight” (Raycast Target OFF)

    public LetterTile GetLetterTile()
    {
        // look through *all* children
        foreach (Transform child in transform)
        {
            var lt = child.GetComponent<LetterTile>();
            if (lt != null) return lt;
        }
        return null;        // none found
    }
    [HideInInspector] public int manaGain;   // ← เพิ่มบรรทัดนี้

    [HideInInspector] public int row;
    [HideInInspector] public int col;
    [HideInInspector] public SlotType type = SlotType.Normal;

    public void Setup(int r, int c, SlotType t, int _manaGain)
    {
        row      = r;
        col      = c;
        type     = t;
        manaGain = _manaGain;              // ← เก็บค่าเข้า slot
        ApplyVisual();
    }

    public void ApplyVisual()
    {
        bg.color = type switch
        {
            SlotType.DoubleLetter => new Color32(88, 184, 255, 255),
            SlotType.TripleLetter => new Color32(0, 120, 255, 255),
            SlotType.DoubleWord => new Color32(255, 136, 136, 255),
            SlotType.TripleWord => new Color32(255, 64, 64, 255),
            _ => Color.white
        };
    }

    // ---------- Hover ส่งต่อให้ PlacementManager ----------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (PlacementManager.Instance != null)
            PlacementManager.Instance.HoverSlot(this);
    }
    public void Flash(Color col, int times = 4, float dur = 0.15f)
    {
        StartCoroutine(FlashCo(col, times, dur));
    }

    IEnumerator FlashCo(Color col, int times, float dur)
    {
        highlight.transform.SetAsLastSibling();     // ⬅︎ ย้ายมาวาดบนสุด
        Color c = col; c.a = 0.6f;                  // ⬅︎ ปรับความทึบ (อยากได้เท่าไรลองเล่นดู)

        for (int i = 0; i < times; i++)
        {
            highlight.enabled = true;
            highlight.color = c;
            yield return new WaitForSeconds(dur);

            highlight.enabled = false;
            yield return new WaitForSeconds(dur);
        }
    }
    //:contentReference[oaicite:0]{index=0}&#8203;:contentReference[oaicite:1]{index=1}  

    // ---------- คลิกซ้ายวาง ----------
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // ─── ถ้าอยู่ในโหมด Targeted Flux ให้เรียก Handler ก่อน ───
        if (BoardManager.Instance != null && BoardManager.Instance.targetedFluxRemaining > 0)
        {
            BoardManager.Instance.HandleTargetedFluxClick(row, col);
            return;
        }
        // ───────────────────────────────────────────────────────────────

        // ปุ่มซ้ายปกติ → วางตัวอักษร
        PlacementManager.Instance.TryPlaceFromSlot(this);
    }

    // ---------- ให้ PlacementManager เรียก ----------
    public void ShowPreview(Color color) { highlight.enabled = true; highlight.color = color; }
    public void HidePreview() { highlight.enabled = false; }

    public bool HasLetterTile()
    {
        if (transform.childCount > 1) return true;
        return GetComponentInChildren<LetterTile>() != null;
    }

    // ลบตัวอักษรออกจากช่องและคืนวัตถุ LetterTile
    public LetterTile RemoveLetter()
    {
        var tile = GetLetterTile();
        if (tile == null)          // <–– ป้องกัน NullReference
        return null;
        tile.transform.SetParent(null);
        // อาจเคลียร์ตัวแปรภายใน BoardSlot ถ้ามี
        return tile;
    }

    [HideInInspector] public bool IsLocked = false;

    public void Lock()
    {
        IsLocked = true;
        bg.color = new Color32(120, 120, 120, 255); // สีช่องที่ถูกล็อก
                                                    // คุณอาจ Flash ก็ได้: Flash(Color.black);
    }

}
