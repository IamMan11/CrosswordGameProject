using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ShopCardSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] Image    icon;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Button   buyButton;

    [Header("Popup (วางไว้เหนือ price)")]
    [SerializeField] GameObject popupRoot;   // Panel ของ popup (SetActive false ไว้)
    [SerializeField] TMP_Text   popupText;

    CardData card;

    // ให้ ShopManager เรียกเวลาสุ่มใหม่
    public void SetCard(CardData data)
    {
        card = data;

        if (card != null)
        {
            if (icon)     icon.sprite = card.icon;
            if (nameText) nameText.text = card.displayName;
            if (priceText) priceText.text = card.price.ToString();
            if (popupText) popupText.text = card.description;

            bool owned = PlayerProgressSO.Instance.HasCard(card.id);
            buyButton.interactable = !owned && CurrencyManager.Instance.Has(card.price);
        }
        else
        {
            if (nameText)  nameText.text = "-";
            if (priceText) priceText.text = "-";
            buyButton.interactable = false;
        }

        if (popupRoot) popupRoot.SetActive(false);
    }

    // ผูกปุ่ม Buy ของสลอตนี้ให้เรียกฟังก์ชันนี้
    public void OnBuy()
    {
        if (card == null) return;

        if (!CurrencyManager.Instance.Spend(card.price))
        {
            ShopManager.Instance.ShowToast("เหรียญไม่พอ");
            return;
        }

        // ปลดล็อกการ์ดใบนี้ให้ผู้เล่น (มีใช้ใน CardManager: HasCard())
        PlayerProgressSO.Instance.AddCard(card.id);

        // รีเฟรช coin UI ที่มุมซ้ายทันที
        ShopManager.Instance.RefreshUI();

        // ซื้อสำเร็จแล้ว ปิดปุ่มซื้อไว้
        buyButton.interactable = false;
        ShopManager.Instance.ShowToast($"ซื้อ {card.displayName} สำเร็จ");
    }

    // ===== Hover Popup =====
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (card == null) return;
        if (popupRoot) popupRoot.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (popupRoot) popupRoot.SetActive(false);
    }
}
