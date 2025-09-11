using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[DisallowMultipleComponent]
public class UICardSelect : MonoBehaviour
{
    [Header("Root Panel (Cardpanel)")]
    [SerializeField] GameObject panel;          // สามารถ inactive ไว้ตั้งแต่เริ่มได้
    [SerializeField] CanvasGroup panelGroup;    // ถ้าไม่มีจะสร้างให้เองตอนเปิด

    [Header("Behaviour")]
    [SerializeField] bool autoHideOnStart = true;

    [Header("Card Buttons (3)")]
    [SerializeField] List<Button> cardButtons;

    [Header("Card Graphics on Buttons (Image)")]
    [SerializeField] List<Image> cardImages;

    [Header("Card Animators (3)")]
    [SerializeField] List<Animator> cardAnimators; // Idle_Wave / Hover(bool) / Click(trigger) / Hide(trigger)

    [Header("Text (Optional)")]
    [SerializeField] List<TMP_Text> cardNames;

    [Header("Targets (Card Slots)")]
    [SerializeField] List<RectTransform> slotTargets; // อนุญาตให้เป็น inactive ได้

    [Header("Timings")]
    [SerializeField] float hoverScale = 1.1f;
    [SerializeField] float hoverLerp = 12f;
    [SerializeField] float clickBounceWait = 0.12f;
    [SerializeField] float flyToSlotDur = 0.55f;
    [SerializeField] float hideOthersDur = 0.18f;
    [SerializeField] float landBounceScale = 1.06f;
    [SerializeField] float landBounceDur = 0.12f;

    [Header("Limits")]
    [SerializeField, Min(1)] int defaultMaxHeldSlots = 2;

    // ========= Runtime =========
    Action<CardData> onPicked;
    List<CardData> currentOptions;
    bool isAnimating;
    bool waitingReplace;

    Image activeClone;
    RectTransform activeCloneRect;

    Coroutine[] hoverScaleCo;   // ต่อปุ่ม
    static readonly int IdleHash = Animator.StringToHash("Idle_Wave");
    // เก็บ CanvasGroup ที่เราเปิดแบบโปร่งระหว่างบิน เพื่อคืนค่าทีหลัง
    struct SavedCG
    {
        public CanvasGroup cg;
        public float alpha;
        public bool interact, blocks;
    }
    readonly List<SavedCG> _revealQueue = new List<SavedCG>();

    // ---------- Public states ----------
    public bool IsOpen => panel && panel.activeSelf && panelGroup && panelGroup.alpha > 0.99f;
    public bool IsWaitingReplace => waitingReplace;
    public bool HasActiveClone => activeClone != null;
    public bool IsAnimating => isAnimating;

    // ---------- Lifecycle ----------
    void Awake()
    {
        // ไม่ไปยุ่งกับ active/inactive ของ panel
        if (!panel) panel = gameObject;
        if (!panelGroup && panel) panelGroup = panel.GetComponent<CanvasGroup>();
    }
    void Start()
    {
        // ถ้าเปิดเกมมาแล้ว panel ยัง Active อยู่ ให้ซ่อนทันที
        if (autoHideOnStart)
            HidePanelImmediate();
    }

    // ---------- API ----------
    public void Open(List<CardData> options, Action<CardData> onPickedCallback)
    {
        // เปิด Cardpanel (แม้เดิมจะ inactive)
        EnsurePanelCreatedAndShown();

        // เตรียมข้อมูล
        currentOptions = options ?? new List<CardData>();
        onPicked = onPickedCallback;
        isAnimating = false;
        waitingReplace = false;
        ClearActiveClone();

        EnsureSlotTargets();
        if (hoverScaleCo == null || hoverScaleCo.Length != cardButtons.Count)
            hoverScaleCo = new Coroutine[cardButtons.Count];

        // เติม UI ปุ่ม
        for (int i = 0; i < cardButtons.Count; i++)
        {
            var btn = cardButtons[i];
            bool active = (i < currentOptions.Count && currentOptions[i] != null);
            if (!btn) continue;

            btn.gameObject.SetActive(active);
            btn.onClick.RemoveAllListeners();
            if (!active) continue;

            var data = currentOptions[i];

            if (cardNames != null && i < cardNames.Count && cardNames[i])
                cardNames[i].text = data.displayName;

            if (cardImages != null && i < cardImages.Count && cardImages[i])
            {
                var img = cardImages[i];
                img.sprite = data.icon;
                img.enabled = true;
                img.canvasRenderer.SetAlpha(1f);
            }

            ResetCardVisual(btn, i);

            int idx = i;
            btn.onClick.AddListener(() => OnCardClicked(idx));
            EnsureEventTrigger(btn.gameObject, idx);
        }
    }

