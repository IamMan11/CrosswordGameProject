using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class LetterTile : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
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

    // === NEW ===
    [HideInInspector] public bool IsInSpace = false;
    private void Awake()
    {
        rectTf = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (visualPivot != null) visualAnimator = visualPivot.GetComponent<Animator>();
    }
    private bool IsOnBoard()
    {
        var p = transform.parent;
        return p != null && p.GetComponent<BoardSlot>() != null;
    }

    // ================= Drag =================
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsOnBoard()) return;
        if (isLocked || isBusy || UiGuard.IsBusy) return;

        OriginalParent = transform.parent;

        int benchIdx = BenchManager.Instance ? BenchManager.Instance.IndexOfSlot(OriginalParent) : -1;
        int spaceIdx = SpaceManager.Instance ? SpaceManager.Instance.IndexOfSlot(OriginalParent) : -1;

        // ถ้าเริ่มจาก Bench/Space ให้แจ้ง Manager ที่ถูกต้อง
        if (benchIdx >= 0)
        {
            _fromBenchDrag = true;
            BenchManager.Instance.BeginDrag(this, benchIdx);
        }
        else if (spaceIdx >= 0)
        {
            _fromBenchDrag = false;
            SpaceManager.Instance.BeginDrag(this, spaceIdx);
        }

        transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
        SetDragging(true);
    }
    private void OnTransformParentChanged()
    {
        SpaceManager.Instance?.UpdateDiscardButton();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isLocked || isBusy) return;

        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out pos);
        rectTf.localPosition = pos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        canvasGroup.blocksRaycasts = true;

        bool placed = transform.parent != canvas.transform; // มีใครรับ drop แล้วหรือยัง

        if (!placed)
        {
            // วางไม่โดนช่อง → กลับ "ช่องว่างปัจจุบัน" ของ manager ต้นทาง
            if (_fromBenchDrag && BenchManager.Instance != null)
            {
                var empty = BenchManager.Instance.GetCurrentEmptySlot();
                if (empty != null) SnapTo(empty.transform);
                else SnapTo(OriginalParent);
            }
            else if (!_fromBenchDrag && SpaceManager.Instance != null)
            {
                var empty = SpaceManager.Instance.GetCurrentEmptySlot();
                if (empty != null) SnapTo(empty.transform);
                else SnapTo(OriginalParent);
            }
            else SnapTo(OriginalParent);

            PlaySettle();
        }

        SetDragging(false);

        if (_fromBenchDrag && BenchManager.Instance != null)
            BenchManager.Instance.EndDrag(placed);
        else if (SpaceManager.Instance != null)
            SpaceManager.Instance.EndDrag(placed);
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
        if (IsOnBoard())
        {
            var target = SpaceManager.Instance?.GetFirstEmptySlot(); // หา SpaceSlot ว่างตัวแรก
            if (target == null) return; // ถ้า Space เต็มก็ไม่ทำอะไร (จะให้เด้งเตือนค่อยใส่เพิ่มทีหลัง)

            // ใช้แอนิเมชันบินกลับ (มีอยู่แล้วในคลาส)
            StartCoroutine(FlyToSlot(target.transform));

            // อัปเดตปุ่ม Discard ทันทีให้สะท้อนว่ามีตัวใน Space แล้ว
            SpaceManager.Instance?.RefreshDiscardButton(); // ถ้าไม่มีเมธอดนี้ ให้ดูข้อ 2 ด้านล่าง

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
            StartCoroutine(FlyToSlot(target.transform));
            return;
        }

        // จาก Space → ไป Bench
        if (spaceIdx >= 0 && BenchManager.Instance != null)
        {
            var target = BenchManager.Instance.GetFirstEmptySlot();       // BenchSlot
            if (target == null) return;

            SpaceManager.Instance.CollapseFrom(spaceIdx);
            StartCoroutine(FlyToSlot(target.transform));
            return;
        }
    }
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

        transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();

        Vector3 start = rectTf.position;
        Vector3 end = (targetSlot as RectTransform).TransformPoint(Vector3.zero);

        float t = 0f, dur = Mathf.Max(0.0001f, flyDuration);

        // ใช้ try/finally ให้แน่ใจว่า Pop() เสมอ
        try
        {
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                float a = flyEase.Evaluate(Mathf.Clamp01(t));
                rectTf.position = Vector3.LerpUnclamped(start, end, a);
                yield return null;
            }

            // เคลียร์ผู้โดยสารเก่า ถ้ามี
            if (targetSlot.childCount > 0)
            {
                if (BenchManager.Instance && BenchManager.Instance.IndexOfSlot(targetSlot) >= 0)
                    BenchManager.Instance.KickOutExistingToNearestEmpty(targetSlot);
                else if (SpaceManager.Instance && SpaceManager.Instance.IndexOfSlot(targetSlot) >= 0)
                    SpaceManager.Instance.KickOutExistingToNearestEmpty(targetSlot);
            }

            // เข้าช่องและอยู่บนสุด
            transform.SetParent(targetSlot, false);
            transform.SetAsLastSibling();
            transform.localPosition = Vector3.zero;
            AdjustSizeToParent();
            PlaySettle();
            SpaceManager.Instance?.UpdateDiscardButton();
        }
        finally
        {
            canvasGroup.blocksRaycasts = true;
            isBusy = false;
            UiGuard.Pop();   // <<< ปลดล็อกเสมอ ต่อให้มี exception
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
    private LetterData data;
    public void Setup(LetterData d)
    {
        data = d;
        icon.sprite     = data.sprite;
        letterText.text = data.letter;
        scoreText.text  = data.score.ToString();
        isSpecialTile = data.isSpecial;
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
