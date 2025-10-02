using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;

public class SimpleTutorialManager : MonoBehaviour
{
    [Header("Config")]
    public SimpleTutorialSequenceSO sequence;

    [Header("Anchors")]
    public Canvas targetCanvas;
    public RectTransform subtitleDefaultAnchor;  // จุดตั้งต้น (เช่น ล่างกลาง)
    public RectTransform subtitleAltA;           // จุดสำรอง A (เช่น เหนือ Bench)
    public RectTransform subtitleAltB;           // จุดสำรอง B (เช่น ขวาบน)
    RectTransform tempAnchor;  // สร้างใช้ชั่วคราวสำหรับกรอบ union
    public RectTransform benchAnchor; // ลากกรอบ UI ของ Bench มาใส่
    public RectTransform spaceAnchor;       // โซนแถว Space (ทั้งแถว)
    public RectTransform tilePackAnchor;    // ถุงไทล์ หรือใช้ BenchManager.tileSpawnAnchor
    public RectTransform dictionaryAnchor;  // ปุ่ม/ไอคอน Dictionary
    public RectTransform manaAnchor; // NEW: ลาก Rect ของ "Mana: x/x" มาวางที่นี่
    public RectTransform taskAnchor;        // Task panel
    public RectTransform confirmAnchor;     // ปุ่ม Confirm
    public RectTransform clearAnchor;       // ปุ่ม Clear
    public RectTransform discardAnchor;     // ปุ่ม Discard (ตัวที่ใช้ไฮไลต์)
    public RectTransform timeAnchor;        // ข้อความเวลา
    public RectTransform scoreAnchor;       // ข้อความคะแนน
    public RectTransform levelAnchor;       // ข้อความเลเวล

    [Header("SHOP Anchors")]
    public RectTransform coinAnchor;
    public RectTransform manaInfoAnchor;      // "Mana : 00"
    public RectTransform cardSlotInfoAnchor;  // "Card Slot : 00"
    public RectTransform tilePackInfoAnchor;  // "TilePack : 000"

    public RectTransform maxManaUpAnchor;
    public RectTransform maxTilepackUpAnchor;
    public RectTransform maxCardslotUpAnchor;

    public RectTransform priceManaAnchor;
    public RectTransform priceTilepackAnchor;
    public RectTransform priceCardslotAnchor;

    public RectTransform progressManaAnchor;
    public RectTransform progressTilepackAnchor;
    public RectTransform progressCardslotAnchor;

    public RectTransform buyCard1Anchor;
    public RectTransform buyCard2Anchor;
    public RectTransform buyCard3Anchor;

    public RectTransform rerollAnchor;
    public RectTransform nextAnchor;

    [Header("Optional Roots")]
    public GameObject discardRoot;          // รากของปุ่ม/กลุ่ม Discard (ไว้เปิด/ปิดทั้งก้อน)

    [Header("UI")]
    public SimpleTutorialUI ui;

    int index = -1;
    bool running = false;
    Coroutine watcherCo; // เฝ้ารอ special tile
    bool discardPrevActive = false;
    SimpleTutorialStep _prevStep = null;
    const string TUT_SESSION_KEY = "TUT_ENABLE_SESSION";
    bool canAdvanceStep = false;
    float advanceCooldownUntil = 0f;
    [SerializeField] float firstStepInputBlock = 0.15f; // กันคลิก/คีย์ค้างจากก่อนเข้าทิวทอเรียล
    [SerializeField] float skipToNextCooldown = 0.3f;   // กันกดเพื่อจบการพิมพ์แล้วทะลุไปสเต็ปถัดไป
    string SceneSeenKey =>
    (sequence != null)
    ? $"{sequence.seenPlayerPrefKey}@{SceneManager.GetActiveScene().name}"
    : $"SIMPLE_TUTORIAL_SCENE_SEEN@{SceneManager.GetActiveScene().name}";
    string GlobalSeenKey =>
    (sequence != null)
    ? sequence.seenPlayerPrefKey
    : "SIMPLE_TUTORIAL_SEEN_V1";

