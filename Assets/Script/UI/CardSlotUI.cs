using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// พฤติกรรมของช่องการ์ดใน HUD (hover เพื่อโชว์รายละเอียด / drop เพื่อย้ายหรือ fusion)
/// </summary>
[DisallowMultipleComponent]
public class CardSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    public CardData cardInSlot; // UIManager จะอัปเดตให้
    public int slotIndex;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardInSlot != null)
            UICardInfo.Instance?.Show(cardInSlot);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UICardInfo.Instance?.Hide();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;

        var draggedGO = eventData.pointerDrag;
        if (draggedGO == null) return;

        var draggable = draggedGO.GetComponent<CardDraggable>();
        if (draggable == null || draggable.cardData == null) return;

        // ปล่อยใส่ช่องเดิม → ไม่ทำอะไร
        if (draggable.slotIndex == slotIndex) return;

        // ถ้ามีการ์ดอยู่ → ลองฟิวชัน
        if (cardInSlot != null)
        {
            bool ok = CardManager.Instance != null && CardManager.Instance.TryFuseByIndex(draggable.slotIndex, slotIndex);
            if (!ok) UIManager.Instance?.ShowMessage("ไม่สามารถ fusion ได้", 1.2f);
            return;
        }

        // ช่องว่าง → ย้ายการ์ดมาช่องนี้
        CardManager.Instance?.MoveCard(draggable.slotIndex, slotIndex);
    }
}
