using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;

/// <summary>
/// CardManager
/// - ดูแล pool การ์ดทั้งหมด (โหลดจาก Resources) และถือการ์ดในมือผู้เล่น (heldCards)
/// - จัดคิวตัวเลือกการ์ด (3 ใบ) เปิด UI เลือกการ์ดตามลำดับ
/// - โหมดทดสอบ: สลับโฟลเดอร์การ์ด/ตารางฟิวชันขณะรันได้
/// - รองรับ Replace เมื่อช่องเต็ม, Fusion, จัดเรียงการ์ด, ใช้การ์ด (หักมานา/จำจำนวนใช้ต่อเทิร์น)
/// 
/// หมายเหตุการคงพฤติกรรม:
/// - ไม่เปลี่ยนชื่อฟิลด์/เมธอดสาธารณะ
/// - เพิ่มเช็ก null/ขอบเขต, อธิบายคอมเมนต์, และจับเคส UI/Singleton ยังไม่พร้อม เพื่อกัน NRE
/// </summary>
public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    // ====== UI Hold (กัน UI เลือกการ์ดเด้งขึ้นระหว่างอนิเมชันอื่น) ======
    private int _uiHoldCount = 0;
    /// <summary>ตอนที่ _uiHoldCount > 0 จะ "พัก" การเปิด UI เลือกการ์ด</summary>
    public bool IsUIHeld => _uiHoldCount > 0;

    /// <summary>เพิ่ม/ลด hold UI; เมื่อปลด hold แล้วจะพยายามเปิดคิวถัดไป</summary>
    public void HoldUI(bool on)
    {
        if (on) _uiHoldCount++;
        else    _uiHoldCount = Mathf.Max(0, _uiHoldCount - 1);

        if (_uiHoldCount == 0) TryOpenNextSelection(); // ปลด hold แล้วค่อยเปิดคิวต่อ
    }

    // ====== Config / State ======
    [Header("Card Pool")]
    public List<CardData> allCards;
    public int maxHeldCards = 2;
    public List<CardData> heldCards = new List<CardData>();

    [Header("Category Weights (ปรับ % ได้)")]
    [Tooltip("น้ำหนักการสุ่ม Category; ค่ารวมไม่จำเป็นต้อง 100 (จะคิดแบบสัดส่วน)")]
    public List<CategoryWeight> categoryWeights = new List<CategoryWeight>()
    {
        new CategoryWeight { category = CardCategory.Buff,     weight = 40 },
        new CategoryWeight { category = CardCategory.Dispell,  weight = 30 },
        new CategoryWeight { category = CardCategory.Neutral,  weight = 20 },
        new CategoryWeight { category = CardCategory.Wildcard, weight = 10 }
    };

    [System.Serializable]
    public struct CategoryWeight
    {
        public CardCategory category;
        public int weight;
    }

    [Header("Fusion")]
    public CardFusionTable fusionTable;   // ตารางฟิวชัน (ScriptableObject)

    // คิวของ "ตัวเลือกการ์ด 3 ใบ" ที่รอเปิด UI
    private readonly Queue<List<CardData>> optionsQueue = new Queue<List<CardData>>();
    private CardData pendingReplacementCard;   // การ์ดที่เลือกไว้ รอแทนที่ช่อง
    private bool isReplaceMode = false;        // โหมดแทนที่
    private List<CardData> lastOptions;        // เก็บชุดล่าสุด เผื่อกดยกเลิกแทนที่จะย้อนกลับมา

    // ตัวนับเพื่อดีบัก (ไม่บังคับใช้)
    private int totalQueuedCount = 0;
    private int processedCount = 0;

    [Header("UI")]
    public UICardSelect uiSelect; // หน้าต่างเลือกการ์ด 3 ใบ

    // ====== เส้นทาง Resources (สลับโหมดทดสอบได้) ======
    [SerializeField] string cardsFolder        = "Cards";         // Resources/Cards
    [SerializeField] string cardsFolder_Test   = "Card_Tests";    // Resources/Card_Tests
    [SerializeField] string fusionPath         = "CardFusions/Fusions";
    [SerializeField] string fusionPath_Test    = "CardFusions/Fusions";
    [SerializeField] bool   useTestInThisScene = false;

    // ค่าที่คงอยู่ข้ามซีน (static)
    static bool   sInited;
    static string sActiveCardsFolder;
    static string sActiveFusionPath;

    // =======================================================================
    #region Unity Lifecycle

    void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);

        // จำนวนช่องการ์ดสูงสุดอ่านจากโปรเกรส (ถ้ามี)
        maxHeldCards = 2;
        var prog = PlayerProgressSO.Instance;
        if (prog != null && prog.data != null)
            maxHeldCards = Mathf.Max(1, prog.data.maxCardSlots);
        else
            Debug.LogWarning("[CardManager] PlayerProgressSO ยังไม่พร้อม ใช้ค่า default 2 ชั่วคราว");

        // ตั้ง active paths ครั้งเดียวต่อแอป (ซีนแรก)
        if (!sInited)
        {
            bool useTest = useTestInThisScene;
            sActiveCardsFolder = useTest ? cardsFolder_Test : cardsFolder;
            sActiveFusionPath  = useTest ? fusionPath_Test  : fusionPath;
            sInited = true;
        }

        LoadAllCards();
        LoadFusionTable();
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // หา UICardSelect ในซีนใหม่ (รวม inactive)
        uiSelect = FindObjectOfType<UICardSelect>(true);
    }

    #endregion
    // =======================================================================

    #region Resource Loading

    /// <summary>โหลดการ์ดทั้งหมดจาก Resources/{activeFolder}</summary>
    void LoadAllCards()
    {
        var folder = string.IsNullOrEmpty(sActiveCardsFolder) ? cardsFolder : sActiveCardsFolder;
        allCards = Resources.LoadAll<CardData>(folder).ToList();
        Debug.Log($"[CardManager] Loaded {allCards.Count} cards from Resources/{folder}");
    }

    /// <summary>โหลดตารางฟิวชันจาก Resources/{activeFusionPath} ถ้า Inspector ไม่ได้ตั้งไว้</summary>
    void LoadFusionTable()
    {
        var path = string.IsNullOrEmpty(sActiveFusionPath) ? fusionPath : sActiveFusionPath;

        if (fusionTable == null)
            fusionTable = Resources.Load<CardFusionTable>(path);

        if (fusionTable != null)
        {
            fusionTable.BuildMap();
            Debug.Log($"[CardManager] FusionTable loaded from Resources/{path}");
        }
        else
        {
            Debug.LogWarning($"[CardManager] ไม่พบ FusionTable ที่ Resources/{path} (จะใช้ค่าที่ผูกใน Inspector ถ้ามี)");
        }
    }

    /// <summary>สลับเข้า/ออกโหมดทดสอบระหว่างรัน แล้วรีโหลด pool/fusion</summary>
    public void UseTestMode(bool on)
    {
        sActiveCardsFolder = on ? cardsFolder_Test : cardsFolder;
        sActiveFusionPath  = on ? fusionPath_Test  : fusionPath;

        LoadAllCards();
        fusionTable = null;   // บังคับให้โหลดใหม่
        LoadFusionTable();

        UIManager.Instance?.UpdateCardSlots(heldCards);
    }

    #endregion
    // =======================================================================

    #region Random Options / MasterDraft

    /// <summary>
    /// สุ่มการ์ด 1 ใบตาม "น้ำหนักของหมวด (Category)" ก่อน แล้วค่อย "น้ำหนักของการ์ดในหมวด"
    /// - เคารพเงื่อนไข requirePurchase: ถ้ายังไม่ได้ซื้อ จะไม่นำมาสุ่ม (ยกเว้นไม่มี PlayerProgress)
    /// - ข้ามหมวด/ใบที่ weight <= 0
    /// </summary>
    private CardData GetWeightedRandomCard()
    {
        // 1) หมวดที่ weight > 0
        var nonZeroCategories = categoryWeights.Where(cw => cw.weight > 0).ToList();
        if (nonZeroCategories.Count == 0) return null;

        // 2) สุ่มเลือกหมวด
        int totalCategoryWeight = nonZeroCategories.Sum(cw => cw.weight);
        int randCatValue = Random.Range(0, totalCategoryWeight);

        CardCategory chosenCategory = nonZeroCategories[0].category;
        int acc = 0;
        foreach (var cw in nonZeroCategories)
        {
            acc += cw.weight;
            if (randCatValue < acc) { chosenCategory = cw.category; break; }
        }

        // 3) กรองการ์ดในหมวดนั้น ๆ
        bool canCheckOwns = PlayerProgressSO.Instance != null;
        var cardsInCategory = allCards
            .Where(cd =>
                cd.category == chosenCategory &&
                cd.weight > 0 &&
                (!cd.requirePurchase || (canCheckOwns && PlayerProgressSO.Instance.HasCard(cd.id)))
            ).ToList();

        if (cardsInCategory.Count == 0)
        {
            // fallback: เลือกจาก pool ทั้งหมดที่ไม่ใช่ FusionCard
            var pool = allCards.Where(cd => cd.category != CardCategory.FusionCard).ToList();
            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        // 4) สุ่มเลือกการ์ดในหมวด
        int totalCardWeight = cardsInCategory.Sum(cd => cd.weight);
        int randCardValue = Random.Range(0, totalCardWeight);

        int accCard = 0;
        foreach (var cd in cardsInCategory)
        {
            accCard += cd.weight;
            if (randCardValue < accCard) return cd;
        }
        return cardsInCategory[0];
    }

    /// <summary>สร้างชุดตัวเลือก 3 ใบ โดยพยายามไม่ซ้ำ</summary>
    private List<CardData> BuildThreeWeightedRandom()
    {
        var opts = new List<CardData>();
        int attempts = 0;

        while (opts.Count < 3 && attempts < 20)
        {
            var candidate = GetWeightedRandomCard();
            if (candidate != null && !opts.Contains(candidate))
                opts.Add(candidate);
            attempts++;
        }

        // ถ้ายังไม่ครบ 3 ใบ ให้สุ่มจาก pool ทั้งหมด (ยกเว้น FusionCard)
        while (opts.Count < 3)
        {
            var fallbackPool = allCards.Where(cd => cd.category != CardCategory.FusionCard).ToList();
            if (fallbackPool.Count == 0) break;
            var fallback = fallbackPool[Random.Range(0, fallbackPool.Count)];
            if (!opts.Contains(fallback)) opts.Add(fallback);
        }
        return opts;
    }

    /// <summary>เปิดหน้าต่าง MasterDraft (เลือกจาก allCards ทั้งหมด)</summary>
    private void OnUseMasterDraft()
    {
        if (UIMasterDraft.Instance == null)
        {
            UIManager.Instance?.ShowMessage("ไม่พบ UIMasterDraft", 1.2f);
            return;
        }
        UIMasterDraft.Instance.Open(allCards, OnMasterDraftCardPicked);
    }

    /// <summary>รับผลจาก MasterDraft แล้วใส่การ์ดเข้ามือ (หรือแทนช่อง 0 ถ้าเต็ม)</summary>
    private void OnMasterDraftCardPicked(CardData selected)
    {
        if (selected == null) return;

        if (heldCards.Count < maxHeldCards)
            heldCards.Add(selected);
        else
            heldCards[0] = selected; // ตัวอย่าง: แทน index 0

        UIManager.Instance?.UpdateCardSlots(heldCards);
    }

    #endregion
    // =======================================================================

    #region Public APIs (สุ่ม/แจก/อัปเกรด/ย้าย/ใช้/ฟิวชัน)

    /// <summary>ต่อคิว “ตัวเลือก 3 ใบ” แล้วพยายามเปิด UI ถ้าว่าง</summary>
    public void GiveRandomCard()
    {
        var opts = BuildThreeWeightedRandom();
        if (opts == null || opts.Count == 0) return;

        optionsQueue.Enqueue(opts);
        totalQueuedCount++;
        TryOpenNextSelection();
    }

    /// <summary>ปรับช่องถือการ์ดสูงสุด (จำกัด 2–6) และอัปเดต UI</summary>
    public void UpgradeMaxHeldCards(int newMax)
    {
        maxHeldCards = Mathf.Clamp(newMax, 2, 6);
        UIManager.Instance?.UpdateCardSlots(heldCards);
    }

    /// <summary>ถ้าไม่ถูก hold และไม่มี UI เปิด/replace อยู่ จะเปิดชุดถัดไปจากคิว</summary>
    void TryOpenNextSelection()
    {
        if (_uiHoldCount > 0) { Debug.Log("Hold UI"); return; }
        if (isReplaceMode)    { Debug.Log("Replace mode pending"); return; }
        if (optionsQueue.Count == 0) { Debug.Log("No options queued"); return; }
        if (uiSelect == null) { Debug.Log("No UICardSelect in scene"); return; }

        if (uiSelect.IsOpen || uiSelect.IsWaitingReplace || uiSelect.HasActiveClone)
        { Debug.Log("UI busy (open/replace/clone)"); return; }

        lastOptions = optionsQueue.Dequeue();
        uiSelect.Open(lastOptions, OnCardPicked);
    }
    /// <summary>callback เมื่อเลือกการ์ดจาก UI 3 ใบ</summary>
    private void OnCardPicked(CardData picked)
    {
        if (picked == null) { TryOpenNextSelection(); return; }
        StartCoroutine(ApplyPickAfterEndOfFrame(picked));
    }

    private IEnumerator ApplyPickAfterEndOfFrame(CardData picked)
    {
        // ให้ UICardSelect ซ่อน/ทำลายโคลนในเฟรมปัจจุบันให้เรียบร้อยก่อน
        yield return new WaitForEndOfFrame();

        // ⭐ รอจน panel ปิด, ไม่มีโคลน และไม่อยู่ระหว่างอนิเมชัน
        if (uiSelect != null)
            yield return new WaitUntil(() =>
                !uiSelect.IsOpen && !uiSelect.HasActiveClone && !uiSelect.IsAnimating
            );

        // เผื่อระบบ Layout/Rebuild ของ Unity ให้รออีก 1 เฟรม
        yield return null;

        if (!isReplaceMode)
        {
            if (heldCards.Count < maxHeldCards)
            {
                heldCards.Add(picked);
                UIManager.Instance?.UpdateCardSlots(heldCards);
            }
            else
            {
                pendingReplacementCard = picked;
                isReplaceMode = true;
                UIManager.Instance?.UpdateCardSlots(heldCards, true);
                yield break;
            }
        }

        UIManager.Instance?.UpdateCardSlots(heldCards);
        isReplaceMode = false;
        TryOpenNextSelection();
    }

    /// <summary>ยกเลิกโหมดแทนที่ แล้วเปิดชุดเดิมซ้ำอีกครั้ง</summary>
    public void CancelReplacement()
    {
        if (!isReplaceMode && !(uiSelect != null && (uiSelect.IsWaitingReplace || uiSelect.HasActiveClone)))
            return;

        pendingReplacementCard = null;
        isReplaceMode = false;

        UIManager.Instance?.HideMessage();
        UIManager.Instance?.UpdateCardSlots(heldCards);

        if (uiSelect != null) uiSelect.OnReplaceCanceled();

        if (uiSelect != null && lastOptions != null && lastOptions.Count > 0)
            uiSelect.Open(lastOptions, OnCardPicked);
        else
            TryOpenNextSelection();
    }

    public void ReplaceSlot(int index)
    {
        if (index < 0 || index >= heldCards.Count) return;

        if (uiSelect != null && (uiSelect.IsWaitingReplace || uiSelect.HasActiveClone))
        {
            StartCoroutine(ReplaceWithAnim(index));
            return;
        }

        if (!isReplaceMode || pendingReplacementCard == null) return;

        heldCards[index] = pendingReplacementCard;
        pendingReplacementCard = null;
        isReplaceMode = false;

        UIManager.Instance?.HideMessage();
        UIManager.Instance?.UpdateCardSlots(heldCards);
        TryOpenNextSelection();
    }

    private IEnumerator ReplaceWithAnim(int index)
    {
        yield return uiSelect.AnimatePendingToSlot(index);

        heldCards[index] = pendingReplacementCard;
        pendingReplacementCard = null;
        isReplaceMode = false;

        UIManager.Instance?.HideMessage();
        UIManager.Instance?.UpdateCardSlots(heldCards);
        TryOpenNextSelection();
    }

    /// <summary>ใช้การ์ดในช่อง index (ยืนยัน → เช็กจำนวนต่อเทิร์น → เช็ก Mana → ApplyEffect)</summary>
    public void UseCard(int index)
    {
        if (index < 0 || index >= heldCards.Count) return;

        var card = heldCards[index];
        if (card == null) return;

        // popup ยืนยัน (ยังไม่หัก mana)
        UIConfirmPopup.Show(
            $"ใช้การ์ด '{card.displayName}' ({card.Mana} Mana)?",
            () =>
            {
                // ▶ กด Confirm
                if (TurnManager.Instance == null)
                {
                    UIManager.Instance?.ShowMessage("TurnManager ไม่พร้อม", 1.2f);
                    return;
                }

                // 1) จำกัดจำนวนใช้ต่อเทิร์น
                if (!TurnManager.Instance.CanUseCard(card))
                {
                    UIManager.Instance?.ShowMessage("เกินจำนวนที่ใช้ได้", 2f);
                    // ตามโค้ดเดิม: ลบการ์ดทันที
                    heldCards.RemoveAt(index);
                    UIManager.Instance?.UpdateCardSlots(heldCards);
                    return;
                }

                // 2) เช็กมานา
                int cost = card.Mana;
                if (!TurnManager.Instance.UseMana(cost))
                {
                    UIManager.Instance?.ShowMessage($"Mana ไม่พอ (ต้องใช้ {cost})", 2f);
                    return;
                }

                // 3) ใช้เอฟเฟกต์
                ApplyEffect(card);

                // 4) จดว่าใช้ไปแล้วในเทิร์นนี้
                TurnManager.Instance.OnCardUsed(card);

                // 5) ลบการ์ดจากมือ + อัปเดต UI
                heldCards.RemoveAt(index);
                UIManager.Instance?.UpdateCardSlots(heldCards);
            },
            () =>
            {
                // ▶ Cancel: ยังไม่มีการหัก mana ก่อนหน้านี้ จึงไม่ต้องคืน
            }
        );
    }

    /// <summary>ลองฟิวชันการ์ดจากช่อง A → B; ถ้าสำเร็จจะเขียนผลทับช่อง B และลบ A</summary>
    public bool TryFuseByIndex(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return false;
        if (fromIndex < 0 || fromIndex >= heldCards.Count ||
            toIndex   < 0 || toIndex   >= heldCards.Count)
        {
            UIManager.Instance?.ShowMessage("ไม่สามารถ fusion ได้", 1.2f);
            return false;
        }

        if (fusionTable == null)
        {
            UIManager.Instance?.ShowMessage("FusionTable ไม่พร้อม", 1.2f);
            return false;
        }

        var a = heldCards[fromIndex];
        var b = heldCards[toIndex];
        var result = fusionTable.TryFuse(a, b);

        if (result == null)
        {
            UIManager.Instance?.ShowMessage("ไม่สามารถ fusion ได้", 1.2f);
            return false;
        }

        heldCards[toIndex] = result;
        // ลบใบ A ออก; การ set ช่อง B แล้วทำให้ index ขยับไม่เป็นปัญหา
        heldCards.RemoveAt(fromIndex);

        UIManager.Instance?.UpdateCardSlots(heldCards);
        UIManager.Instance?.ShowMessage($"Fusion: {a.displayName} + {b.displayName} → {result.displayName}", 2f);
        return true;
    }

    /// <summary>ย้ายการ์ดจาก fromIndex ไปยังตำแหน่ง toIndex (แทรก)</summary>
    public void MoveCard(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= heldCards.Count) return;
        if (toIndex < 0) return;

        var card = heldCards[fromIndex];
        heldCards.RemoveAt(fromIndex);

        if (toIndex > heldCards.Count) toIndex = heldCards.Count;
        heldCards.Insert(toIndex, card);

        UIManager.Instance?.UpdateCardSlots(heldCards);
    }

    #endregion
    // =======================================================================

    #region Effects

    /// <summary>เรียกใช้เอฟเฟกต์ตามชนิดของการ์ด (CardEffectType)</summary>
    private void ApplyEffect(CardData card)
    {
        if (card == null) return;

        switch (card.effectType)
        {
            // 1) DL → x4
            case CardEffectType.LetterQuadSurge:
                ScoreManager.SetDoubleLetterOverride(4);
                UIManager.Instance?.ShowMessage("ช่อง DL กลายเป็น x4 ในตานี้!", 2);
                break;

            // 2) DL → x6
            case CardEffectType.LetterHexSurge:
                ScoreManager.SetDoubleLetterOverride(6);
                UIManager.Instance?.ShowMessage("ช่อง DL กลายเป็น x6 ในตานี้!", 2);
                break;

            // 3) DW → x4
            case CardEffectType.WordQuadSurge:
                ScoreManager.SetDoubleWordOverride(4);
                UIManager.Instance?.ShowMessage("ช่อง DW กลายเป็น x4 ในตานี้!", 2);
                break;

            // 4) DW → x6
            case CardEffectType.WordHexSurge:
                ScoreManager.SetDoubleWordOverride(6);
                UIManager.Instance?.ShowMessage("ช่อง DW กลายเป็น x6 ในตานี้!", 2);
                break;

            // 5) เติม Bench 2
            case CardEffectType.TwinDraw:
                for (int i = 0; i < 2; i++) BenchManager.Instance?.RefillOneSlot();
                UIManager.Instance?.ShowMessage("Twin Draw – เติม 2 ตัวอักษร!", 2);
                break;

            // 6) เติม Bench 4
            case CardEffectType.QuadSupply:
                for (int i = 0; i < 4; i++) BenchManager.Instance?.RefillOneSlot();
                UIManager.Instance?.ShowMessage("Quad Supply – เติม 4 ตัวอักษร!", 2);
                break;

            // 7) เติม Bench ทุกช่องว่าง
            case CardEffectType.BenchBlitz:
                BenchManager.Instance?.RefillEmptySlots();
                UIManager.Instance?.ShowMessage("Bench Blitz – เติมครบทุกช่องว่าง!", 2);
                break;

            // 8) จั่วการ์ดเพิ่ม 2 ใบ
            case CardEffectType.DoubleRecast:
                for (int i = 0; i < 2; i++) GiveRandomCard();
                UIManager.Instance?.ShowMessage("Pick new card", 2);
                break;

            // 9) คำถัดไป x2
            case CardEffectType.EchoBurst:
                TurnManager.Instance?.SetScoreMultiplier(2);
                UIManager.Instance?.ShowMessage("Echo Burst! คำนี้คูณ ×2 ทันที", 2);
                break;

            // 10) Full Rerack
            case CardEffectType.FullRerack:
                BenchManager.Instance?.FullRerack();
                UIManager.Instance?.ShowMessage("Full Rerack — สุ่ม Bench ใหม่ทั้งหมด!", 2);
                break;

            // 11) Glyph Spark – แทนที่ 1 ตัวบน Bench ด้วย special
            case CardEffectType.GlyphSpark:
                BenchManager.Instance?.ReplaceRandomWithSpecial(1);
                UIManager.Instance?.ShowMessage("Glyph Spark — หนึ่งตัวใน Bench เป็นตัวพิเศษ!", 2);
                break;

            // 12) Twin Sparks – แทนที่ 2 ตัวบน Bench ด้วย special
            case CardEffectType.TwinSparks:
                BenchManager.Instance?.ReplaceRandomWithSpecial(2);
                UIManager.Instance?.ShowMessage("Twin Sparks — สองตัวใน Bench เป็นตัวพิเศษ!", 2);
                break;

            // 13) Free Pass – ยกเลิกโทษดิกในเทิร์นนี้
            case CardEffectType.FreePass:
                TurnManager.Instance?.ApplyFreePass();
                break;

            // 14) Minor Infusion +2 Mana
            case CardEffectType.MinorInfusion:
                TurnManager.Instance?.AddMana(2);
                break;

            // 15) Major Infusion +5 Mana
            case CardEffectType.MajorInfusion:
                TurnManager.Instance?.AddMana(5);
                break;

            // 16) Mana Overflow – เติมจนเต็ม
            case CardEffectType.ManaOverflow:
                if (TurnManager.Instance != null)
                    TurnManager.Instance.AddMana(TurnManager.Instance.maxMana);
                break;

            // 17) Wild Bloom – เพิ่มช่องพิเศษสุ่ม 10 ช่อง
            case CardEffectType.WildBloom:
                BoardManager.Instance?.AddRandomSpecialSlots(10);
                UIManager.Instance?.ShowMessage("Wild Bloom — เพิ่มช่องพิเศษแบบสุ่ม 10 ช่อง!", 2f);
                break;

            // 18) Chaos Bloom – เพิ่มช่องพิเศษสุ่ม 25 ช่อง
            case CardEffectType.ChaosBloom:
                BoardManager.Instance?.AddRandomSpecialSlots(25);
                UIManager.Instance?.ShowMessage("Chaos Bloom — เพิ่มช่องพิเศษแบบสุ่ม 25 ช่อง!", 2f);
                break;

            // 19) Targeted Flux – เลือก 5 ช่องให้เป็น special
            case CardEffectType.TargetedFlux:
                BoardManager.Instance?.StartTargetedFlux(5);
                UIManager.Instance?.ShowMessage("Targeted Flux — คลิกเลือก 5 ช่องเพื่อเป็นช่องพิเศษ!", 2f);
                break;

            // 20) Clean Slate – ล้างตัวอักษรทั้งหมดบนบอร์ด
            case CardEffectType.CleanSlate:
                BoardManager.Instance?.CleanSlate();
                UIManager.Instance?.ShowMessage("Clean Slate — ล้างตัวอักษรทั้งหมดบนกระดาน!", 2f);
                break;

            // 21) Global Echo – ตัวอักษรทั้งหมด x2 ชั่วคราว (1 นาที)
            case CardEffectType.GlobalEcho:
                ScoreManager.ActivateGlobalLetterMultiplier(2, 60f);
                UIManager.Instance?.ShowMessage("Letter Double Time – ตัวอักษรทั้งหมด ×2 เป็นเวลา 1 นาที!", 2f);
                break;

            // 22) Pandemonium Field – ทุกช่องเป็น special แบบสุ่ม (1 นาที)
            case CardEffectType.PandemoniumField:
                BoardManager.Instance?.ActivateAllRandomSpecial(60f);
                // ข้อความแสดงในเมธอดข้างในแล้ว
                break;

            // 23) Card Refresh – รีเซ็ตจำนวนใช้การ์ดในเทิร์นนี้
            case CardEffectType.CardRefresh:
                TurnManager.Instance?.ResetCardUsage();
                break;

            // 24) Infinite Tiles – tilepack ไม่จำกัด 1 นาที
            case CardEffectType.InfiniteTiles:
                TileBag.Instance?.ActivateInfinite(60f);
                UIManager.Instance?.ShowMessage("Infinite Tiles – tilepack ไม่จำกัด 1 นาที!", 2f);
                break;

            // 25) Pack Renewal – รีเซ็ต tilepack
            case CardEffectType.PackRenewal:
                TileBag.Instance?.ResetPool();
                UIManager.Instance?.ShowMessage("Pack Renewal – รีเซ็ต tilepack ใหม่ทั้งหมด!", 2f);
                break;

            // 26) Mana Infinity – มานาไม่จำกัด 1 นาที
            case CardEffectType.ManaInfinity:
                TurnManager.Instance?.ActivateInfiniteMana(60f);
                break;

            // 27) OmniSpark – เปลี่ยน Bench ทั้งหมดเป็น special (ตาม LetterTile/skin รองรับ)
            case CardEffectType.OmniSpark:
                BenchManager.Instance?.OmniSpark();
                UIManager.Instance?.ShowMessage("Omni Spark – ทุกตัวใน Bench เป็น special ชั่วคราว!", 2f);
                break;

            // 28) MasterDraft – เลือกการ์ดจากคลังทั้งหมด (ยกเว้น wildcard ตามดีไซน์)
            case CardEffectType.MasterDraft:
                OnUseMasterDraft();
                break;
        }
    }

    #endregion
}
