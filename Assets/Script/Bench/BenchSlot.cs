using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class BenchSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler
{
    /// <summary>
    /// ตอนโฮเวอร์เมาส์เหนือสลอต (ใช้ขณะลาก LetterTile อยู่)
    /// - แจ้ง BenchManager ให้ "ทำช่องว่าง" ที่ตำแหน่งนี้ แล้วเลื่อนเพื่อนบ้านเข้าหาช่องว่าง
    /// - มีผลเฉพาะระหว่างที่ BenchManager.draggingTile != null
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        var bm = BenchManager.Instance;
        if (bm == null) return;
        if (bm.IsLocked) return; 
        if (bm.draggingTile == null) return;   // ไม่ได้ลากอะไรอยู่ → ไม่ต้องเลื่อนแถว

        bm.OnHoverSlot(transform);
    }

    /// <summary>
    /// ตอนปล่อย (Drop) วัตถุลงที่สลอตนี้
    /// ขั้นตอน:
    /// A) สั่งให้สลอตนี้กลายเป็น "ช่องว่าง" (สำหรับกรณีสอดแทรก/เลื่อนแถว)
    /// B) ถ้าสลอตนี้ยังมีของค้างอยู่ ให้เตะไปยังช่องว่างที่ใกล้ที่สุดก่อน (กันชน)
    /// C) วาง LetterTile ลงสลอตนี้ ปรับขนาด + เอฟเฟกต์นิดหน่อย + แจ้ง UI อื่น ๆ
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;

        // วัตถุที่ถูกลากมา
        var go = eventData.pointerDrag;
        if (go == null) return;

        // ต้องเป็น LetterTile เท่านั้น
        var tile = go.GetComponent<LetterTile>();
        if (tile == null) return;

        var bm = BenchManager.Instance;
        if (bm != null && bm.IsLocked) return;

        // A) ทำให้ "สลอตนี้" กลายเป็นช่องว่างก่อน (รองรับกรณีสอดแทรกไทล์กลางแถว)
        if (bm != null && bm.draggingTile != null)
            bm.EnsureEmptyAt(transform);

        // B) กันพลาด: ถ้ายังมีของค้างอยู่ ให้เตะไปยังช่องว่างที่ใกล้ที่สุด
        if (transform.childCount > 0 && bm != null)
            bm.KickOutExistingToNearestEmpty(transform);

        // C) วางลง: ตั้งพ่อแม่/ขนาด/ลำดับเลเยอร์ และเอฟเฟกต์จบการวาง
        tile.OriginalParent = transform;              // จำ parent ปลายทาง (เผื่อระบบอื่นอ้างอิง)
        go.transform.SetParent(transform, false);     // false = ใช้ local space ของสลอต
        go.transform.SetAsLastSibling();              // ให้ไทล์อยู่บนสุดในสลอต (กันบัง)
        tile.AdjustSizeToParent();                    // ปรับให้พอดีกับขนาดสลอต

        // เอฟเฟกต์หยุดนิ่งเล็กน้อยให้ความรู้สึก "วางลง"
        tile.PlaySettle();

        // อัปเดตสถานะปุ่ม discard ของฝั่ง Space (ถ้ามี)
        SpaceManager.Instance?.UpdateDiscardButton();
    }
}
