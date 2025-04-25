using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum SlotType { Normal, DoubleLetter, TripleLetter, DoubleWord, TripleWord }

public class BoardSlot : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("UI")]
    public Image bg;                 // พื้นหลัง (Image หลักของ Prefab)
    public Image highlight;          // Image ลูกชื่อ “Highlight” (Raycast Target OFF)

    [HideInInspector] public int row;
    [HideInInspector] public int col;
    [HideInInspector] public SlotType type = SlotType.Normal;

    public void Setup(int r, int c, SlotType t)
    {
        row = r; col = c; type = t;
        ApplyVisual();
    }

    void ApplyVisual()
    {
        bg.color = type switch
        {
            SlotType.DoubleLetter => new Color32( 88,184,255,255),
            SlotType.TripleLetter => new Color32(  0,120,255,255),
            SlotType.DoubleWord   => new Color32(255,136,136,255),
            SlotType.TripleWord   => new Color32(255, 64, 64,255),
            _                     => Color.white
        };
    }

    // ---------- Hover ส่งต่อให้ PlacementManager ----------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (PlacementManager.Instance != null)
            PlacementManager.Instance.HoverSlot(this);
    }

    // ---------- คลิกซ้ายวาง ----------
    public void OnPointerClick(PointerEventData eventData)
    {
        // เฉพาะปุ่มซ้าย
        if (eventData.button == PointerEventData.InputButton.Left)
            PlacementManager.Instance.TryPlaceFromSlot(this);
    }

    // ---------- ให้ PlacementManager เรียก ----------
    public void ShowPreview(Color color)  { highlight.enabled = true;  highlight.color = color; }
    public void HidePreview()             { highlight.enabled = false; }

    public bool HasLetterTile()
    {
        // childCount > 1  =  มี Highlight + LetterTile
        // หรือถ้าอยากตรวจละเอียดกว่านี้ loop เช็ก component LetterTile ก็ได้
        return transform.childCount > 1;
    }
}