    public void OnCardClicked(int index)
    {
        if (isAnimating) return;
        CoroutineRunner.Instance.StartCoroutine(SelectFlow_DirectToSlot(index));
    }

    public IEnumerator AnimatePendingToSlot(int targetSlotIndex)
    {
        if (!waitingReplace || activeCloneRect == null) yield break;

        var canvasRT = RootCanvasRect();
        RectTransform target = FindSlotByIndex(targetSlotIndex);
        if (!target) { ForceCloseAndCleanup(); yield break; }

        EnsureSlotIsLandable(target);

        Vector2 endPos = WorldToLocalOn(canvasRT, target.position);
        Vector2 endSize = SizeOn(target, canvasRT);

        yield return LerpRect(activeCloneRect,
            activeCloneRect.anchoredPosition, endPos,
            activeCloneRect.sizeDelta, endSize,
            flyToSlotDur);

        yield return PulseScale(activeCloneRect, landBounceScale, landBounceDur);
        RevealActivated();

        yield return new WaitForEndOfFrame();

        ForceCloseAndCleanup(); // ปิดและเคลียร์ทุกอย่าง
    }

    public void OnReplaceCanceled()
    {
        ClearActiveClone();
        waitingReplace = false;
        ShowPanel(); // ให้กลับไปเลือกใหม่
    }

    // ---------- Core Flow ----------
    IEnumerator SelectFlow_DirectToSlot(int pickedIdx)
    {
        if (isAnimating) yield break;
        isAnimating = true;

        if (pickedIdx < 0 || pickedIdx >= cardButtons.Count) { isAnimating = false; yield break; }
        var pickedBtn = cardButtons[pickedIdx];
        var pickedImg = (cardImages != null && pickedIdx < cardImages.Count) ? cardImages[pickedIdx] : null;
        var pickedAnim = (cardAnimators != null && pickedIdx < cardAnimators.Count) ? cardAnimators[pickedIdx] : null;
        var dataSel = (currentOptions != null && pickedIdx < currentOptions.Count) ? currentOptions[pickedIdx] : null;
        if (!pickedBtn || !pickedImg || dataSel == null) { FinishPick(null); yield break; }

        // ถ้าถือการ์ดเต็มแล้ว → เข้าสู่โหมดแทนที่ทันที
        if (!HasAnyFreeSlot())
        {
            CreateCloneAtImage(pickedImg);
            HidePanel();
            waitingReplace = true;
            onPicked?.Invoke(dataSel);   // ให้ CardManager เข้าสู่โหมด Replace
            isAnimating = false;
            yield break;
        }

        // หยุดโฮเวอร์ + เด้งคลิก
        if (cardAnimators != null)
            for (int i = 0; i < cardAnimators.Count; i++)
                if (cardAnimators[i]) cardAnimators[i].SetBool("Hover", false);
        if (pickedAnim) { pickedAnim.ResetTrigger("Click"); pickedAnim.SetTrigger("Click"); }
        yield return new WaitForSecondsRealtime(clickBounceWait);

        // ซ่อนใบอื่น
        if (cardAnimators != null)
            for (int i = 0; i < cardAnimators.Count; i++)
                if (i != pickedIdx && cardAnimators[i]) { cardAnimators[i].ResetTrigger("Hide"); cardAnimators[i].SetTrigger("Hide"); }
        yield return new WaitForSecondsRealtime(hideOthersDur);

        // หาเป้าหมาย (รองรับ slot ที่ inactive)
        RectTransform targetSlot = PickFirstEmptySlot();
        if (targetSlot == null)
        {
            // กันหลุด: ถ้าหาไม่ได้ ให้เข้า Replace
            CreateCloneAtImage(pickedImg);
            HidePanel();
            waitingReplace = true;
            onPicked?.Invoke(dataSel);
            isAnimating = false;
            yield break;
        }

        // เตรียมบิน
        var canvasRT = RootCanvasRect();
        CreateCloneAtImage(pickedImg);
        HidePanel(); // ปิด popup (คราวนี้ปิดเป็น inactive ก็ได้ เพราะคอร์รูลีนรันบน Runner)

        EnsureSlotIsLandable(targetSlot);
        Vector2 endPos = WorldToLocalOn(canvasRT, targetSlot.position);
        Vector2 endSize = SizeOn(targetSlot, canvasRT);

        yield return LerpRect(activeCloneRect,
            activeCloneRect.anchoredPosition, endPos,
            activeCloneRect.sizeDelta, endSize,
            flyToSlotDur);

        yield return PulseScale(activeCloneRect, landBounceScale, landBounceDur);
        RevealActivated();
        // ⭐ เพิ่มบรรทัดนี้ เพื่อให้ Destroy()/การซ่อนมีผลก่อน
        yield return new WaitForEndOfFrame();

        FinishPick(dataSel);
    }

