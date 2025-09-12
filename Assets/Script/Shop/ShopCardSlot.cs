using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// ShopCardSlot
/// - แสดงการ์ดในร้านค้า (ไอคอน/ชื่อ/ราคา) + ปุ่มซื้อ
/// - โชว์ Popup รายละเอียดเมื่อโฮเวอร์
/// - ตัดสินใจ enable ปุ่มซื้อจากเงื่อนไข: ยังไม่เป็นเจ้าของ && เหรียญพอ
///
/// หมายเหตุ: คงการเรียกใช้ Singletons (PlayerProgressSO, CurrencyManager, ShopManager) ตามเดิม
/// </summary>
[DisallowMultipleComponent]
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

    private CardData card;

    void Awake()
    {
        // กันลืม: ซ่อน popup ตั้งต้น
        if (popupRoot) popupRoot.SetActive(false);
    }

    /// <summary>
    /// ให้ ShopManager เรียกเมื่อต้องการตั้งค่าการ์ดในสลอตนี้
    /// จะอัปเดต UI ทั้งหมด + สถานะปุ่มซื้อ
    /// </summary>
    public void SetCard(CardData data)
    {
        card = data;

        if (card != null)
        {
            if (icon)      icon.sprite = card.icon;
            if (nameText)  nameText.text = card.displayName;
            if (priceText) priceText.text = card.price.ToString("N0"); // คั่นหลักพันให้อ่านง่าย
            if (popupText) popupText.text = card.description;

            UpdateBuyButtonInteractable();
        }
        else
        {
            if (icon)      icon.sprite = null;
            if (nameText)  nameText.text = "-";
            if (priceText) priceText.text = "-";
            if (popupText) popupText.text = "";
            if (buyButton) buyButton.interactable = false;
        }

        if (popupRoot) popupRoot.SetActive(false);
    }

    /// <summary>
    /// ผูกกับปุ่ม Buy ของสลอตนี้
    /// - หักเหรียญผ่าน CurrencyManager
    /// - บันทึกการเป็นเจ้าของการ์ดผ่าน PlayerProgressSO
    /// - รีเฟรช UI ผ่าน ShopManager
    /// </summary>
    public void OnBuy()
    {
        if (card == null) return;

        // เช็ก singleton ให้ปลอดภัยก่อน
        var cm  = CurrencyManager.Instance;
        var sm  = ShopManager.Instance;
        var pso = PlayerProgressSO.Instance;

        if (cm == null || sm == null || pso == null)
        {
            Debug.LogWarning("[ShopCardSlot] Singletons ยังไม่พร้อม (Currency/Shop/Progress)");
            return;
        }

        // เหรียญไม่พอ
        if (!cm.Spend(card.price))
        {
            sm.ShowToast("เหรียญไม่พอ");
            return;
        }

        // ปลดล็อกการ์ดใบนี้ให้ผู้เล่น (มีใช้ใน CardManager: HasCard())
        pso.AddCard(card.id);

        // รีเฟรช coin UI ที่มุมซ้ายทันที
        sm.RefreshUI();

        // ซื้อสำเร็จแล้ว ปิดปุ่มซื้อไว้
        if (buyButton) buyButton.interactable = false;

        sm.ShowToast($"ซื้อ {card.displayName} สำเร็จ");
    }

    /// <summary>อัปเดตสถานะปุ่มซื้อ จาก “ยังไม่เป็นเจ้าของ && เหรียญพอ”</summary>
    private void UpdateBuyButtonInteractable()
    {
        if (!buyButton) return;

        bool owned = false;
        bool hasMoney = false;

        var pso = PlayerProgressSO.Instance;
        var cm  = CurrencyManager.Instance;

        if (pso != null && card != null)
            owned = pso.HasCard(card.id);

        if (cm != null && card != null)
            hasMoney = cm.Has(card.price);

        buyButton.interactable = (!owned && hasMoney);
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
