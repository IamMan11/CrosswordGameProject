using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

[DisallowMultipleComponent]
public class ShopCardSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] Image    icon;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Button   buyButton;
    [Header("Slot CanvasGroup (ไว้ลดความทึบตอนซื้อแล้ว)")]
    [SerializeField] CanvasGroup slotGroup;
    [SerializeField, Range(0f,1f)] float purchasedAlpha = 0.45f;

    [Header("Popup / Description (ใช้ Animator แทนการ SetActive)")]
    [SerializeField] GameObject popupRoot;      // ต้องมี Animator + CanvasGroup
    [SerializeField] TMP_Text   popupText;
    [SerializeField] Animator   popupAnimator;  // พารามิเตอร์ Bool: "Show"

    [Header("Animators")]
    [SerializeField] Animator cardAnimator;     // Bool: Hover, Trig: PopOut/PopIn
    [SerializeField] string idleStateName = "Idle";     // ชื่้อ state Idle ใน Animator
    [SerializeField, Range(0f,1f)] float offsetMin = 0f;
    [SerializeField, Range(0f,1f)] float offsetMax = 1f;

    private const string P_BOOL_HOVER = "Hover";
    private const string P_TRIG_OUT   = "PopOut";
    private const string P_TRIG_IN    = "PopIn";
    private const string P_BOOL_SHOW  = "Show";

    private CardData card;
    bool purchasedShown = false;   // โหมดโชว์ว่าเป็นการ์ดที่ซื้อแล้ว (ซีด/นิ่ง)

    void Awake()
    {
        if (!slotGroup) slotGroup = GetComponent<CanvasGroup>();
        if (popupAnimator == null && popupRoot != null) popupRoot.SetActive(false);
    }
    void OnEnable()
    {
        if (cardAnimator)
        {
            float offs = Random.Range(offsetMin, offsetMax);
            cardAnimator.Play(idleStateName, 0, offs);
            cardAnimator.Update(0f);
            cardAnimator.speed = 1f;
        }
        ApplyPurchasedVisual(false); // รีเซ็ตทุกครั้งที่เปิด
    }

    /* ================== External API ================== */

    public void SetCard(CardData data)
    {
        card = data;

        if (card != null)
        {
            if (icon)      icon.sprite = card.icon;
            if (nameText)  nameText.text = card.displayName;
            if (priceText) priceText.text = card.price.ToString("N0");
            if (popupText) popupText.text = card.description;
        }
        else
        {
            if (icon)      icon.sprite = null;
            if (nameText)  nameText.text = "-";
            if (priceText) priceText.text = "-";
            if (popupText) popupText.text = "";
        }

        // รีเซ็ตโหมด “ซื้อแล้ว” (เดี๋ยวค่อยสั่งให้เป็น purchased ถ้าต้องการ)
        ApplyPurchasedVisual(false);

        // ปิด popup เมื่อเปลี่ยนการ์ด
        if (popupAnimator) popupAnimator.SetBool(P_BOOL_SHOW, false);
        else if (popupRoot) popupRoot.SetActive(false);

        UpdateBuyButtonInteractable();
    }

    /// <summary>สั่งให้แสดงผลเหมือน “การ์ดที่ซื้อไปแล้ว” (นิ่งตรง + ซีด + ซื้อไม่ได้)</summary>
    public void ApplyPurchasedVisual(bool on)
    {
        purchasedShown = on;

        // 1) ลด/คืน alpha ทั้งสลอต
        if (slotGroup)
        {
            slotGroup.alpha = on ? purchasedAlpha : 1f;
            slotGroup.interactable = !on;
            slotGroup.blocksRaycasts = true; // ให้ hover ผ่านได้เฉพาะเราเองคุม
        }

        // 2) ทำให้การ์ดนิ่งตรง (หยุด Animator) หรือกลับมาเล่นปกติ
        if (cardAnimator)
        {
            cardAnimator.SetBool(P_BOOL_HOVER, false);
            if (on)
            {
                cardAnimator.Play(idleStateName, 0, 0f);
                cardAnimator.Update(0f);
                cardAnimator.speed = 0f; // หยุด
            }
            else
            {
                cardAnimator.speed = 1f; // เล่นต่อ
                float offs = Random.Range(offsetMin, offsetMax);
                cardAnimator.Play(idleStateName, 0, offs);
                cardAnimator.Update(0f);
            }
        }

        // 3) ปุ่มซื้อ
        if (buyButton) buyButton.interactable = (!on) && CanAfford();
    }

    /// <summary>
    /// ใช้ตอน Reroll: เด้งออก -> เปลี่ยนการ์ด -> เด้งเข้า
    /// showAsPurchased = true จะทำให้เข้าโหมด “ซื้อแล้ว”
    /// </summary>
    public IEnumerator AnimateSwap(CardData newCard, bool showAsPurchased, float outDur = 0.12f, float inDur = 0.14f)
    {
        if (cardAnimator) cardAnimator.SetTrigger(P_TRIG_OUT);
        yield return new WaitForSecondsRealtime(outDur);

        SetCard(newCard);
        ApplyPurchasedVisual(showAsPurchased);

        if (cardAnimator) cardAnimator.SetTrigger(P_TRIG_IN);
        yield return new WaitForSecondsRealtime(inDur);
    }

    /// <summary>
    /// ให้ EventTrigger (ของ Slot และของปุ่ม Buy) เรียกตอน hover เข้า
    /// </summary>
    public void HoverEnter()
    {
        if (purchasedShown) return; // ซื้อแล้วไม่ต้องขยาย/โชว์คำอธิบาย
        if (cardAnimator) cardAnimator.SetBool(P_BOOL_HOVER, true);
        if (popupAnimator) popupAnimator.SetBool(P_BOOL_SHOW, true);
        else if (popupRoot) popupRoot.SetActive(true);
    }
    public void HoverExit()
    {
        if (purchasedShown) return;
        if (cardAnimator) cardAnimator.SetBool(P_BOOL_HOVER, false);
        if (popupAnimator) popupAnimator.SetBool(P_BOOL_SHOW, false);
        else if (popupRoot) popupRoot.SetActive(false);
    }


    /// <summary>
    /// ใช้ตอน Reroll: เด้งออก -> เปลี่ยนการ์ด -> เด้งเข้า (ใช้ Animator Trigger)
    /// </summary>
    public IEnumerator AnimateSwap(CardData newCard, float outDur = 0.12f, float inDur = 0.14f)
    {
        if (cardAnimator) cardAnimator.SetTrigger(P_TRIG_OUT);
        yield return new WaitForSecondsRealtime(outDur);

        SetCard(newCard);

        if (cardAnimator) cardAnimator.SetTrigger(P_TRIG_IN);
        yield return new WaitForSecondsRealtime(inDur);
    }

    /* ---------- Buttons ---------- */

    public void OnBuy()
    {
        if (card == null) return;

        var cm  = CurrencyManager.Instance;
        var sm  = ShopManager.Instance;
        var pso = PlayerProgressSO.Instance;

        if (cm == null || sm == null || pso == null)
        {
            Debug.LogWarning("[ShopCardSlot] Singletons missing.");
            return;
        }

        if (!cm.Spend(card.price))
        {
            sm.ShowToast("เหรียญไม่พอ");
            return;
        }

        pso.AddCard(card.id);
        sm.RefreshUI();

        if (buyButton) buyButton.interactable = false;
        sm.ShowToast($"ซื้อ {card.displayName} สำเร็จ");
    }

    /* ---------- Helpers ---------- */

    void UpdateBuyButtonInteractable()
    {
        if (!buyButton) return;
        buyButton.interactable = (!purchasedShown) && CanAfford();
    }

    bool CanAfford()
    {
        var cm = CurrencyManager.Instance;
        return (cm != null && card != null && cm.Coins >= card.price);
    }

    // IPointer ของสลอต
    public void OnPointerEnter(PointerEventData _) => HoverEnter();
    public void OnPointerExit (PointerEventData _) => HoverExit();
}