    // ---------- Helpers (Flow) ----------
    void FinishPick(CardData picked)
    {
        ClearActiveClone();
        waitingReplace = false;
        HidePanel();

        isAnimating = false;           // ⭐ ย้ายขึ้นมาก่อน callback
        onPicked?.Invoke(picked);      // CardManager จะ add ใส่มือ/อัปเดต UI ให้เอง
    }

    void ForceCloseAndCleanup()
    {
        ClearActiveClone();
        waitingReplace = false;
        HidePanel();
    }

    void CreateCloneAtImage(Image pickedImg)
    {
        var canvasRT = RootCanvasRect();
        activeClone = CreateImageCloneOnCanvas(pickedImg);
        activeCloneRect = activeClone.rectTransform;
        pickedImg.canvasRenderer.SetAlpha(0f);

        var fromRT = (RectTransform)pickedImg.transform;
        Vector2 startPos = WorldToLocalOn(canvasRT, fromRT.position);
        Vector2 startSize = SizeOn(fromRT, canvasRT);

        activeCloneRect.anchoredPosition = startPos;
        activeCloneRect.sizeDelta = startSize;
    }

    void ClearActiveClone()
    {
        if (activeClone)
        {
            // ซ่อนทันทีในเฟรมนี้ก่อน
            activeClone.gameObject.SetActive(false);
            var r = activeClone.GetComponent<CanvasRenderer>();
            if (r != null) r.SetAlpha(0f);

            // ค่อยทำลายท้ายเฟรม (ไม่กระพริบ)
            Destroy(activeClone.gameObject);
        }
        activeClone = null;
        activeCloneRect = null;
    }

    // ---------- UI visual ----------
    void ResetCardVisual(Button btn, int idx)
    {
        if (!btn) return;

        btn.transform.localScale = Vector3.one;

        if (cardImages != null && idx < cardImages.Count && cardImages[idx])
            cardImages[idx].canvasRenderer.SetAlpha(1f);

        var cg = btn.GetComponent<CanvasGroup>();
        if (cg) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }

