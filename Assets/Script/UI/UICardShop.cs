using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UICardShop : MonoBehaviour
{
    [Header("Slots ที่ลากใส่เอง")]
    [SerializeField] List<CardShopSlotUI> slots = new();

    [Header("ปุ่ม Reroll")]
    [SerializeField] Button rerollBtn;
    [Header("Message Popup")]
    [SerializeField] GameObject popupPanel;   // ลาก Panel ที่ซ่อนอยู่
    [SerializeField] TMP_Text messageText;  // ลาก Text ข้างใน Panel
    [SerializeField] float displayTime = 2f;
    Coroutine hideCo;

    void Awake()
    {
        if (rerollBtn) rerollBtn.onClick.AddListener(Reroll);

        // 🆕 ตรวจ & Bootstrap CardManager
        if (popupPanel) popupPanel.SetActive(false);
        EnsureCardManagerExists();
    }
    void EnsureCardManagerExists()
    {
        if (CardManager.Instance != null) return;

        var go = new GameObject("CardManager (Auto)");
        var cm = go.AddComponent<CardManager>();

        cm.SendMessage("LoadAllCards");   // หรือเปลี่ยน LoadAllCards() ให้เป็น public แล้วเรียกตรง ๆ
    }
    void OnEnable() => Reroll();

    /* ---------- Reroll ---------- */
    void Reroll()
    {
        Debug.Log($"► allCards = {CardManager.Instance.allCards.Count}");
        // กรองการ์ดที่ "ยังไม่ได้ซื้อ"
        var pool = CardManager.Instance.allCards
                .Where(cd => cd.requirePurchase &&
                             !PlayerProgressSO.Instance.HasCard(cd.id))
                .OrderBy(_ => Random.value)
                .ToList();
        Debug.Log($"► pool (ขายได้) = {pool.Count}");

        // เติมการ์ดลงช่อง
        for (int i = 0; i < slots.Count; i++)
        {
            if (i < pool.Count)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].Setup(pool[i], false, TryBuy);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        // ถ้าไม่มีการ์ดเหลือขาย ปิด Reroll
        if (rerollBtn) rerollBtn.interactable = pool.Count > 0;
    }

    /* ---------- ซื้อการ์ด ---------- */
    void TryBuy(CardData cd)
    {
        if (!CurrencyManager.Instance.Spend(cd.price))
        {
            ShowMessage("เหรียญไม่พอ");
            return;
        }

        PlayerProgressSO.Instance.AddCard(cd.id);
        ShowMessage($"ซื้อ {cd.displayName} สำเร็จ!");
        Reroll();
    }
    void ShowMessage(string msg)
    {
        if (popupPanel == null || messageText == null)
        {
            Debug.LogWarning($"[UICardShop] {msg}");   // fallback
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
        popupPanel.SetActive(false);
    }
}
