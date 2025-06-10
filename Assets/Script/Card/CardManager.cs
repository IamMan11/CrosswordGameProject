using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Card Pool")]
    public List<CardData> allCards;
    public int maxHeldCards = 2;
    public List<CardData> heldCards = new List<CardData>();

    [Header("Category Weights (ปรับ % ออกได้)")]
    [Tooltip("น้ำหนักการสุ่ม Category แต่ละประเภท (ค่ารวมแล้ว = 100 หรืออะไรก็ได้ แต่จะถูก Normalized อัตโนมัติ)")]
    public List<CategoryWeight> categoryWeights = new List<CategoryWeight>()
    {
        new CategoryWeight { category = CardCategory.Buff, weight = 40 },
        new CategoryWeight { category = CardCategory.Dispell, weight = 30 },
        new CategoryWeight { category = CardCategory.Neutral, weight = 20 },
        new CategoryWeight { category = CardCategory.Wildcard, weight = 10 }
    };
    [System.Serializable]
    public struct CategoryWeight
    {
        public CardCategory category;
        public int weight;
    }

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
        DontDestroyOnLoad(gameObject);       // 🟢 ต้องมี เพื่ออยู่ข้าม Scene
        LoadAllCards();                      // 🟢 เมธอดที่จะโหลด CardData ทั้งหมด
    }
    void LoadAllCards()
    {
        // ดึง CardData ทั้งหมดจาก Resources/Cards เหมือนตัวอย่างก่อนหน้า
        allCards = Resources.LoadAll<CardData>("Cards").ToList();
    }
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // พยายามหา UICardSelect ตัวใหม่ใน Scene ที่เพิ่งโหลด
        uiSelect = FindObjectOfType<UICardSelect>(true);
    }

    private CardData GetWeightedRandomCard()
    {
        // 1) สร้างลิสต์ที่รวม Category ที่มี weight > 0
        var nonZeroCategories = categoryWeights
            .Where(cw => cw.weight > 0)
            .ToList();

        if (nonZeroCategories.Count == 0)
            return null;

        // 2) คำนวณผลรวม weight ของ Category ทั้งหมด
        int totalCategoryWeight = nonZeroCategories.Sum(cw => cw.weight);

        // 3) สุ่มตัวเลขตั้งแต่ 0 - totalCategoryWeight-1
        int randCatValue = Random.Range(0, totalCategoryWeight);

        // 4) หา Category ที่ถูกเลือก (First-fit)
        CardCategory chosenCategory = nonZeroCategories[0].category;
        int accumulated = 0;
        foreach (var cw in nonZeroCategories)
        {
            accumulated += cw.weight;
            if (randCatValue < accumulated)
            {
                chosenCategory = cw.category;
                break;
            }
        }

        // 5) รวบรวมการ์ดที่ตรงกันใน allCards ตาม chosenCategory
        var cardsInCategory = allCards
            .Where(cd =>
                cd.category == chosenCategory &&
                cd.weight > 0 &&
                (!cd.requirePurchase || PlayerProgressSO.Instance.HasCard(cd.id))   // 🆕
            )
            .ToList();

        if (cardsInCategory.Count == 0)
        {
            // ถ้าไม่มีการ์ดใน Category นี้ ให้ fallback ไปสุ่มจาก allCards ปกติ
            return allCards[Random.Range(0, allCards.Count)];
        }

        // 6) คำนวณผลรวม weight ภายใน Category
        int totalCardWeight = cardsInCategory.Sum(cd => cd.weight);

        // 7) สุ่มตัวเลขตั้งแต่ 0 - totalCardWeight-1
        int randCardValue = Random.Range(0, totalCardWeight);

        // 8) หา CardData ใบที่ถูกเลือก (First-fit)
        int accCard = 0;
        foreach (var cd in cardsInCategory)
        {
            accCard += cd.weight;
            if (randCardValue < accCard)
                return cd;
        }

        // กรณีตกหล่น (ควรจะไม่ถึง)
        return cardsInCategory[0];
    }


    private List<CardData> BuildThreeWeightedRandom()
    {
        var opts = new List<CardData>();
        int attempts = 0;
        while (opts.Count < 3 && attempts < 20)
        {
            var candidate = GetWeightedRandomCard();
            if (candidate != null && !opts.Contains(candidate))
            {
                opts.Add(candidate);
            }
            attempts++;
        }

        // หากยังไม่ครบ 3 ใบ (เช่น หาก weight จัดไว้ผิดพลาด) ให้สุ่มเพิ่มเติมจาก allCards ปกติ
        while (opts.Count < 3)
        {
            var fallback = allCards[Random.Range(0, allCards.Count)];
            if (!opts.Contains(fallback))
                opts.Add(fallback);
        }

        return opts;
    }
    private void OnUseMasterDraft()
    {
        // เปิด UI MasterDraft รับ allCards ทั้งหมด
        UIMasterDraft.Instance.Open(allCards, OnMasterDraftCardPicked);
    }
    private void OnMasterDraftCardPicked(CardData selected)
    {
        // นำ CardData ที่ได้ มาใส่ใน heldCards หรือแทนที่ช่องที่ต้องการ
        if (heldCards.Count < maxHeldCards)
        {
            heldCards.Add(selected);
        }
        else
        {
            // กรณี heldCards เต็มแล้ว → อาจจะให้ Replace โดยเลือก index ที่ต้องการ
            // หรือเลือกให้ผู้เล่นคลิก slot ที่จะถูกแทน
            // ตัวอย่าง: แทนที่ index 0
            heldCards[0] = selected;
        }

        // อัปเดต UI การ์ดที่ถือ (เช่น UpdateCardSlots)
        UIManager.Instance.UpdateCardSlots(heldCards);

        Debug.Log($"[MasterDraft] ผู้เล่นเลือกการ์ด {selected.displayName} แล้วใส่ลง heldCards");
    }
    public void GiveRandomCard()
    {
        var opts = BuildThreeWeightedRandom();
        optionsQueue.Enqueue(opts);
        totalQueuedCount++;
        Debug.Log($"[CardManager] Enqueued options. Queue size: {optionsQueue.Count}, Total queued: {totalQueuedCount}");
        TryOpenNextSelection();
    }
    public void UpgradeMaxHeldCards(int newMax)
    {
        // ปรับค่าช่องการ์ดสูงสุดตามที่ซื้อมา (จำกัด 2–6 ช่อง)
        maxHeldCards = Mathf.Clamp(newMax, 2, 6);

        // อัปเดต UI เฉพาะเมื่อ UIManager มีอยู่ (เช่น ในเกมหลัก ไม่เรียกในหน้า Shop)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateCardSlots(heldCards);
        }
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
            // Card 13. Free Pass – ยกเลิก penalty การเปิดพจนานุกรมในเทิร์นนี้
            case CardEffectType.FreePass:
                TurnManager.Instance.ApplyFreePass();
                break;

            // Card 14. Minor Infusion – เพิ่ม Mana 2 หน่วย
            case CardEffectType.MinorInfusion:
                TurnManager.Instance.AddMana(2);
                break;

            // Card 15. Major Infusion – เพิ่ม Mana 5 หน่วย
            case CardEffectType.MajorInfusion:
                TurnManager.Instance.AddMana(5);
                break;

            // Card 16. Mana Overflow – เติม Mana จนเต็ม (maxMana)
            case CardEffectType.ManaOverflow:
                TurnManager.Instance.AddMana(TurnManager.Instance.maxMana);
                break;
            // 17. Wild Bloom – สุ่มให้มีช่องพิเศษใน Board เพิ่มขึ้น 10 ช่อง
            case CardEffectType.WildBloom:
                BoardManager.Instance.AddRandomSpecialSlots(10);
                UIManager.Instance.ShowMessage("Wild Bloom — เพิ่มช่องพิเศษแบบสุ่ม 10 ช่อง!", 2f);
                break;

            // 18. Chaos Bloom – สุ่มให้มีช่องพิเศษใน Board เพิ่มขึ้น 25 ช่อง
            case CardEffectType.ChaosBloom:
                BoardManager.Instance.AddRandomSpecialSlots(25);
                UIManager.Instance.ShowMessage("Chaos Bloom — เพิ่มช่องพิเศษแบบสุ่ม 25 ช่อง!", 2f);
                break;

            // 19. Targeted Flux – เลือกช่อง 5 ช่องโดยการคลิก เพื่อเปลี่ยนเป็นช่องพิเศษ
            case CardEffectType.TargetedFlux:
                BoardManager.Instance.StartTargetedFlux(5);
                UIManager.Instance.ShowMessage("Targeted Flux — คลิกเลือก 5 ช่องเพื่อเป็นช่องพิเศษ!", 2f);
                break;

            // 20. Clean Slate – ล้างตัวอักษรทั้งหมดใน Board
            case CardEffectType.CleanSlate:
                BoardManager.Instance.CleanSlate();
                UIManager.Instance.ShowMessage("Clean Slate — ล้างตัวอักษรทั้งหมดบนกระดาน!", 2f);
                break;
            // 21. LetterDoubleTime – ทำให้ตัวอักษรทั้งหมดคะแนน x2 เป็นเวลา 1 นาที
            case CardEffectType.GlobalEcho:
                // multiplier=2, duration=60 วินาที
                ScoreManager.ActivateGlobalLetterMultiplier(2, 60f);
                UIManager.Instance.ShowMessage("Letter Double Time – ตัวอักษรทั้งหมด ×2 เป็นเวลา 1 นาที!", 2f);
                break;

            // 22. AllRandomSpecialTime – ทุกช่องกลายเป็น special แบบสุ่ม เป็นเวลา 1 นาที
            case CardEffectType.PandemoniumField:
                BoardManager.Instance.ActivateAllRandomSpecial(60f);
                // ข้อความแสดงผลอยู่ใน ActivateAllRandomSpecial() แล้ว
                break;

            // 23. ResetCardUsage – รีเซ็ตการใช้การ์ดในเทิร์นนี้
            case CardEffectType.CardRefresh:
                TurnManager.Instance.ResetCardUsage();
                // ข้อความแสดงผลอยู่ใน ResetCardUsage() แล้ว
                break;
            // 24. InfiniteTiles (ตัวอักษรจาก tilepack ไม่หมด) 60 วินาที
            case CardEffectType.InfiniteTiles:
                TileBag.Instance.ActivateInfinite(60f);
                UIManager.Instance.ShowMessage("Infinite Tiles – tilepack ไม่จำกัด 1 นาที!", 2f);
                break;

            // 25. PackRenewal (รีเซ็ต tilepack)
            case CardEffectType.PackRenewal:
                TileBag.Instance.ResetPool();
                UIManager.Instance.ShowMessage("Pack Renewal – รีเซ็ต tilepack ใหม่ทั้งหมด!", 2f);
                break;

            // 26. ManaInfinity (มานาไม่จำกัด) 60 วินาที
            case CardEffectType.ManaInfinity:
                TurnManager.Instance.ActivateInfiniteMana(60f);
                // ข้อความแสดงอยู่ใน ActivateInfiniteMana()
                break;

            // 27. OmniSpark (bench เป็น special ทั้งหมด ชั่วคราว)
            case CardEffectType.OmniSpark:
                BenchManager.Instance.OmniSpark();
                UIManager.Instance.ShowMessage("Omni Spark – ทุกตัวใน Bench เป็น special ชั่วคราว!", 2f);
                break;
            // 28. MasterDraft เลือกการ์ดยกเว่้น widecard
            case CardEffectType.MasterDraft:
                OnUseMasterDraft();
                break;
        }
    }
}