        if (cardAnimators != null && idx < cardAnimators.Count)
        {
            var a = cardAnimators[idx];
            if (a)
            {
                a.keepAnimatorControllerStateOnDisable = false;
                a.ResetTrigger("Hide");
                a.ResetTrigger("Click");
                a.SetBool("Hover", false);
                a.Rebind(); a.Update(0f);
                a.Play(IdleHash, 0, UnityEngine.Random.value);
                a.speed = 1f + UnityEngine.Random.Range(-0.06f, 0.08f);
            }
        }
    }

    void EnsureEventTrigger(GameObject go, int index)
    {
        var et = go.GetComponent<EventTrigger>();
        if (!et) et = go.AddComponent<EventTrigger>();
        et.triggers ??= new List<EventTrigger.Entry>();
        et.triggers.Clear();

        var eIn = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        eIn.callback.AddListener(_ => OnHoverEnter(index));
        et.triggers.Add(eIn);

        var eOut = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        eOut.callback.AddListener(_ => OnHoverExit(index));
        et.triggers.Add(eOut);
    }

    void OnHoverEnter(int i)
    {
        if (isAnimating || waitingReplace) return;

        if (cardAnimators != null)
            for (int k = 0; k < cardAnimators.Count; k++)
                if (cardAnimators[k]) cardAnimators[k].SetBool("Hover", k == i);

        SmoothScaleTo(cardButtons[i].transform as RectTransform, hoverScale, i);

        for (int k = 0; k < cardButtons.Count; k++)
            if (k != i) SmoothScaleTo(cardButtons[k].transform as RectTransform, 1f, k);
    }

    void OnHoverExit(int i)
    {
        if (isAnimating || waitingReplace) return;
        if (cardAnimators != null && i < cardAnimators.Count && cardAnimators[i])
            cardAnimators[i].SetBool("Hover", false);
        SmoothScaleTo(cardButtons[i].transform as RectTransform, 1f, i);
    }

    void SmoothScaleTo(RectTransform rt, float to, int idx)
    {
        if (!rt) return;

        if (hoverScaleCo == null || hoverScaleCo.Length != cardButtons.Count)
            hoverScaleCo = new Coroutine[cardButtons.Count];

        if (hoverScaleCo[idx] != null)
            CoroutineRunner.Instance.StopCoroutine(hoverScaleCo[idx]);

        hoverScaleCo[idx] = CoroutineRunner.Instance.StartCoroutine(HoverScaleCo(rt, to));
    }

    IEnumerator HoverScaleCo(RectTransform rt, float to)
    {
        Vector3 target = Vector3.one * to;
        while ((rt.localScale - target).sqrMagnitude > 0.0001f)
        {
            rt.localScale = Vector3.Lerp(rt.localScale, target, Time.unscaledDeltaTime * hoverLerp);
            yield return null;
        }
        rt.localScale = target;
    }

    IEnumerator LerpRect(RectTransform r, Vector2 fromPos, Vector2 toPos, Vector2 fromSize, Vector2 toSize, float dur)
    {
        if (!r) yield break;
        if (dur <= 0f) dur = 0.0001f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // easeOutCubic
            r.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, e);
            r.sizeDelta = Vector2.LerpUnclamped(fromSize, toSize, e);
            yield return null;
        }
        r.anchoredPosition = toPos;
        r.sizeDelta = toSize;
    }

    IEnumerator PulseScale(RectTransform rt, float peak, float dur)
    {
        if (!rt) yield break;
        Vector3 s0 = Vector3.one;
        Vector3 s1 = Vector3.one * Mathf.Max(1f, peak);

        float half = Mathf.Max(0.01f, dur * 0.5f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / half;
            float e = 1f - Mathf.Pow(1f - t, 3f);
            rt.localScale = Vector3.LerpUnclamped(s0, s1, e);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / half;
            float e = 1f - Mathf.Pow(1f - t, 3f);
            rt.localScale = Vector3.LerpUnclamped(s1, s0, e);
            yield return null;
        }
        rt.localScale = s0;
    }

    // ---------- Canvas / Coordinate ----------
    Canvas _rootCanvas;
    RectTransform RootCanvasRect()
    {
        if (!_rootCanvas) _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        return _rootCanvas ? _rootCanvas.GetComponent<RectTransform>() : (RectTransform)transform;
    }
    Camera CanvasCam()
    {
        if (_rootCanvas && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return _rootCanvas.worldCamera != null ? _rootCanvas.worldCamera : Camera.main;
        return null;
    }
    Vector2 WorldToLocalOn(RectTransform targetRect, Vector3 worldPos)
    {
        if (!targetRect) return Vector2.zero;
        Camera cam = CanvasCam();
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRect,
            RectTransformUtility.WorldToScreenPoint(cam, worldPos),
            cam,
            out local
        );
        return local;
    }
    Vector2 SizeOn(RectTransform src, RectTransform dst)
    {
        if (!src || !dst) return Vector2.zero;
        Vector3[] c = new Vector3[4];
        src.GetWorldCorners(c);
        Vector2 bl = WorldToLocalOn(dst, c[0]);
        Vector2 tr = WorldToLocalOn(dst, c[2]);
        return tr - bl;
    }

    Image CreateImageCloneOnCanvas(Image src)
    {
        var parent = RootCanvasRect();
        var go = new GameObject("CardClone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var img = go.GetComponent<Image>();
        img.sprite = src.sprite;
        img.preserveAspect = src.preserveAspect;
        img.raycastTarget = false;

        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.pivot = ((RectTransform)src.transform).pivot;
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
        return img;
    }

    // ---------- Slots ----------
    void EnsureSlotTargets()
    {
        if (slotTargets != null && slotTargets.Count > 0) return;

        var slots = FindObjectsOfType<CardSlotUI>(true); // รวม inactive
        slotTargets = slots
            .OrderBy(s => s.slotIndex)
            .Select(s => s.transform as RectTransform)
            .Where(rt => rt != null)
            .ToList();
    }

    int AllowedSlots()
    {
        if (CardManager.Instance != null)
            return Mathf.Max(1, CardManager.Instance.maxHeldCards);
        return Mathf.Max(1, defaultMaxHeldSlots);
    }

    bool HasAnyFreeSlot()
    {
        EnsureSlotTargets();
        int allowed = AllowedSlots();

        foreach (var rt in slotTargets)
        {
            if (!rt) continue;
            var slot = rt.GetComponent<CardSlotUI>();
            if (!slot || slot.slotIndex >= allowed) continue;
            if (slot.cardInSlot == null) return true;
        }
        return false;
    }

    RectTransform PickFirstEmptySlot()
    {
        EnsureSlotTargets();
        int allowed = AllowedSlots();
        RectTransform firstDisabledEmpty = null;

        foreach (var rt in slotTargets)
        {
            if (!rt) continue;
            var slot = rt.GetComponent<CardSlotUI>();
            if (!slot || slot.slotIndex >= allowed) continue;

            bool isEmpty = (slot.cardInSlot == null);
            bool isActive = rt.gameObject.activeInHierarchy;
            bool lockedByCG = HasLockedCanvasGroup(rt.transform);

            if (isEmpty && isActive && !lockedByCG)
                return rt;

            if (isEmpty && firstDisabledEmpty == null)
                firstDisabledEmpty = rt;
        }
        return firstDisabledEmpty; // อนุญาตคืนช่องที่ inactive แล้วค่อยเปิดใน EnsureSlotIsLandable
    }

    RectTransform FindSlotByIndex(int idx)
    {
        EnsureSlotTargets();
        foreach (var rt in slotTargets)
        {
            if (!rt) continue;
            var s = rt.GetComponent<CardSlotUI>();
            if (s != null && s.slotIndex == idx) return rt;
        }
        if (idx >= 0 && idx < slotTargets.Count) return slotTargets[idx];
        return null;
    }

    bool HasLockedCanvasGroup(Transform t)
    {
        while (t != null)
        {
            var cg = t.GetComponent<CanvasGroup>();
            if (cg && (!cg.interactable || !cg.blocksRaycasts || cg.alpha <= 0.001f))
                return true;
            t = t.parent;
        }
        return false;
    }

    void EnsureSlotIsLandable(RectTransform target)
    {
        if (!target) return;
        var t = target.transform;
        while (t != null)
        {
            ActivateInvisible(t);      // ← เปิดแต่โปร่ง ไม่เห็น/ไม่รับคลิก
            if (t.GetComponent<Canvas>()) break;
            t = t.parent;
        }
    }

    // ---------- Panel show/hide ----------
    void EnsurePanelCreatedAndShown()
    {
        if (!panel) panel = gameObject;
        if (!panelGroup) panelGroup = panel.GetComponent<CanvasGroup>();
        if (!panelGroup) panelGroup = panel.AddComponent<CanvasGroup>();
        ShowPanel();
    }

    void ShowPanel()
    {
        if (!panel) return;
        if (!panel.activeSelf) panel.SetActive(true);
        if (!panelGroup) panelGroup = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
        panelGroup.alpha = 1f;
        panelGroup.blocksRaycasts = true;
        panelGroup.interactable = true;
    }

    void HidePanel()
    {
        if (!panel) return;
        if (!panelGroup) panelGroup = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;
        panel.SetActive(false); // ปิดจริงๆ ได้ เพราะคอร์รูลีนไม่ได้ผูกกับเรา
    }
    void HidePanelImmediate()
    {
        if (!panel) panel = gameObject;
        if (!panelGroup) panelGroup = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();

        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

        // ปิดจริงๆ เพื่อกันเผลอคลิกได้ และกันเห็นในเฟรมแรก
        if (panel.activeSelf) panel.SetActive(false);
    }
    void ActivateInvisible(Transform t) {
        // เปิด GO ถ้ายังปิดอยู่ แล้วทำให้โปร่ง/ไม่รับคลิก
        if (!t.gameObject.activeSelf) {
            t.gameObject.SetActive(true);
            var cg = t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>();
            _revealQueue.Add(new SavedCG { cg=cg, alpha=cg.alpha, interact=cg.interactable, blocks=cg.blocksRaycasts });
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }

    void RevealActivated() {
        for (int i = 0; i < _revealQueue.Count; i++) {
            var s = _revealQueue[i];
            if (!s.cg) continue;
            // ถ้าของเดิมไม่มี alpha ก็เผยเป็น 1 ไปเลย
            s.cg.alpha = (s.alpha <= 0f ? 1f : s.alpha);
            s.cg.interactable   = true;
            s.cg.blocksRaycasts = true;
        }
        _revealQueue.Clear();
    }
}
