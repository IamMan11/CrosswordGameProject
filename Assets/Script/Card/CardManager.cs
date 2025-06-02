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
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }

        maxHeldCards = PlayerProgressSO.Instance.data.maxCardSlots;   // ✨
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
    public void UpgradeMaxHeldCards(int newMax)
    {
        maxHeldCards = Mathf.Clamp(newMax, 2, 6);   // อยากจำกัดไม่เกิน 6 ช่อง
        UIManager.Instance.UpdateCardSlots(heldCards);
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
        if (index < 0 || index >= heldCards.Count) return;

        var card = heldCards[index];

        // 1) เรียก Popup ยืนยันก่อนเลย (ยังไม่ตรวจ CanUseCard)
        UIConfirmPopup.Show(
            $"ใช้การ์ด '{card.displayName}' ({card.Mana} Mana)?",
            () =>
            {
                // ▶ ปุ่มยืนยัน (Confirm) ถูกกดครั้งแรก ณ จุดนี้ เริ่มตรวจเงื่อนไข

                // 2) ตรวจว่าใช้เกินจำนวนต่อเทิร์นหรือยัง
                if (!TurnManager.Instance.CanUseCard(card))
                {
                    UIManager.Instance.ShowMessage("เกินจำนวนที่ใช้ได้", 2f);
                    // ลบการ์ดออกจากมือทันที (ไม่เกิดเอฟเฟกต์)
                    heldCards.RemoveAt(index);
                    UIManager.Instance.UpdateCardSlots(heldCards);
                    return; // ร้องจบ ไม่ทำอะไรต่อ
                }

                // 3) ถ้ายังใช้ได้ จึงตรวจ Mana ตามเดิม
                int cost = card.Mana;
                if (!TurnManager.Instance.UseMana(cost))
                {
                    UIManager.Instance.ShowMessage($"Mana ไม่พอ (ต้องใช้ {cost})", 2f);
                    return; // ถอนการยืนยัน ไม่ลบการ์ด (ผู้เล่นยังถือการ์ดนี้ไว้)
                }

                // 4) เรียกใช้เอฟเฟกต์การ์ด
                ApplyEffect(card);

                // 5) บันทึกว่าการ์ดใบนี้ถูกใช้ไป 1 ครั้ง
                TurnManager.Instance.OnCardUsed(card);

                // 6) ลบการ์ดออกจากมือและอัปเดต UI
                heldCards.RemoveAt(index);
                UIManager.Instance.UpdateCardSlots(heldCards);
            },
            () =>
            {
                // ▶ ปุ่มยกเลิก (Cancel) ถูกกด: คืน Mana ถ้ามีการหักไปก่อนหน้า
                // แต่โค้ดนี้ยังไม่หัก Mana ตั้งแต่ก่อน confirm จึงไม่ต้องคืน
            }
        );
    }

    private void ApplyEffect(CardData card)
    {
        switch (card.effectType)
        {
            //Card 1.เปลี่ยนให้ช่องพิเศษ letter จาก x2 เป็น x4
            case CardEffectType.LetterQuadSurge:
                ScoreManager.SetDoubleLetterOverride(4);   // บอกตัวคำนวณคะแนน
                UIManager.Instance.ShowMessage("ช่อง DL กลายเป็น x4 ในตานี้!", 2);
                break;
            //Card 2.เปลี่ยนให้ช่องพิเศษ letter จาก x3 เป็น x6
            case CardEffectType.LetterHexSurge:
                ScoreManager.SetDoubleLetterOverride(6);   // บอกตัวคำนวณคะแนน
                UIManager.Instance.ShowMessage("ช่อง DL กลายเป็น x6 ในตานี้!", 2);
                break;
            //Card 3.เปลี่ยนให้ช่องพิเศษ Word จาก x2 เป็น x4
            case CardEffectType.WordQuadSurge:
                ScoreManager.SetDoubleWordOverride(4);   // บอกตัวคำนวณคะแนน
                UIManager.Instance.ShowMessage("ช่อง DW กลายเป็น x4 ในตานี้!", 2);
                break;
            //Card 4.เปลี่ยนให้ช่องพิเศษ Word จาก x3 เป็น x6
            case CardEffectType.WordHexSurge:
                ScoreManager.SetDoubleWordOverride(6);   // บอกตัวคำนวณคะแนน
                UIManager.Instance.ShowMessage("ช่อง DW กลายเป็น x6 ในตานี้!", 2);
                break;
            // Card 5.เติม Bench 2 ตัวอักษร
            case CardEffectType.TwinDraw:
                for (int i = 0; i < 2; i++)
                    BenchManager.Instance.RefillOneSlot();
                UIManager.Instance.ShowMessage("Twin Draw – เติม 2 ตัวอักษร!", 2);
                break;
            //Card 6.เติม Bench 4 ตัวอักษร
            case CardEffectType.QuadSupply:
                for (int i = 0; i < 4; i++)
                    BenchManager.Instance.RefillOneSlot();
                UIManager.Instance.ShowMessage("Quad Supply – เติม 4 ตัวอักษร!", 2);
                break;
            //Card 7.เติม Bench ทุกช่องว่าง
            case CardEffectType.BenchBlitz:
                BenchManager.Instance.RefillEmptySlots();
                UIManager.Instance.ShowMessage("Bench Blitz – เติมครบทุกช่องว่าง!", 2);
                break;
            //Card 8.จั่วการ์ดเพิ่ม 2 ใบ
            case CardEffectType.DoubleRecast:
                for (int i = 0; i < 2; i++)
                    GiveRandomCard();
                UIManager.Instance.ShowMessage(
                    "Pick new card", 2);
                break;
            //Card 9.x2 คำใน Turn นั้น
            case CardEffectType.EchoBurst:
                TurnManager.Instance.SetScoreMultiplier(2);
                UIManager.Instance.ShowMessage("Echo Burst! คำนี้คูณ ×2 ทันที", 2);
                break;
            //Card 10.Full Rerack
            case CardEffectType.FullRerack:
                BenchManager.Instance.FullRerack();
                UIManager.Instance.ShowMessage("Full Rerack — สุ่ม Bench ใหม่ทั้งหมด!", 2);
                break;

            //Card 11.Glyph Spark: แทนที่ตัวอักษรใน Bench ให้เป็นพิเศษ 1 ตัว
            case CardEffectType.GlyphSpark:
                BenchManager.Instance.ReplaceRandomWithSpecial(1);
                UIManager.Instance.ShowMessage("Glyph Spark — หนึ่งตัวใน Bench เป็นตัวพิเศษ!", 2);
                break;

            //Card 12.Twin Sparks: แทนที่ตัวอักษรใน Bench ให้เป็นพิเศษ 2 ตัว
            case CardEffectType.TwinSparks:
                BenchManager.Instance.ReplaceRandomWithSpecial(2);
                UIManager.Instance.ShowMessage("Twin Sparks — สองตัวใน Bench เป็นตัวพิเศษ!", 2);
                break;
            }
    }
}
