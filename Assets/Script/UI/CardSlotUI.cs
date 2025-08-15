using UnityEngine;
using UnityEngine.EventSystems;

public class CardSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    public CardData cardInSlot;   // UIManager จะอัปเดตให้
    public int slotIndex;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardInSlot != null) UICardInfo.Instance.Show(cardInSlot);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        UICardInfo.Instance.Hide();
    }

    public void OnDrop(PointerEventData eventData)
    {
        var draggedGO = eventData.pointerDrag;
        if (draggedGO == null) return;

        var draggable = draggedGO.GetComponent<CardDraggable>();
        if (draggable == null || draggable.cardData == null) return;

        // ปล่อยใส่ช่องเดิม → ไม่ทำอะไร
        if (draggable.slotIndex == slotIndex) return;

        // ถ้าช่องนี้มีการ์ดอยู่ → พยายามฟิวชัน
        if (cardInSlot != null)
        {
            bool ok = CardManager.Instance.TryFuseByIndex(draggable.slotIndex, slotIndex);
            if (!ok) UIManager.Instance.ShowMessage("ไม่สามารถ fusion ได้", 1.2f);
            return;
        }

        // ช่องว่าง → แค่ย้ายการ์ดมาช่องนี้
        CardManager.Instance.MoveCard(draggable.slotIndex, slotIndex);
    }
}
