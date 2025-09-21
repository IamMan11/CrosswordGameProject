using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Config / UI")]
    [SerializeField] private TutorialConfigSO config;
    [SerializeField] private TutorialUI ui;

    [Header("Bindings (ตำแหน่งที่จะโฟกัส) - Play scene")]
    public RectTransform boardArea;
    public RectTransform benchArea;
    public RectTransform confirmButton;
    public RectTransform dictionaryButton;
    public RectTransform manaUI;
    public RectTransform bagUI;

    private int index = -1;
    private bool running = false;
    private readonly Queue<TutorialEvent> eventQueue = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (config == null || ui == null) return;

        ui.BindButtons(Next, Back, SkipAll);

        if (config.runOnFirstLaunch && PlayerPrefs.GetInt(config.seenKey, 0) == 0)
            StartTutorial();
    }

    void Update()
    {
        if (!running) return;

        var step = CurrentStep();
        if (step == null) return;

        if (step.advanceOnAnyKey && !step.waitForEvent)
        {
            if (Input.anyKeyDown && !EventSystem.current.IsPointerOverGameObject())
                Next();
        }

        if (step.waitForEvent && eventQueue.Count > 0)
        {
            if (eventQueue.Peek() == step.trigger)
            {
                eventQueue.Dequeue();
                Next();
            }
            else eventQueue.Dequeue();
        }
    }

    public void StartTutorial()
    {
        if (running) return;
        if (config == null || config.steps.Count == 0) return;

        running = true;
        index = -1;
        Next();
    }

    public void StopTutorial(bool markSeen)
    {
        running = false;
        ui.HideCard(true);
        if (markSeen && config != null)
        {
            PlayerPrefs.SetInt(config.seenKey, 1);
            PlayerPrefs.Save();
        }
        BoardManager.Instance?.ClearHighlights();
        ui.ClearSpotlightHole();
    }

    public void Next()
    {
        index++;
        if (index >= config.steps.Count)
        {
            StopTutorial(true);
            return;
        }
        ShowCurrent();
    }

    public void Back()
    {
        index = Mathf.Max(0, index - 1);
        ShowCurrent();
    }

    public void SkipAll() => StopTutorial(true);

    private void ShowCurrent()
    {
        var step = CurrentStep();
        if (step == null) return;

        // ---------- basic card / overlay ----------
        ui.SetBlockInput(step.blockInput);
        ui.SetPage(index, config.steps.Count);
        ui.SetCard(step.title, step.body, step.icon);

        ui.ShowCard(true);

        // ใช้ CardSlot เป็นเป้า ถ้า focus ระบุ CardSlot
        RectTransform focusTarget =
            (step.focus == FocusKey.CardSlot)
                ? GetCardSlotRect(step.cardSlotIndex)
                : MapFocus(step.focus);

        ui.SetFocus(focusTarget, step.focusPadding);

        var s = step.style;
        ui.SetDimAlpha(s.dimAlpha);
        ui.SetSkipVisible(s.showSkip);
        ui.SetButtonLabels(s.nextLabel, s.backLabel, s.skipLabel);
        ui.SetCardVerticalTexts(s.cardLeftVertical, s.cardRightVertical);
        ui.SetCardTilt(s.cardTiltDeg);

        bool anchorToTarget = (s.anchor == AnchorMode.FocusTarget);
        ui.SetStackAnchor(anchorToTarget ? focusTarget : null, anchorToTarget, s.stackOffset);

        // ---------- optional board highlights ----------
        BoardManager.Instance?.ClearHighlights();

        if (step.highlightOneCell)
        {
            if (step.highlightByCoordinate)
                BoardManager.Instance?.HighlightCell(step.highlightRow, step.highlightCol, 1.8f);
            else
                BoardManager.Instance?.HighlightFirstOfType(step.highlightType, 1.8f);
        }

        if (step.highlightCardSlot)
            BoardManager.Instance?.HighlightCardSlot(step.cardSlotIndex, 10.0f);

        // ---------- SHOP: spotlight hole for items row ----------
        ui.ClearSpotlightHole(); // กันค้างจากสเต็ปก่อน
        if (step.focus == FocusKey.ShopItemsRow)
        {
            var rects = CollectShopItemRects();
            if (rects.Count > 0)
            {
                // ซ่อนกรอบเหลือง แล้วเจาะ “รูโปร่ง” ครอบทั้งแถว
                ui.SetFocus(null, Vector2.zero);
                ui.SpotlightTargets(rects, 6f);
            }
        }
    }

    private TutorialStep CurrentStep()
    {
        if (config == null || index < 0 || index >= config.steps.Count) return null;
        return config.steps[index];
    }

    // เปลี่ยนคอนฟิกระหว่างซีน (เช่น Shop)
    public void SetConfig(TutorialConfigSO newConfig, bool startNow = true)
    {
        if (newConfig == null) return;
        if (running) StopTutorial(false);   // จบของเดิมก่อน (ไม่ mark seen)
        config = newConfig;
        if (startNow) StartTutorial();
    }

    private RectTransform MapFocus(FocusKey fk)
    {
        switch (fk)
        {
            // ====== PLAY ======
            case FocusKey.Board: return boardArea;
            case FocusKey.Bench: return benchArea;
            case FocusKey.ConfirmButton: return confirmButton;
            case FocusKey.DictionaryButton: return dictionaryButton;
            case FocusKey.ManaUI: return manaUI;
            case FocusKey.BagUI: return bagUI;

            // ====== SHOP (ผ่าน Binder) ======
            case FocusKey.ShopPanel:       return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.panelRoot   : null;
            case FocusKey.ShopCoin:        return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.coinText    : null;
            case FocusKey.ShopBuyMana:     return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.buyMana     : null;
            case FocusKey.ShopBuyTilePack: return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.buyTilePack : null;
            case FocusKey.ShopBuyMaxCard:  return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.buyMaxCard  : null;
            case FocusKey.ShopItemsRow:    return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.itemsRow    : null;
            case FocusKey.ShopItem1:       return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.item1       : null;
            case FocusKey.ShopItem2:       return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.item2       : null;
            case FocusKey.ShopItem3:       return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.item3       : null;
            case FocusKey.ShopReroll:      return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.rerollButton: null;
            case FocusKey.ShopNext:        return ShopTutorialBinder.Instance ? ShopTutorialBinder.Instance.nextButton  : null;

            default: return null;
        }
    }

    private List<RectTransform> CollectShopItemRects()
    {
        var list = new List<RectTransform>();
        var b = ShopTutorialBinder.Instance;
        if (!b) return list;

        void Add(RectTransform r) { if (r) list.Add(r); }
        Add(b.item1); Add(b.item2); Add(b.item3);

        // ถ้าไม่ได้ลาก item1-3 ให้เก็บลูกทั้งหมดของ itemsRow
        if (list.Count == 0 && b.itemsRow)
        {
            for (int i = 0; i < b.itemsRow.childCount; i++)
            {
                var rt = b.itemsRow.GetChild(i) as RectTransform;
                if (rt) list.Add(rt);
            }
        }
        return list;
    }

    private RectTransform GetCardSlotRect(int idx)
    {
#if UNITY_2023_1_OR_NEWER
        foreach (var s in Object.FindObjectsByType<CardSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s.slotIndex == idx) return s.GetComponent<RectTransform>();
#else
        foreach (var s in GameObject.FindObjectsOfType<CardSlotUI>(true))
            if (s.slotIndex == idx) return s.GetComponent<RectTransform>();
#endif
        var go = GameObject.Find($"Cardslot{idx + 1}");
        return go ? go.GetComponent<RectTransform>() : null;
    }

    public void Fire(TutorialEvent ev)
    {
        if (!running) return;
        eventQueue.Enqueue(ev);
    }
}
