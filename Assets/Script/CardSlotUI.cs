using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>ติดกับวัตถุช่องการ์ด (Button / Image) เพื่อโชว์ข้อมูลเมื่อเอาเมาส์ชี้</summary>
public class CardSlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    // การ์ดที่อยู่ในช่อง (เซ็ตจาก CardManager ตอนอัปเดต UI)
    public CardData cardInSlot;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardInSlot != null)
            UICardInfo.Instance.Show(cardInSlot);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UICardInfo.Instance.Hide();
    }
}
