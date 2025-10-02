using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SpaceSlot
/// - จุดวางตัวอักษรชั่วคราว (พื้นที่ Space ระหว่าง Bench กับ Board)
/// - รับ Drag&Drop จาก LetterTile และแจ้ง SpaceManager ให้จัดช่องว่าง/ขยับเพื่อนบ้าน
/// </summary>
[DisallowMultipleComponent]
public class SpaceSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler
{
    [Header("BG Color")]
    [SerializeField] private Image bg;                    // ✅ ใส่ Image ของพื้นช่อง
    [SerializeField] private Color defaultBg = new(1,1,1,0f);
    /// <summary>เมื่อเมาส์โฮเวอร์ระหว่างลาก ให้ SpaceManager จำตำแหน่งช่องเพื่อขยับ</summary>
    public void SetStateColor(Color c)
    {
        if (bg) bg.color = c;
    }

    public void ClearStateColor()
    {
        if (bg) bg.color = defaultBg;
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        var sm = SpaceManager.Instance;
        if (sm != null && sm.draggingTile != null)
        {
            sm.OnHoverSlot(transform);
            sm.RefreshWordleColorsRealtime();   // ✅ เรียกเมื่อกำลังลากจริงเท่านั้น
        }
    }

    /// <summary>เมื่อปล่อยไทล์ลงช่องนี้</summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;

        var go = eventData.pointerDrag;
        if (!go) return;

        var tile = go.GetComponent<LetterTile>();
        if (tile == null) return;

        var sm = SpaceManager.Instance;

        // 1) ถ้ากำลังลากจริง ให้ "จัดทำช่องว่าง" ณ จุดนี้ก่อน (จะขยับช่องข้าง ๆ ให้พอดี)
        if (sm != null && sm.draggingTile != null)
            sm.EnsureEmptyAt(transform);

        // 2) ถ้ายังมีไทล์ค้างในช่อง (edge-case) ให้เตะไปช่องว่างที่ใกล้ที่สุดก่อน
        if (transform.childCount > 0 && sm != null)
            sm.KickOutExistingToNearestEmpty(transform);

        // 3) วางจริง
        tile.OriginalParent = transform;
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();
        tile.AdjustSizeToParent();
        tile.PlaySettle();

        // 4) อัปเดตปุ่ม Discard ให้ตรงสภาพ
        sm?.UpdateDiscardButton();
        sm?.RefreshWordleColorsRealtime(); 
    }
}
