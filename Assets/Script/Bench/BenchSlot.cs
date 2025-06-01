using UnityEngine;
using UnityEngine.EventSystems;

public class BenchSlot : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        GameObject draggedGO = eventData.pointerDrag;
        if (draggedGO == null) return;

        LetterTile draggedTile = draggedGO.GetComponent<LetterTile>();
        if (draggedTile == null) return;

        // ถ้า drag ใส่ช่องเดิมตัวเอง → ไม่ต้องทำอะไร
        if (draggedTile.OriginalParent == transform)
            return;

        // ถ้ามีตัวอักษรอยู่แล้ว → สลับกับตัวที่ลากมา
        if (transform.childCount > 0)
        {
            Transform existingTile = transform.GetChild(0);

            existingTile.SetParent(draggedTile.OriginalParent);
            existingTile.localPosition = Vector3.zero;

            var existingLT = existingTile.GetComponent<LetterTile>();
            if (existingLT != null)
                existingLT.OriginalParent = draggedTile.OriginalParent;
        }

        // ย้ายตัวที่ลากมาลง slot นี้
        draggedTile.OriginalParent = transform;
        draggedGO.transform.SetParent(transform);
        draggedGO.transform.localPosition = Vector3.zero;
        draggedTile.AdjustSizeToParent();  // ปรับขนาดให้พอดีช่อง
    }
}
