using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ร้านขายการ์ด (สำหรับการ์ดที่ requirePurchase เท่านั้น)
/// - ปุ่ม Reroll จะสุ่มชุดใหม่จากพูลที่ยัง “ไม่ได้เป็นเจ้าของ”
/// - กดซื้อ: หักเหรียญ → เพิ่มการ์ดเข้าคลัง → แจ้งผล และ reroll ใหม่
/// </summary>
[DisallowMultipleComponent]
public class UICardShop : MonoBehaviour
{
    [Header("Slots (ลากใส่เองตามลำดับ)")]
    [SerializeField] List<CardShopSlotUI> slots = new();

    [Header("Reroll")]
    [SerializeField] Button rerollBtn;

    [Header("Message Popup")]
    [SerializeField] GameObject popupPanel;
    [SerializeField] TMP_Text messageText;
    [SerializeField] float displayTime = 2f;
    Coroutine hideCo;

    void Awake()
    {
        if (rerollBtn) rerollBtn.onClick.AddListener(Reroll);

        if (popupPanel) popupPanel.SetActive(false);
        EnsureCardManagerExists();
    }

    void OnEnable() => Reroll();

    /// <summary>ให้แน่ใจว่ามี CardManager ในซีน (รองรับกรณีเข้าหน้านี้ตรง ๆ)</summary>
    void EnsureCardManagerExists()
    {
        if (CardManager.Instance != null) return;

        var go = new GameObject("CardManager (Auto)");
        var cm = go.AddComponent<CardManager>();
        // ใช้ SendMessage เพื่อเรียก private method ตามโค้ดเดิมในโปรเจกต์
        cm.SendMessage("LoadAllCards", SendMessageOptions.DontRequireReceiver);
    }

    /* ---------- Reroll ---------- */
    void Reroll()
    {
        if (CardManager.Instance == null || PlayerProgressSO.Instance == null)
        {
            ShowMessage("ระบบการ์ดยังไม่พร้อม");
            return;
        }
        if (slots == null || slots.Count == 0) return;

        // สร้างพูล: ต้องซื้อก่อน และยังไม่เป็นเจ้าของ
        var pool = CardManager.Instance.allCards
            .Where(cd => cd != null && cd.requirePurchase && !PlayerProgressSO.Instance.HasCard(cd.id))
            .OrderBy(_ => Random.value)
            .ToList();

        // กระจายลงช่อง
        for (int i = 0; i < slots.Count; i++)
        {
            var ui = slots[i];
            if (ui == null) continue;

            if (i < pool.Count)
            {
                ui.gameObject.SetActive(true);
                ui.Setup(pool[i], false, TryBuy);
            }
            else
            {
                ui.gameObject.SetActive(false);
            }
        }

        if (rerollBtn) rerollBtn.interactable = pool.Count > 0;
    }

    /* ---------- ซื้อการ์ด ---------- */
    void TryBuy(CardData cd)
    {
        if (cd == null) return;

        if (CurrencyManager.Instance == null || PlayerProgressSO.Instance == null)
        {
            ShowMessage("ระบบเหรียญ/โปรเกรสยังไม่พร้อม");
            return;
        }

        if (!CurrencyManager.Instance.Spend(cd.price))
        {
            ShowMessage("เหรียญไม่พอ");
            return;
        }

        PlayerProgressSO.Instance.AddCard(cd.id);
        ShowMessage($"ซื้อ {cd.displayName} สำเร็จ!");
        Reroll();
    }

    /* ---------- Popup ---------- */
    void ShowMessage(string msg)
    {
        if (popupPanel == null || messageText == null)
        {
            Debug.LogWarning($"[UICardShop] {msg}");
            return;
        }

        if (hideCo != null) StopCoroutine(hideCo);

        messageText.text = msg;
        popupPanel.SetActive(true);
        hideCo = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayTime);
        if (popupPanel) popupPanel.SetActive(false);
    }
}
