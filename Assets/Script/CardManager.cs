using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Card Pool")]
    public List<CardData> allCards;
    public int maxHeldCards = 2;
    public List<CardData> heldCards = new List<CardData>();

    // คิวเก็บแต่ละชุดตัวเลือกการ์ด
    private Queue<List<CardData>> optionsQueue = new Queue<List<CardData>>();
    private CardData pendingReplacementCard;
    private bool isReplaceMode = false;
    private List<CardData> lastOptions;

    // นับจำนวนชุดทั้งหมดที่ถูก enqueue และที่ถูกประมวลผล
    private int totalQueuedCount = 0;
    private int processedCount = 0;

    [Header("UI")]
    public UICardSelect uiSelect;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private List<CardData> BuildThreeRandom()
    {
        var opts = new List<CardData>();
        while (opts.Count < 3)
        {
            var cd = allCards[Random.Range(0, allCards.Count)];
            if (!opts.Contains(cd)) opts.Add(cd);
        }
        return opts;
    }

    public void GiveRandomCard()
    {
        var opts = BuildThreeRandom();
        optionsQueue.Enqueue(opts);
        totalQueuedCount++;
        Debug.Log($"[CardManager] Enqueued options. Queue size: {optionsQueue.Count}, Total queued: {totalQueuedCount}");
        TryOpenNextSelection();
    }

    private void TryOpenNextSelection()
    {
        if (uiSelect.IsOpen || isReplaceMode) return;
        if (optionsQueue.Count == 0) return;

        processedCount++;
        Debug.Log($"[CardManager] Processing queue {processedCount}/{totalQueuedCount}. Remaining in queue: {optionsQueue.Count - 1}");

        lastOptions = optionsQueue.Dequeue();
        uiSelect.Open(lastOptions, OnCardPicked);
    }

    private void OnCardPicked(CardData picked)
    {
        if (!isReplaceMode)
        {
            if (heldCards.Count < maxHeldCards)
            {
                heldCards.Add(picked);
                UIManager.Instance.UpdateCardSlots(heldCards);
            }
            else
            {
                pendingReplacementCard = picked;
                isReplaceMode = true;
                UIManager.Instance.UpdateCardSlots(heldCards, true);
                return;
            }
        }

        UIManager.Instance.UpdateCardSlots(heldCards);
        isReplaceMode = false;

        TryOpenNextSelection();
    }

    public void CancelReplacement()
    {
        if (!isReplaceMode) return;
        pendingReplacementCard = null;
        isReplaceMode = false;
        UIManager.Instance.HideMessage();
        UIManager.Instance.UpdateCardSlots(heldCards);

        // เปิดหน้าเลือกการ์ดชุดเดิมซ้ำอีกครั้ง
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

        TryOpenNextSelection();
    }

    public void UseCard(int index)
    {
        if (index >= heldCards.Count) return;
        var card = heldCards[index];
        UIConfirmPopup.Show($"ใช้การ์ด '{card.displayName}'?", () =>
        {
            ApplyEffect(card);
            heldCards.RemoveAt(index);
            UIManager.Instance.UpdateCardSlots(heldCards);
        });
    }

    private void ApplyEffect(CardData card)
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
        }
    }
}
