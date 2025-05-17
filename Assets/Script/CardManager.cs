using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Card Pool")]
    public List<CardData> allCards;            // ใส่ใน Inspector

    [Header("UI")]
    public UICardSelect uiSelect;              // panel เลือกการ์ด (สร้างแยก)

    void Awake() { if (Instance==null) Instance=this; else Destroy(gameObject); }

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
        uiSelect.Open(opts, OnCardPicked);
    }

    void OnCardPicked(CardData picked)
    {
        ApplyEffect(picked);
        // ถ้าเก็บไว้ใช้ทีหลังให้เพิ่มเข้ามือผู้เล่นแทน
    }

    /* -------- Effect -------- */
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
