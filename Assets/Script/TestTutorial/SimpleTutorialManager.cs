using System.Collections.Generic;
using UnityEngine;

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
    [Header("Optional Roots")]
    public GameObject discardRoot;          // รากของปุ่ม/กลุ่ม Discard (ไว้เปิด/ปิดทั้งก้อน)

    [Header("UI")]
    public SimpleTutorialUI ui;

    int index = -1;
    bool running = false;
    Coroutine watcherCo; // เฝ้ารอ special tile
    bool discardPrevActive = false;
    SimpleTutorialStep _prevStep = null;

    void Start()
    {
        if (sequence == null) return;

        if (!sequence.runOnFirstLaunch || PlayerPrefs.GetInt(sequence.seenPlayerPrefKey, 0) == 0)
        {
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
        running = true;
        index = -1;
        Next();
    }

    
    public void Next()
    {
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
        ui.ConfigureOverlay(step.clickAnywhereToAdvance ? (System.Action)Next : null,
                            enableDim: step.type == SimpleTutStepType.SubtitleAndHighlight && step.dimBackground);

        RectTransform subAnchor = step.subtitleUseSlot ? MapSubtitleSlot(step.subtitleSlot)
                                                    : MapAnchor(step.subtitleAnchor);
        ui.ShowSubtitle(step.text, subAnchor, step.subtitleOffset);

        // highlight
        if (step.type == SimpleTutStepType.SubtitleAndHighlight)
        {
            if (step.waitForSpecialTile || step.highlightTarget == SimpleTutAnchor.SpecialTileFirst)
            {
                ui.ConfigureOverlay(onClick: null, enableDim: step.dimBackground);
                ui.ShowHighlight(null, 0);
                watcherCo = StartCoroutine(WaitAndHighlightSpecialThenEnable(step));
            }
            else
            {
                ui.ShowHighlight(MapAnchor(step.highlightTarget), step.highlightPadding);
            }
        }
        else
        {
            ui.ShowHighlight(null, 0);
        }
    }
    public void EndSequence()
    {
        if (watcherCo != null) { StopCoroutine(watcherCo); watcherCo = null; }
        ExitPreviousStepIfAny();
        running = false;
        ui.HideAll();
        if (sequence && sequence.runOnFirstLaunch)
            PlayerPrefs.SetInt(sequence.seenPlayerPrefKey, 1);
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

    // เรียกจากปุ่มเพื่อรีเซ็ตสถานะว่าเคยดู แล้วเริ่มทิวทอเรียลใหม่ทันที
    public void ResetTutorialAndRestart()
    {
        if (sequence == null) return;
        PlayerPrefs.DeleteKey(sequence.seenPlayerPrefKey); // ลบ flag ว่าเคยดู
        PlayerPrefs.Save();
        ui.HideAll();
        running = false;
        StartSequence();
    }

    // ถ้าอยากแค่ล้างสถานะ แต่ยังไม่เริ่ม ให้เรียกอันนี้แทน
    public void ResetTutorialFlagOnly()
    {
        if (sequence == null) return;
        PlayerPrefs.DeleteKey(sequence.seenPlayerPrefKey);
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
                ui.ShowHighlight(targetRt, step.highlightPadding);
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
