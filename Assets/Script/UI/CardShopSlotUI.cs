using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ช่องแสดงการ์ดในร้าน (ภาพ/ชื่อ/ราคา + ปุ่มซื้อ)
/// </summary>
[DisallowMultipleComponent]
public class CardShopSlotUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Image    icon;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Button   buyBtn;

    CardData data;

    /// <summary>ตั้งค่าช่องด้วยการ์ด 1 ใบ + สถานะเป็นเจ้าของหรือยัง + callback ซื้อ</summary>
    public void Setup(CardData cd, bool owned, System.Action<CardData> onBuy)
    {
        data = cd;
        if (cd == null)
        {
            if (icon)     icon.sprite = null;
            if (nameText) nameText.text = "-";
            if (priceText) priceText.text = "-";
            if (buyBtn)   buyBtn.interactable = false;
            return;
        }

        if (icon)     icon.sprite = cd.icon;
        if (nameText) nameText.text = cd.displayName;

        if (owned)
        {
            if (priceText) priceText.text = "Owned";
            if (buyBtn)    buyBtn.interactable = false;
            if (buyBtn)    buyBtn.onClick.RemoveAllListeners();
        }
        else
        {
            if (priceText) priceText.text = $"💰 {cd.price}";
            if (buyBtn)
            {
                buyBtn.interactable = true;
                buyBtn.onClick.RemoveAllListeners();
                buyBtn.onClick.AddListener(() => onBuy?.Invoke(cd));
            }
        }
    }
}
