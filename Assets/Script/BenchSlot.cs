using UnityEngine;
using UnityEngine.EventSystems;

public class BenchSlot : MonoBehaviour, IDropHandler
{
    // ถูกเรียกโดย EventSystem เมื่อมี LetterTile ปล่อยลง Slot
    public void OnDrop(PointerEventData eventData)
    {
        GameObject draggedGO = eventData.pointerDrag;
        if (draggedGO == null) return;

        // ตรวจว่าเป็น LetterTile จริงไหม
        LetterTile draggedTile = draggedGO.GetComponent<LetterTile>();
        if (draggedTile == null) return;

        // ถ้า Slot นี้มีลูกอยู่แล้ว → สลับตำแหน่งกัน
        if (transform.childCount > 0)
        {
            Transform existingTile = transform.GetChild(0);

            // ย้าย Tile เดิมกลับไปยังช่องต้นทางของตัวที่ลากมา
            existingTile.SetParent(draggedTile.OriginalParent);
            existingTile.localPosition = Vector3.zero;
        }

        // ย้ายตัวที่ลากมาลง Slot ปลายทาง
        draggedTile.OriginalParent = transform;  // อัปเดต parent ใหม่เพื่อสลับครั้งถัดไป
        draggedGO.transform.SetParent(transform);
        draggedGO.transform.localPosition = Vector3.zero;
    }
}
