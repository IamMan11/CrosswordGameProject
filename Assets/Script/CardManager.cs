using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Card Pool")]
    public List<CardData> allCards;            // ใส่ใน Inspector
    public int maxHeldCards = 2;
    public List<CardData> heldCards = new List<CardData>();
    private List<CardData> lastOptions;
    private CardData pendingReplacementCard;
    private bool isReplaceMode = false;

    [Header("UI")]
    public UICardSelect uiSelect;              // panel เลือกการ์ด (สร้างแยก)

    void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

    /* -------- API -------- */

    /// <summary>ให้การ์ด 1 ใบ (สุ่ม 3 → ผู้เล่นเลือก 1)</summary>
    public void GiveRandomCard()
    {
        Debug.Log("[CardManager] สุ่ม 3 การ์ดเตรียมให้ผู้เล่นเลือก");
        var opts = new List<CardData>();
        while (opts.Count < 3)
        {
            var cd = allCards[Random.Range(0, allCards.Count)];
            if (!opts.Contains(cd)) opts.Add(cd);
        }
        lastOptions = opts;
        uiSelect.Open(opts, OnCardPicked);
    }

    /* -------- Effect -------- */
    void OnCardPicked(CardData picked)
    {
        if (!isReplaceMode)
        {
            if (heldCards.Count < maxHeldCards)
            {
                heldCards.Add(picked);
            }
            else
            {
                // เริ่มโหมดแทนการ์ด
                pendingReplacementCard = picked;
                isReplaceMode = true;
                UIManager.Instance.ShowMessage("เลือกการ์ดที่จะแทน", 0f);
                UIManager.Instance.UpdateCardSlots(heldCards, true);
                return;
            }
        }
        UIManager.Instance.UpdateCardSlots(heldCards);
    }

    public void UseCard(int index)
    {
        if (index >= heldCards.Count) return;
        var card = heldCards[index];
        UIConfirmPopup.Show($"ใช้การ์ด \"{card.displayName}\"?", () =>
        {
            ApplyEffect(card);
            heldCards.RemoveAt(index);
            UIManager.Instance.UpdateCardSlots(heldCards);
        });
    }
    /// <summary>แทนการ์ดใน heldCards[index] ด้วย pendingReplacementCard</summary>
    public void CancelReplacement()
    {
        if (!isReplaceMode) return;

        // ยกเลิกโหมดแทน
        pendingReplacementCard = null;
        isReplaceMode = false;
        UIManager.Instance.HideMessage();
        UIManager.Instance.UpdateCardSlots(heldCards);

        // ← 3. เปิด Popup เลือกการ์ดชุดเดิมซ้ำอีกครั้ง
        uiSelect.Open(lastOptions, OnCardPicked);
    }

    public void ReplaceSlot(int index)
    {
        if (!isReplaceMode || pendingReplacementCard == null) return;

        heldCards[index] = pendingReplacementCard;
        pendingReplacementCard = null;
        isReplaceMode = false;

        UIManager.Instance.HideMessage();
        UIManager.Instance.UpdateCardSlots(heldCards);
    }
    void ApplyEffect(CardData card)
    {
        switch (card.effectType)
        {
            case CardEffectType.ExtraDraw:
                for (int i = 0; i < card.value; i++)
                    BenchManager.Instance.RefillOneSlot();
                UIManager.Instance.ShowMessage($"+{card.value} letters!", 2);
                break;

            case CardEffectType.DoubleNextScore:
                TurnManager.Instance.SetScoreMultiplier(card.value);
                UIManager.Instance.ShowMessage($"Next word x{card.value}", 2);
                break;

                // … เพิ่มตามต้องการ …
        }
    }
    
}