    float _blockInputUntil;       // ใช้กันเหตุสเต็ปแรกข้าม
    float _cooldownUntil;         // ใช้กันกดทะลุไปสเต็ปถัด
    bool _overlayClickQueued = false;
    void Start()
    {
        if (ui == null) ui = SimpleTutorialUI.Instance;
        if (ui != null && targetCanvas != null) ui.SetMainCanvas(targetCanvas);

        if (sequence == null) return;

        bool sessionWant = PlayerPrefs.GetInt(TUT_SESSION_KEY, 0) == 1;

        if (sessionWant)
        {
            // ผู้เล่นกด “Yes” ที่ MainMenu → บังคับให้รันในรอบนี้
            StartSequence();
        }
        else
        {
            int globalSeen = PlayerPrefs.GetInt(GlobalSeenKey, 0);  // ✅ กด “No” ที่ MainMenu จะเซ็ตเป็น 1
            int sceneSeen  = PlayerPrefs.GetInt(SceneSeenKey, 0);

            // เงื่อนไข auto-run: ถ้า global ยังไม่เคยดู “และ” (ตั้งให้รันครั้งแรก หรือ ซีนนี้ยังไม่เคยดู)
            bool shouldAutoRun = (globalSeen == 0) && (!sequence.runOnFirstLaunch || sceneSeen == 0);

            if (shouldAutoRun)
                StartSequence();
        }
    }
    void ExitPreviousStepIfAny()
    {
        if (_prevStep != null && _prevStep.forceShowDiscardDuringStep && _prevStep.revertDiscardStateOnExit)
        {
            if (discardRoot) discardRoot.SetActive(discardPrevActive);
        }
        _prevStep = null;
    }

    public void StartSequence()
    {
        if (running) return;
        enabled = true;
        running = true;
        index = -1;

        // เปิดปุ่ม Skip — ปิดเฉพาะซีนนี้ ไม่แตะ PlayerPrefs และไม่ปิด session key
        ui.SetSkip(() =>
        {
            // ปิดทิวทอเรียลเฉพาะซีนนี้ และ markSeen รายซีนตามปกติ
            EndSequence(markSeen: true);
        }, true);

        Next();
    }
    public void SetAdvanceEnabled(bool enable) => canAdvanceStep = enable;

    void Update()
    {
        
        if (!running) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ปิดเฉพาะซีนนี้
            EndSequence(markSeen: true);
            return;
        }
        // กันอินพุตค้างจากก่อนเข้า/ก่อนขึ้นสเต็ป
        if (Time.unscaledTime < _blockInputUntil)
        {
            // เคลียร์คิวคลิกทิ้งไปเลยในช่วงบล็อก เพื่อไม่ให้กดค้างแล้วทะลุ
            _overlayClickQueued = false;
            return;
        }

        // รวมอินพุต: anykey + เมาส์ + คลิกจาก overlay (คิวไว้)
        bool anyPress =
            Input.anyKeyDown ||                         // ปุ่มคีย์บอร์ดทั้งหมด (ไม่รวมเมาส์)
            Input.GetMouseButtonDown(0) ||              // ซ้าย
            Input.GetMouseButtonDown(1) ||              // ขวา
            Input.GetMouseButtonDown(2) ||              // กลาง
            _overlayClickQueued;                        // คลิกผ่าน overlay UI

        if (!anyPress) return;

        // กินคิวคลิก overlay (ป้องกันถูกใช้ซ้ำในเฟรมถัดไป)
        _overlayClickQueued = false;

        // ขั้นที่ 1: ถ้ากำลังพิมพ์อยู่ → กด = จบการพิมพ์ทันที แต่ "ไม่" ไป Next
        if (ui != null && ui.IsTyping)
        {
            if (ui.TryFinishTypingNow())
            {
                _cooldownUntil = Time.unscaledTime + skipToNextCooldown; // กันกดครั้งเดียวแล้วทะลุ Next
            }
            return;
        }

