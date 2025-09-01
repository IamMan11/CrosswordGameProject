using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class LetterTile : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private LetterData data;
    [SerializeField] private bool isBlankTile = false;  // เผื่อกำหนดจาก Inspector ได้ (ไม่จำเป็นก็ได้)
    private string overrideLetter = null;               // ตัวอักษรที่ผู้เล่นเลือก (เฉพาะ Blank)
    private bool _fromBenchDrag = false;
    [Header("UI References")]
    public Image icon;
    public TMP_Text letterText;
    public TMP_Text scoreText;
    public Image specialMark;

    [Header("Visual Root (Animator อยู่ตรงนี้)")]
    public RectTransform visualPivot;

    [Header("Click Move FX")]
    public float flyDuration = 0.2f;
    public AnimationCurve flyEase = AnimationCurve.EaseInOut(0,0,1,1);

    // ==== Animator Helpers ====
    private Animator visualAnimator;
    // เปลี่ยนชื่อตามที่ตั้งใน Animator Controller ของคุณ
    [SerializeField] private string STATE_IDLE   = "Idle";
    [SerializeField] private string STATE_DRAG   = "Draggloop";
    [SerializeField] private string STATE_SETTLE = "Settle";
    // กันสั่ง Settle รัว ๆ
    [SerializeField] private float settleDuration = 0.22f;   // ให้เท่ากับความยาวคลิป Settle จริง
    [SerializeField] private float settleDebounce = 0.10f;   // ช่วงกันสั่งซ้ำ
    // === Drag snapshot ===
    Transform dragPrevParent;
    int      dragPrevSibling;
    Vector2  dragPrevAnchorMin, dragPrevAnchorMax, dragPrevPivot, dragPrevSizeDelta;
    Vector3  dragPrevScale;
    bool     dragging;
    // ใกล้ๆ ฟิลด์ของคลาส
    [HideInInspector] public bool IsInSpace = false;
    private bool wasInSpaceAtDragStart = false;

    RectTransform CanvasRect => canvas.rootCanvas.transform as RectTransform;
    Camera UICam => canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null
                : canvas.rootCanvas.worldCamera;

    Vector2 ScreenToCanvasLocal(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, screenPos, UICam, out var lp);
        return lp;
    }

    private bool animLock = false;
    private float lastSettleTime = -999f;
    private Coroutine settleLockCo;

    private static readonly int HASH_DRAGGING = Animator.StringToHash("Dragging");
    private Canvas canvas;           // หา Canvas หลัก (สำหรับคำนวณตำแหน่ง)
    private CanvasGroup canvasGroup; // ใช้ปิด Raycast ระหว่างลาก
    private RectTransform rectTf;
    private bool isSpecialTile;
    public bool isLocked = false;

    private bool isBusy = false; // กันคลิก/ลากซ้อนตอนกำลังบิน

    [HideInInspector]
    public Transform OriginalParent; // ให้ BenchSlot.cs เข้าถึงตอนสลับ

    private void Awake()
    {
        rectTf = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // เดิม: if (visualPivot != null) visualAnimator = visualPivot.GetComponent<Animator>();
        if (visualPivot != null)
            visualAnimator = visualPivot.GetComponent<Animator>();
        else
            visualAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
    }
    // เดิมใช้แค่ parent → บางกรณีคืนค่าเพี้ยน
    private bool IsOnBoard()
    {
        // ถ้ากำลังบินอยู่ parent จะเป็น Canvas ให้ถือว่า "ไม่อยู่บนบอร์ด"
        if (transform.parent == canvas.transform) return false;

        // ดูทั้ง ancestor เพื่อรองรับกรณีมีคอนเทนเนอร์ขั้นกลาง
        return GetComponentInParent<BoardSlot>() != null;
    }


    // ================= Drag =================
    public void OnBeginDrag(PointerEventData e)
    {
        // ห้ามลากบนบอร์ด แต่ Bench/Space ให้ลากได้
        if (IsOnBoard()) { dragging = false; return; }     // << กันตั้งแต่เริ่ม
        if (isLocked || isBusy || UiGuard.IsBusy) { dragging = false; return; }
        if (IsBlank && !IsBlankResolved)
        {
            BlankPopup.Show(ch => ResolveBlank(ch));
            return; // ยังไม่ให้ลากจนกว่าจะเลือกตัวอักษร
        }

        dragging = true;                                    // << เริ่มลากจริง

        OriginalParent = transform.parent;

        // แจ้งให้ manager รู้ว่าลากจากที่ไหน (เพื่อให้ช่องเลื่อน)
        int benchIdx = BenchManager.Instance ? BenchManager.Instance.IndexOfSlot(OriginalParent) : -1;
        int spaceIdx = SpaceManager.Instance ? SpaceManager.Instance.IndexOfSlot(OriginalParent) : -1;
        if (benchIdx >= 0) { _fromBenchDrag = true; BenchManager.Instance.BeginDrag(this, benchIdx); }
        else if (spaceIdx >= 0) { _fromBenchDrag = false; SpaceManager.Instance.BeginDrag(this, spaceIdx); }

        wasInSpaceAtDragStart = IsInSpace;

        // ย้ายไปอยู่ใต้ Canvas ชั่วคราว + ลอยตามเมาส์
        transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;                 // ให้ช่องปลายทางรับ Drop/Enter ได้
        SetDragging(true);                                  // เล่นอนิเมชัน “กำลังลาก” (ถ้ามี)
    }

    public void OnDrag(PointerEventData e)
    {
        // *** สำคัญ: ถ้าไม่ใช่การลากจริง (หรือยังอยู่ใต้ slot เดิม) ห้ามขยับ ***
        if (!dragging) return;
        if (isLocked || isBusy) return;
        if (transform.parent != canvas.transform) return;   // ยังไม่ได้ย้ายมา Canvas = ไม่ใช่ state ลาก

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, e.position, e.pressEventCamera, out var pos);
        rectTf.localPosition = pos;                         // ลอยตามเมาส์
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging) return;          // ถ้าไม่ได้ลากจริง ไม่ต้องทำอะไร
        dragging = false;

        canvasGroup.blocksRaycasts = true;

        // ถ้าโดนช่องรับวาง OnDrop จะ reparent ให้แล้ว → parent จะไม่ใช่ canvas อีกต่อไป
        bool placed = transform.parent != canvas.transform;

        if (!placed)
        {
            // ไม่โดนอะไรเลย → กลับช่องว่างของ manager ต้นทาง (หรือที่เดิม) แบบ "บินกลับ"
            Transform target = OriginalParent;

            if (_fromBenchDrag && BenchManager.Instance)
            {
                var slot = BenchManager.Instance.GetCurrentEmptySlot();
                target = (slot ? slot.transform : OriginalParent);
            }
            else if (!_fromBenchDrag && SpaceManager.Instance)
            {
                var slot = SpaceManager.Instance.GetCurrentEmptySlot();
                target = (slot ? slot.transform : OriginalParent);
            }

            FlyTo(target);                                   // บินกลับ (ค่อย ๆ ปรับขนาด)
            IsInSpace = (target.GetComponent<BoardSlot>() == null);

            SfxPlayer.Play(SfxId.TileDrop);                 // เสียงวาง/คืนช่อง
            PlaySettle();
        }
        else
        {
            // โดน OnDrop แล้ว → ดูว่าลงบอร์ดหรือช่อง (Bench/Space) เพื่ออัปเดตสถานะ/เสียง
            var board = GetComponentInParent<BoardSlot>();
            var space = GetComponentInParent<SpaceSlot>();

            IsInSpace = true; // หรือ false ตามโค้ดที่คุณมีอยู่แล้ว
            if (IsInSpace != wasInSpaceAtDragStart)
                SfxPlayer.Play(SfxId.TileTransfer);   // ✅ ย้ายข้ามฝั่ง Bench <-> Space
            else
                SfxPlayer.Play(SfxId.TileDrop);       // วางในฝั่งเดิม/สลับช่องภายในฝั่งเดียวกัน

            PlaySettle();
        }

        SetDragging(false);

        // เคลียร์สถานะ drag ให้ manager (เพื่อเลื่อนช่องรอบต่อไปได้)
        if (_fromBenchDrag) BenchManager.Instance?.EndDrag(placed);
        else                SpaceManager.Instance?.EndDrag(placed);
    }
    private void OnTransformParentChanged()
    {
        SpaceManager.Instance?.UpdateDiscardButton();
    }
    private void SnapTo(Transform parent)
    {
        transform.SetParent(parent, false);
        transform.SetAsLastSibling();
        transform.localPosition = Vector3.zero;
        AdjustSizeToParent();
    }

    private void SetDragging(bool v)
    {
        if (!visualAnimator) return;

        if (v)
        {
            // ถ้ากำลังเล่น Settle อยู่ ให้ "สั่งทับ" ไป Drag โดยตรง (ไม่ปล่อยให้ทับกัน)
            var st = visualAnimator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(STATE_SETTLE))
                visualAnimator.CrossFadeInFixedTime(STATE_DRAG, 0.02f, 0, 0f);

            visualAnimator.SetBool(HASH_DRAGGING, true);
        }
        else
        {
            visualAnimator.SetBool(HASH_DRAGGING, false);
            // ไม่ต้องบังคับกลับ Idle ทันที ปล่อยให้ทรานซิชันเอง
        }
    }

    public void PlaySettle()
    {
        if (!visualAnimator) return;

        // Debounce: ถ้าพึ่งสั่งไปไม่นาน ให้เพิกเฉย
        if (Time.unscaledTime - lastSettleTime < settleDebounce) return;
        lastSettleTime = Time.unscaledTime;

        // ปิดสถานะ Drag ให้ชัดเจนก่อน
        visualAnimator.SetBool(HASH_DRAGGING, false);

        // "สั่งทับ" ไปยังคลิป Settle ทันที (ไม่ใช้ Trigger เพื่อตัดแถวชัดเจน)
        visualAnimator.CrossFadeInFixedTime(STATE_SETTLE, 0.02f, 0, 0f);

        // ล็อกช่วงสั้นๆ ไม่ให้มีคำสั่งอนิเมชันอื่นมาชน (กันทับ)
        if (settleLockCo != null) StopCoroutine(settleLockCo);
        settleLockCo = StartCoroutine(SettleLockFor(settleDuration));
    }

    private System.Collections.IEnumerator SettleLockFor(float dur)
    {
        animLock = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, dur));
        animLock = false;
    }

    // ================= Click to Move =================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked || isBusy || UiGuard.IsBusy) return;
        // คลิกซ้ายเท่านั้น (กัน double tap ขวา)
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (IsBlank && !IsBlankResolved)
        {
            BlankPopup.Show(ch => { ResolveBlank(ch); /* ถ้าจะให้ย้ายต่อก็คลิก/ลากซ้ำ */ });
            return;
        }
        if (IsOnBoard())
        {
            var target = SpaceManager.Instance?.GetFirstEmptySlot();
            if (target == null) return;

            SfxPlayer.Play(SfxId.TileTransfer);   // ★ เสียงย้าย Board→Space (คลิก)
            StartCoroutine(FlyToSlot(target.transform));
            SpaceManager.Instance?.RefreshDiscardButton();
            return;
        }

        Transform curParent = transform.parent;

        int benchIdx = BenchManager.Instance ? BenchManager.Instance.IndexOfSlot(curParent) : -1;
        int spaceIdx = SpaceManager.Instance ? SpaceManager.Instance.IndexOfSlot(curParent) : -1;

        // จาก Bench → ไป Space
        if (benchIdx >= 0 && SpaceManager.Instance != null)
        {
            var target = SpaceManager.Instance.GetFirstEmptySlot();       // SpaceSlot
            if (target == null) return;

            // รูดปิดช่องฝั่ง Bench ก่อน
            BenchManager.Instance.CollapseFrom(benchIdx);
            SfxPlayer.Play(SfxId.TileTransfer);   // ★ เสียงย้าย Bench→Space (คลิก)
            StartCoroutine(FlyToSlot(target.transform));
            return;
        }

        // จาก Space → ไป Bench
        if (spaceIdx >= 0 && BenchManager.Instance != null)
        {
            var target = BenchManager.Instance.GetFirstEmptySlot();       // BenchSlot
            if (target == null) return;

            SpaceManager.Instance.CollapseFrom(spaceIdx);
            SfxPlayer.Play(SfxId.TileTransfer);   // ★ เสียงย้าย Space→Bench (คลิก)
            StartCoroutine(FlyToSlot(target.transform));
        }
    }
    public void ResolveBlank(char ch)
    {
        overrideLetter = char.ToUpperInvariant(ch).ToString();

        // อัปเดตตัวหนังสือบนไทล์ (เผื่อคุณแสดงตัวหนังสือทับ)
        if (letterText) letterText.text = overrideLetter;

        // คะแนนของ Blank = 0 เสมอ
        if (scoreText) scoreText.text = "0";

        // >>> สำคัญ: เปลี่ยน "ภาพ" ให้เป็นตัวอักษรที่เลือก
        var bag = TileBag.Instance;
        if (bag != null)
        {
            var lc = bag.initialLetters.Find(l =>
                l != null && l.data != null &&
                string.Equals(l.data.letter, overrideLetter, System.StringComparison.OrdinalIgnoreCase));

            if (lc != null && lc.data != null && lc.data.sprite != null && icon != null)
            {
                icon.sprite = lc.data.sprite;
                // ถ้ารูปเพี้ยนสัดส่วน ลองเปิดบรรทัดนี้
                // icon.SetNativeSize();
            }
        }
    }
    public bool IsBlank => isBlankTile
    || string.Equals(data.letter, "BLANK", System.StringComparison.OrdinalIgnoreCase)
    || data.letter == "?" || data.letter == "_";

    public bool IsBlankResolved => !IsBlank || !string.IsNullOrEmpty(overrideLetter);

    // ใช้ค่านี้แทนการอ่าน data.letter ตรง ๆ
    public string CurrentLetter => string.IsNullOrEmpty(overrideLetter) ? data.letter : overrideLetter;
    public void FlyTo(Transform targetSlot)
    {
        // ใช้คอร์รุตีนบินที่มีอยู่แล้ว
        StartCoroutine(FlyToSlot(targetSlot));
    }
    private IEnumerator FlyToSlot(Transform targetSlot)
    {
        isBusy = true;
        UiGuard.Push();
        canvasGroup.blocksRaycasts = false;

        // บินข้ามเลเยอร์: เอาออกมาอยู่ใต้ Canvas ชั่วคราว
        transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();

        // จุดเริ่ม-ปลาย (world)
        Vector3 startPos = rectTf.position;
        Vector3 endPos   = (targetSlot as RectTransform).TransformPoint(Vector3.zero);

        // คำนวณ "ขนาดโลก" เพื่อให้สเกลปลายทางเท่าช่อง
        Vector2 GetWorldSize(RectTransform rt) {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return new Vector2(Vector3.Distance(c[0], c[3]), Vector3.Distance(c[0], c[1]));
        }
        Vector2 startWS  = GetWorldSize(rectTf);
        Vector2 targetWS = GetWorldSize(targetSlot as RectTransform);

        Transform scaleTarget = transform;
        Vector3 startScale = scaleTarget.localScale;
        Vector3 endScale   = startScale;
        if (startWS.x > 1e-3f && startWS.y > 1e-3f) {
            float sx = targetWS.x / startWS.x, sy = targetWS.y / startWS.y;
            endScale = new Vector3(startScale.x * sx, startScale.y * sy, startScale.z);
        }

        float t = 0f, dur = Mathf.Max(0.0001f, flyDuration);
        try {
            while (t < 1f) {
                t += Time.unscaledDeltaTime / dur;
                float a = flyEase.Evaluate(Mathf.Clamp01(t));
                rectTf.position        = Vector3.LerpUnclamped(startPos, endPos, a);
                scaleTarget.localScale = Vector3.LerpUnclamped(startScale, endScale, a);
                yield return null;
            }

            // เคลียร์ของค้างในช่องปลายทาง (มี helper ใน Bench/Space อยู่แล้ว)
            if (targetSlot.childCount > 0) {
                if (BenchManager.Instance && BenchManager.Instance.IndexOfSlot(targetSlot) >= 0)
                    BenchManager.Instance.KickOutExistingToNearestEmpty(targetSlot);
                else if (SpaceManager.Instance && SpaceManager.Instance.IndexOfSlot(targetSlot) >= 0)
                    SpaceManager.Instance.KickOutExistingToNearestEmpty(targetSlot);
            }

            // ลงจอดจริง + snap ให้พอดีช่อง + รีเซ็ตสเกลภายใน
            transform.SetParent(targetSlot, false);
            transform.SetAsLastSibling();
            transform.localPosition = Vector3.zero;
            scaleTarget.localScale = Vector3.one;
            AdjustSizeToParent();
            PlaySettle();
            SpaceManager.Instance?.UpdateDiscardButton();
        }
        finally {
            canvasGroup.blocksRaycasts = true;
            isBusy = false;
            UiGuard.Pop();
        }
    }



    // ช่วยเช็คว่ามีพารามิเตอร์ชื่อนี้อยู่จริงไหม
    
    static bool AnimatorHasParam(Animator anim, string name, AnimatorControllerParameterType type)
    {
        if (!anim) return false;
        foreach (var p in anim.parameters)
            if (p.type == type && p.name == name) return true;
        return false;
    }

    static readonly System.Collections.Generic.HashSet<Animator> _warned = new();

    // ดึง Transform เป้าหมายสำหรับเด้ง: ถ้าไม่ตั้ง visualPivot จะใช้ตัวเอง
    Transform GetBounceTarget()
    {
        return visualPivot ? visualPivot : transform;
    }

    Animator FindAnimatorForPulse()
    {
        // ลองที่เป้าหมายเด้งก่อน (รากกรณีคุณ)
        var target = GetBounceTarget();
        var anim = target.GetComponent<Animator>();
        if (!anim) anim = GetComponent<Animator>();
        if (!anim) anim = target.GetComponentInChildren<Animator>();
        return anim;
    }

    public void Pulse()
    {
        var anim = FindAnimatorForPulse();
        if (anim)
        {
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

            // รองรับชื่อ Trigger ยอดฮิต ถ้าไม่มีจะ fallback เป็นเด้งด้วยโค้ด
            if (AnimatorHasParam(anim, "Pulse", AnimatorControllerParameterType.Trigger))
            {
                anim.ResetTrigger("Pulse");
                anim.SetTrigger("Pulse");
                return;
            }
            if (AnimatorHasParam(anim, "Bounce", AnimatorControllerParameterType.Trigger))
            {
                anim.ResetTrigger("Bounce");
                anim.SetTrigger("Bounce");
                return;
            }
            if (AnimatorHasParam(anim, "BounceFast", AnimatorControllerParameterType.Trigger))
            {
                anim.ResetTrigger("BounceFast");
                anim.SetTrigger("BounceFast");
                return;
            }

            if (!_warned.Contains(anim))
            {
                _warned.Add(anim);
                Debug.LogWarning($"[LetterTile] Animator '{anim.runtimeAnimatorController?.name}' ไม่มี Trigger 'Pulse/Bounce/BounceFast' — ใช้โค้ดเด้งแทน");
            }
        }

        // ไม่มี/ไม่พบ Trigger → เด้งด้วยโค้ดแทน
        StartCoroutine(QuickBounceCo());
    }

    IEnumerator QuickBounceCo(float dur = 0.12f, float scale = 1.15f)
    {
        var target = GetBounceTarget();
        if (!target) yield break;

        Vector3 a = Vector3.one, b = Vector3.one * scale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, dur);
            float k = t < 0.5f ? (t / 0.5f) : (1f - (t - 0.5f) / 0.5f);
            target.localScale = Vector3.LerpUnclamped(a, b, k);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    // ====== Data & Utils (ของเดิม) ======
    public void Setup(LetterData d)
    {
        data = d;
        icon.sprite     = data.sprite;
        letterText.text = data.letter;
        scoreText.text  = data.score.ToString();
        isSpecialTile = data.isSpecial;
        isBlankTile = IsBlank;
        if (IsBlank)
        {
            // คะแนน Blank = 0 (แม้ใน data.score จะเป็นอะไรก็ตาม)
            if (scoreText) scoreText.text = "0";
        }
        overrideLetter = null; // เริ่มยังไม่เลือก
        specialMark.enabled = data.isSpecial;
    }
    public void SetSpecial(bool v)
    {
        isSpecialTile = v;
        if (data != null) data.isSpecial = v;
        if (specialMark != null) specialMark.enabled = v;
    }
    public LetterData GetData() => data;

    public void AdjustSizeToParent()
    {
        var rtTile = GetComponent<RectTransform>();
        var parentRt = transform.parent as RectTransform;
        if (parentRt == null) return;

        rtTile.anchorMin = Vector2.zero;
        rtTile.anchorMax = Vector2.one;
        rtTile.anchoredPosition = Vector2.zero;
        rtTile.offsetMin = Vector2.zero;
        rtTile.offsetMax = Vector2.zero;
        rtTile.localScale = Vector3.one;
        rtTile.localRotation = Quaternion.identity;
    }

    public void Lock() => isLocked = true;
    public bool IsSpecial => isSpecialTile;
}