        // ขั้นที่ 2: ถ้าไม่ได้พิมพ์แล้ว → พ้นคูลดาวน์ค่อย Next
        if (Time.unscaledTime >= _cooldownUntil)
        {
            Next();
        }
    }
    public void OnOverlayClicked()
    {
        if (!running) return;        // ✅ กันคลิกฝัง overlay หลังปิดไปแล้ว
        _overlayClickQueued = true;
    }
    public void Next()
    {
        if (!running) return;
        // ตัด watcher เก่า
        if (watcherCo != null) { StopCoroutine(watcherCo); watcherCo = null; }

        // ออกจาก step ก่อนหน้า (คืนค่า discard ถ้าต้อง)
        ExitPreviousStepIfAny();

        index++;
        if (sequence == null || index >= sequence.steps.Count) { EndSequence(); return; }

        var step = sequence.steps[index];
        _prevStep = step;

        // ถ้าต้องบังคับโชว์ Discard ใน step นี้
        if (step.forceShowDiscardDuringStep && discardRoot)
        {
            discardPrevActive = discardRoot.activeSelf;
            if (!discardRoot.activeSelf) discardRoot.SetActive(true);
        }

        // overlay/dim + subtitle เหมือนเดิม
        ui.ConfigureOverlay(step.clickAnywhereToAdvance ? (System.Action)OnOverlayClicked : null,
                            enableDim: step.dimBackground);

        RectTransform subAnchor = step.subtitleUseSlot ? MapSubtitleSlot(step.subtitleSlot)
                                                    : MapAnchor(step.subtitleAnchor);
        ui.ShowSubtitle(step.text, subAnchor, step.subtitleOffset, step.typewriterSeconds);
        if (step.speakerShow && step.speakerSprite != null && ui != null)
        {
            RectTransform spAnchor = step.speakerUseSlot
                ? MapSubtitleSlot(step.speakerSlot)   // วางตามตำแหน่ง slot เดียวกับ subtitle
                : MapAnchor(step.speakerAnchor);      // หรือวางตาม anchor ออบเจ็กต์

            ui.ShowSpeaker(step.speakerSprite, spAnchor, step.speakerOffset, step.speakerMirrorX, step.speakerAlpha);
        }
        else
        {
            // ไม่แสดงในสเต็ปนี้ (หรือไม่มีรูป) → ซ่อน
            if (ui != null) ui.HideSpeaker();
        }
        _blockInputUntil = Time.unscaledTime + firstStepInputBlock; // กันอินพุตค้างเฟรมแรก
        _cooldownUntil   = Time.unscaledTime;                       // รีเซ็ตคูลดาวน์
        _overlayClickQueued = false;

        // highlight
        if (step.type == SimpleTutStepType.SubtitleAndHighlight)
        {
            var targets = GetTargets(step.highlightTarget);
            if (step.waitForSpecialTile || step.highlightTarget == SimpleTutAnchor.SpecialTileFirst)
            {
                ui.ConfigureOverlay(onClick: null, enableDim: step.dimBackground);
                ui.ShowHighlights(null, step.highlightPadding);
                watcherCo = StartCoroutine(WaitAndHighlightSpecialThenEnable(step)); // (ตัวนี้ยัง single ตามเดิม)
            }
            else
            {
                ui.ShowHighlights(targets, step.highlightPadding);
            }
        }
        else
        {
            ui.ShowHighlight(null, 0);
        }
        SetAdvanceEnabled(true);
        advanceCooldownUntil = Time.unscaledTime + 0.05f; // กันเด้งซ้ำเฟรมแรกหลังเจอ
    }
    public void EndSequence(bool markSeen = true)
    {
        if (watcherCo != null) { StopCoroutine(watcherCo); watcherCo = null; }
        ExitPreviousStepIfAny();
        running = false;
        ui.HideAll();

        // เดิม: PlayerPrefs.SetInt(sequence.seenPlayerPrefKey, 1);
        if (markSeen && sequence && sequence.runOnFirstLaunch)
            PlayerPrefs.SetInt(SceneSeenKey, 1);

        enabled = false;
    }

    RectTransform EnsureTempAnchor()
    {
        if (tempAnchor) return tempAnchor;
        var go = new GameObject("TutTempAnchor", typeof(RectTransform));
        go.transform.SetParent(targetCanvas ? targetCanvas.transform : transform, false);
        tempAnchor = go.GetComponent<RectTransform>();
        return tempAnchor;
    }

    RectTransform MapAnchor(SimpleTutAnchor a)
    {
        switch (a)
        {
            case SimpleTutAnchor.Bench:      return benchAnchor;
            case SimpleTutAnchor.Space:      return BuildSpaceUnionAnchor();
            case SimpleTutAnchor.TilePack:   return tilePackAnchor ? tilePackAnchor
                                            : (BenchManager.Instance ? BenchManager.Instance.tileSpawnAnchor : null);
            case SimpleTutAnchor.Dictionary: return dictionaryAnchor;
            case SimpleTutAnchor.Mana:       return manaAnchor;
            case SimpleTutAnchor.CardSlots:  return BuildCardSlotsUnionAnchor();

            // ==== NEW ====
            case SimpleTutAnchor.Task:       return taskAnchor;
            case SimpleTutAnchor.Confirm:    return confirmAnchor;
            case SimpleTutAnchor.Clear:      return clearAnchor;
            case SimpleTutAnchor.Discard:    return discardAnchor; // (ปุ่มจะถูกเปิดให้เห็นใน Next() ตาม step)
            case SimpleTutAnchor.Time:       return timeAnchor;
            case SimpleTutAnchor.Score:      return scoreAnchor;
            case SimpleTutAnchor.Level:      return levelAnchor;

            case SimpleTutAnchor.ScreenCenter:return (RectTransform)targetCanvas.transform;
            case SimpleTutAnchor.SpecialTileFirst: return null;
            //---Shop---//
            case SimpleTutAnchor.Coin:               return coinAnchor;
            case SimpleTutAnchor.ManaInfo:     return manaInfoAnchor;
            case SimpleTutAnchor.CardSlotInfo: return cardSlotInfoAnchor;
            case SimpleTutAnchor.TilePackInfo: return tilePackInfoAnchor;

            case SimpleTutAnchor.MaxManaUpgrade:     return maxManaUpAnchor;
            case SimpleTutAnchor.MaxTilepackUpgrade: return maxTilepackUpAnchor;
            case SimpleTutAnchor.MaxCardslotUpgrade: return maxCardslotUpAnchor;

            case SimpleTutAnchor.Price_Mana:         return priceManaAnchor;
            case SimpleTutAnchor.Price_Tilepack:     return priceTilepackAnchor;
            case SimpleTutAnchor.Price_Cardslot:     return priceCardslotAnchor;

            case SimpleTutAnchor.Progress_Mana:      return progressManaAnchor;
            case SimpleTutAnchor.Progress_Tilepack:  return progressTilepackAnchor;
            case SimpleTutAnchor.Progress_Cardslot:  return progressCardslotAnchor;

            case SimpleTutAnchor.BuyCard_1:          return buyCard1Anchor ?? GetShopSlotRT(0);
            case SimpleTutAnchor.BuyCard_2:          return buyCard2Anchor ?? GetShopSlotRT(1);
            case SimpleTutAnchor.BuyCard_3:          return buyCard3Anchor ?? GetShopSlotRT(2);

            case SimpleTutAnchor.Reroll:             return rerollAnchor;
            case SimpleTutAnchor.Next:               return nextAnchor;

            default: return null;
        }
    }
    RectTransform MapSubtitleSlot(SubtitleAnchorSlot slot)
    {
        switch (slot)
        {
            case SubtitleAnchorSlot.AltA:        return subtitleAltA    ? subtitleAltA    : subtitleDefaultAnchor;
            case SubtitleAnchorSlot.AltB:        return subtitleAltB    ? subtitleAltB    : subtitleDefaultAnchor;
            case SubtitleAnchorSlot.ScreenCenter:return (RectTransform)targetCanvas.transform;
            default:                             return subtitleDefaultAnchor ? subtitleDefaultAnchor
                                                                            : (RectTransform)targetCanvas.transform;
        }
    }
    RectTransform GetShopSlotRT(int index)
    {
        var sm = ShopManager.Instance;
        if (sm == null) return null;
        // สมมุติว่ามี array slots ภายใน ShopManager/หรือหา ShopCardSlot ในฉาก
    #if UNITY_2023_1_OR_NEWER
        var slots = Object.FindObjectsByType<ShopCardSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    #else
        var slots = GameObject.FindObjectsOfType<ShopCardSlot>(true);
    #endif
        if (slots != null && slots.Length > index) return slots[index]?.GetComponent<RectTransform>();
        return null;
    }
    // ==== helper: รวมรายการ RectTransform (ข้าม null) ====
    List<RectTransform> MakeList(params RectTransform[] arr)
    {
        var list = new List<RectTransform>();
        foreach (var rt in arr) if (rt) list.Add(rt);
        return list;
    }

    // ==== ดึงกลุ่มเป้าหมายตาม anchor ====
    List<RectTransform> GetTargets(SimpleTutAnchor a)
    {
        switch (a)
        {
            // --- กลุ่มใหม่ ---
            case SimpleTutAnchor.Price_All:
                return MakeList(priceManaAnchor, priceTilepackAnchor, priceCardslotAnchor);

            case SimpleTutAnchor.Progress_All:
                return MakeList(progressManaAnchor, progressTilepackAnchor, progressCardslotAnchor);

            case SimpleTutAnchor.BuyCard_All:
            {
                var a1 = buyCard1Anchor ?? GetShopSlotRT(0);
                var a2 = buyCard2Anchor ?? GetShopSlotRT(1);
                var a3 = buyCard3Anchor ?? GetShopSlotRT(2);
                return MakeList(a1, a2, a3);
            }

            // --- เดี่ยว (ห่อเป็นลิสต์ 1 ตัว) ---
            default:
                var single = MapAnchor(a);
                return single ? new List<RectTransform> { single } : new List<RectTransform>();
        }
    }

    // เรียกจากปุ่มเพื่อรีเซ็ตสถานะว่าเคยดู แล้วเริ่มทิวทอเรียลใหม่ทันที
    public void ResetTutorialAndRestart()
    {
        if (sequence == null) return;
        PlayerPrefs.DeleteKey(SceneSeenKey);  // เดิมลบ sequence.seenPlayerPrefKey
        PlayerPrefs.Save();
        ui.HideAll();
        running = false;
        StartSequence();
    }

    public void ResetTutorialFlagOnly()
    {
        if (sequence == null) return;
        PlayerPrefs.DeleteKey(SceneSeenKey);  // เดิมลบ sequence.seenPlayerPrefKey
        PlayerPrefs.Save();
    }
    // ---------- Builders ----------
    RectTransform BuildSpaceUnionAnchor()
    {
        if (spaceAnchor) return spaceAnchor;

        var sm = SpaceManager.Instance;
        if (sm == null || sm.slotTransforms == null || sm.slotTransforms.Count == 0) return null;

        var list = new List<RectTransform>();
        foreach (var t in sm.slotTransforms) if (t) { var rt = t as RectTransform; if (rt) list.Add(rt); }
        return BuildUnionFrom(list);
    }

    RectTransform BuildCardSlotsUnionAnchor()
    {
        var rts = FindCardSlotRects();
        if (rts.Count == 0) return null;
        return BuildUnionFrom(rts);
    }

    RectTransform BuildUnionFrom(List<RectTransform> rects)
    {
        if (rects == null || rects.Count == 0) return null;

        var canvasRT = (RectTransform)targetCanvas.transform;
        var cam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;

        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;
        var wc = new Vector3[4];

        foreach (var rt in rects)
        {
            if (!rt) continue;
            rt.GetWorldCorners(wc);
            Vector2 a, b;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT,
                RectTransformUtility.WorldToScreenPoint(cam, wc[0]), cam, out a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT,
                RectTransformUtility.WorldToScreenPoint(cam, wc[2]), cam, out b);

            if (!any) { min = Vector2.Min(a, b); max = Vector2.Max(a, b); any = true; }
            else { min = Vector2.Min(min, Vector2.Min(a, b)); max = Vector2.Max(max, Vector2.Max(a, b)); }
        }
        if (!any) return null;

        var anchor = EnsureTempAnchor();
        var size = max - min;
        anchor.anchoredPosition = (min + max) * 0.5f;
        anchor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Abs(size.x));
        anchor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Abs(size.y));
        return anchor;
    }

    List<RectTransform> FindCardSlotRects()
    {
        var list = new List<RectTransform>();

#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<CardSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = GameObject.FindObjectsOfType<CardSlotUI>(true);
#endif
        if (all != null && all.Length > 0)
        {
            foreach (var s in all) { var rt = s.GetComponent<RectTransform>(); if (rt) list.Add(rt); }
        }
        else
        {
            // fallback: ค้นตามชื่อ "Cardslot1".."Cardslot8"
            for (int i = 1; i <= 8; i++)
            {
                var go = GameObject.Find($"Cardslot{i}");
                if (go) { var rt = go.GetComponent<RectTransform>(); if (rt) list.Add(rt); }
            }
        }
        return list;
    }

    // ---------- Special-tile watcher ----------
    System.Collections.IEnumerator WaitAndHighlightSpecialThenEnable(SimpleTutorialStep step)
    {
        RectTransform targetRt = null;

        while (true)
        {
            // กันกดข้ามขณะอนิเมชันเติม/คิดคะแนน
            if (BenchManager.Instance != null && BenchManager.Instance.IsRefilling())
            { yield return null; continue; }
            if (TurnManager.Instance != null && TurnManager.Instance.IsScoringAnimation)
            { yield return null; continue; }

            targetRt = FindFirstSpecialTileRect(step.specialWhere);
            if (targetRt)
            {
                ui.ConfigureOverlay(step.clickAnywhereToAdvance ? (System.Action)CallTryAdvance : null,
                    enableDim: step.dimBackground);
                // เปิดให้กดไปต่อได้ตอนนี้
                ui.ConfigureOverlay(step.clickAnywhereToAdvance ? (System.Action)Next : null, enableDim: step.dimBackground);
                break;
            }

            // ยังไม่พบ → ซ่อนไฮไลต์ไว้ก่อน รอต่อ
            ui.ShowHighlight(null, 0);
            yield return null;
        }
        watcherCo = null;
    }
    public void CallTryAdvance()
    {
        if (!running || !canAdvanceStep) return;
        // overlay คลิกถือเป็น any key อยู่แล้ว แต่เผื่อมีคนเรียกเมธอดนี้จากที่อื่น
        canAdvanceStep = false;
        Next();
    }

    RectTransform FindFirstSpecialTileRect(StepSpecialScope where)
    {
        // Bench
        if (where == StepSpecialScope.Any || where == StepSpecialScope.Bench)
        {
            foreach (var t in BenchManager.Instance?.GetAllBenchTiles() ?? System.Linq.Enumerable.Empty<LetterTile>())
                if (t && t.IsSpecial) return t.GetComponent<RectTransform>();
        }
        // Space
        if (where == StepSpecialScope.Any || where == StepSpecialScope.Space)
        {
            foreach (var t in SpaceManager.Instance?.GetPreparedTiles() ?? new List<LetterTile>())
                if (t && t.IsSpecial) return t.GetComponent<RectTransform>();
        }
        return null;
    }

}
