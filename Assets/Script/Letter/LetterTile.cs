using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// LetterTile
/// - ตัวอักษร 1 ตัวที่ลาก/คลิกย้ายระหว่าง Bench, Space, Board ได้
/// - รองรับไทล์ Blank (ให้ผู้เล่นเลือกอักษร), เอฟเฟกต์อนิเมชัน (Drag/Settle/Pulse)
/// - คลิกเพื่อ "บิน" ไปยังช่องเป้าหมาย, ลาก-ปล่อยเพื่อวาง
/// - ปลอดภัยต่อ timeScale=0 (ใช้ WaitForSecondsRealtime / unscaledDeltaTime)
/// 
/// หมายเหตุ: คงพฤติกรรม/ชื่อเมธอดเดิมทั้งหมด ไม่กระทบสคริปต์อื่น
/// </summary>
public class LetterTile : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    // ===================== Data / Flags =====================
    private LetterData data;

    [SerializeField] private bool isBlankTile = false; // เผื่อกำหนดจาก Inspector (ซ้ำกับกฎ BLANK/?/_)
    private string overrideLetter = null;              // ตัวอักษรที่ผู้เล่นเลือก (เฉพาะ Blank)

    private bool _fromBenchDrag = false;               // true=เริ่มลากจาก Bench, false=จาก Space
    private bool dragging = false;                     // กำลังลากอยู่หรือไม่
    private bool isBusy = false;                       // กันคลิก/ลากซ้อนตอนกำลังบิน
    private bool isSpecialTile;                        // สถานะ special (ซิงก์กับ data)
    public bool isLocked = false;                     // ล็อกการอินพุตสำหรับไทล์นี้
    private bool _garbledBoardDrag = false;
    private bool _inFlight = false;
    private GameObject _benchIssueOverlay;

    [HideInInspector] public bool IsInSpace = false;   // สถานะขณะอยู่ใน Space (เผื่อ UI ใช้)
    private bool wasInSpaceAtDragStart = false;        // จำค่าสถานะตอนเริ่มลาก

    [HideInInspector] public Transform OriginalParent; // ให้ BenchSlot/SpaceSlot ใช้จำ parent

    // ===================== UI Refs =====================
    [Header("UI References")]
    public Image icon;
    public TMP_Text letterText;
    public TMP_Text scoreText;
    public Image specialMark;

    [Header("Visual Root (Animator อยู่ตรงนี้)")]
    public RectTransform visualPivot;

    [Header("Click Move FX")]
    public float flyDuration = 0.2f;
    public AnimationCurve flyEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ===================== Animator Helpers =====================
    private Animator visualAnimator;

    // NOTE: STATE_IDLE ไม่ได้ใช้งาน → ตัดออก
    [SerializeField] private string STATE_DRAG = "Draggloop";
    [SerializeField] private string STATE_SETTLE = "Settle";
    // บนคลาสเดียวกับ PlaySettle()
    static readonly int HASH_DRAGGING = Animator.StringToHash("Dragging");
    static readonly int HASH_INSPACE = Animator.StringToHash("InSpace");
    const string STATE_SPACEWAVE = "SpaceWave";

    [SerializeField] private float settleDebounce = 0.10f; // กันสั่งซ้อนถี่เกินไป
    private float lastSettleTime = -999f;


    // ===================== Canvas / Rect =====================
    private Canvas canvas;           // Canvas หลัก (ใช้เป็นเลเยอร์ลาก/บิน)
    private CanvasGroup canvasGroup; // ปิด Raycast ระหว่างลาก
    private RectTransform rectTf;    // RectTransform ของไทล์เอง
    private RectTransform rootCanvasRect; // RectTransform ของ Root Canvas

    // ===== Unity lifecycle =====
    private void Awake()
    {
        rectTf = GetComponent<RectTransform>();
        RefreshCanvasRef(); 

        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
#if UNITY_2023_1_OR_NEWER
            canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
#else
            canvas = FindObjectOfType<Canvas>();
#endif

        if (canvas != null && canvas.rootCanvas != null)
            rootCanvasRect = canvas.rootCanvas.transform as RectTransform;

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        visualAnimator = visualPivot ? visualPivot.GetComponent<Animator>()
                                     : (GetComponent<Animator>() ?? GetComponentInChildren<Animator>());
    }

    private void OnTransformParentChanged()
    {
        // เมื่อย้ายช่อง (เช่น หลังบินลง Space/Bench) ให้รีเฟรชสถานะปุ่ม discard
        SpaceManager.Instance?.UpdateDiscardButton();
    }

    // ===================== Utilities =====================

    /// <summary>อยู่บน Board หรือไม่ (ดูจาก ancestor ที่เป็น BoardSlot)</summary>
    private bool IsOnBoard()
    {
        if (canvas != null && transform.parent == canvas.transform) return false; // กำลังลอยอยู่บน Canvas
        return GetComponentInParent<BoardSlot>() != null;
    }

    /// <summary>แปลงตำแหน่งจอ → local ของ Root Canvas</summary>
    private Vector2 ScreenToCanvasLocal(Vector2 screenPos, Camera eventCam)
    {
        if (rootCanvasRect == null) return Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootCanvasRect, screenPos, eventCam, out var lp);
        return lp;
    }
    private bool IsOnGarbledSlot()
    {
        var slot = GetComponentInParent<BoardSlot>();
        return Level1GarbledIT.Instance != null && Level1GarbledIT.Instance.IsGarbledSlot(slot);
    }
    // ==== Canvas resolver (กันถูกย้ายไปอยู่ใต้ _SceneTransitioner) ====
    bool IsTransitionerCanvas(Canvas c) =>
        c != null && c.GetComponentInParent<SceneTransitioner>() != null;

    Canvas ResolveGameplayCanvas()
    {
        // 1) เอา Canvas บนสายพ่อแม่ตัวเองก่อน ถ้าไม่ใช่ของ SceneTransitioner
        var c = GetComponentInParent<Canvas>();
        if (c && !IsTransitionerCanvas(c)) return c.rootCanvas;

        // 2) หา Canvas ใน "ซีนเดียวกัน" ที่ไม่ใช่ SceneTransitioner
        Canvas best = null;
        var myScene = gameObject.scene;
        foreach (var ca in FindObjectsOfType<Canvas>(false))
        {
            if (IsTransitionerCanvas(ca)) continue;
            if (ca.gameObject.scene != myScene) continue;
            if (best == null || ca.sortingOrder > best.sortingOrder) best = ca;
        }
        if (best) return best.rootCanvas;

        // 3) เผื่อกรณีสุดท้าย: เอา Canvas ไหนก็ได้ที่ไม่ใช่ SceneTransitioner
        foreach (var ca in FindObjectsOfType<Canvas>(false))
            if (!IsTransitionerCanvas(ca)) return ca.rootCanvas;

        return null;
    }

    void RefreshCanvasRef()
    {
        var c = ResolveGameplayCanvas();
        if (c != null)
        {
            canvas = c;
            rootCanvasRect = c.transform as RectTransform;
        }
    }

    // ===================== Drag Handlers =====================
    public void OnBeginDrag(PointerEventData e)
    {
        bool onBoard = IsOnBoard();
        bool allowBoardDrag = onBoard &&
                            Level1GarbledIT.Instance != null &&
                            Level1GarbledIT.Instance.IsGarbledTile(this);

        if (onBoard && !allowBoardDrag) { dragging = false; return; }

        // เดิม: if (isLocked || isBusy || UiGuard.IsBusy) { ... }
        // ใหม่: อนุญาตถ้าเป็น Garbled แม้ isLocked = true
        if ((isLocked && !allowBoardDrag) || isBusy || UiGuard.IsBusy)
        {
            dragging = false;
            return;
        }

        dragging = true;
        OriginalParent = transform.parent;
        _garbledBoardDrag = allowBoardDrag;

        if (IsBlank && !IsBlankResolved)
        {
            BlankPopup.Show(ch => ResolveBlank(ch));
            return;
        }

        dragging = true;
        OriginalParent = transform.parent;
        _garbledBoardDrag = allowBoardDrag;

        // แจ้ง Bench/Space เฉพาะถ้าไม่ใช่กรณีลากบนบอร์ด Garbled
        int benchIdx = BenchManager.Instance ? BenchManager.Instance.IndexOfSlot(OriginalParent) : -1;
        int spaceIdx = SpaceManager.Instance ? SpaceManager.Instance.IndexOfSlot(OriginalParent) : -1;
        if (!_garbledBoardDrag)
        {
            if (benchIdx >= 0) { _fromBenchDrag = true; BenchManager.Instance.BeginDrag(this, benchIdx); }
            else if (spaceIdx >= 0) { _fromBenchDrag = false; SpaceManager.Instance.BeginDrag(this, spaceIdx); }
        }

        wasInSpaceAtDragStart = IsInSpace;
        RefreshCanvasRef();

        if (canvas != null)
        {
            transform.SetParent(canvas.transform, true);
            transform.SetAsLastSibling();
        }
        canvasGroup.blocksRaycasts = false;
        SetDragging(true);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging || isLocked || isBusy) return;
        if (canvas == null || transform.parent != canvas.transform) return; // ยังไม่ใช่ state ลาก

        // ลอยตามเมาส์ (พิกัดของ root canvas)
        rectTf.localPosition = ScreenToCanvasLocal(e.position, e.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging) return;
        dragging = false;

        canvasGroup.blocksRaycasts = true;

        // เคสลากบนบอร์ด (Garbled)
        if (_garbledBoardDrag)
        {
            bool placed = (canvas != null) ? (transform.parent != canvas.transform) : true;
            bool droppedOnBoard = GetComponentInParent<BoardSlot>() != null;

            if (!placed || !droppedOnBoard)
            {
                // ไม่ได้ปล่อยบนช่องบอร์ด → กลับที่เดิม
                if (OriginalParent) FlyTo(OriginalParent);
                IsInSpace = false;
                SfxPlayer.Play(SfxId.TileDrop);
                PlaySettle();
            }
            else
            {
                // วางบนบอร์ดแล้ว (BoardSlot.OnDrop จะสลับให้เอง ถ้าเป็นชุดเดียวกัน)
                IsInSpace = false;
                SfxPlayer.Play(SfxId.TileDrop);
                PlaySettle();
            }

            SetDragging(false);
            _garbledBoardDrag = false;
            return;
        }

        // ---- พฤติกรรมเดิม (Bench/Space) ----
        bool placedDefault = (canvas != null) ? (transform.parent != canvas.transform) : true;

        if (!placedDefault)
        {
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

            FlyTo(target);
            IsInSpace = (target.GetComponent<BoardSlot>() == null);

            SfxPlayer.Play(SfxId.TileDrop);
            PlaySettle();
        }
        else
        {
            var board = GetComponentInParent<BoardSlot>();
            var space = GetComponentInParent<SpaceSlot>();

            // เดิมตั้งตายตัวเป็น true → แก้ให้ถูกตามที่อยู่จริง
            IsInSpace = (space != null && board == null);

            if (IsInSpace != wasInSpaceAtDragStart) SfxPlayer.Play(SfxId.TileTransfer);
            else SfxPlayer.Play(SfxId.TileDrop);

            PlaySettle();
        }

        SetDragging(false);

        if (_fromBenchDrag) BenchManager.Instance?.EndDrag(placedDefault);
        else SpaceManager.Instance?.EndDrag(placedDefault);
    }

    // ===================== Animator State =====================
    private void SetDragging(bool v)
    {
        if (!visualAnimator) return;

        if (v)
        {
            // ถ้ากำลังเล่น Settle อยู่ ให้ crossfade ไป Drag ทันที กันคำสั่งทับกัน
            var st = visualAnimator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(STATE_SETTLE))
                visualAnimator.CrossFadeInFixedTime(STATE_DRAG, 0.02f, 0, 0f);

            visualAnimator.SetBool(HASH_DRAGGING, true);
        }
        else
        {
            visualAnimator.SetBool(HASH_DRAGGING, false);
        }
    }

    /// <summary>เล่นอนิเมชัน Settle (ดีดเบา ๆ หลังวาง/บินถึง) — ใช้ debounce ป้องกันสั่งถี่</summary>
    public void PlaySettle()
    {
        if (!visualAnimator) return;

        // Debounce
        if (Time.unscaledTime - lastSettleTime < settleDebounce) return;
        lastSettleTime = Time.unscaledTime;

        // 1) บอกสถานะให้ Animator รู้ก่อนว่าอยู่ Space ไหม
        bool inSpace = transform.parent && transform.parent.GetComponent<SpaceSlot>() != null;
        visualAnimator.updateMode = AnimatorUpdateMode.UnscaledTime; // กัน timeScale=0
        visualAnimator.SetBool(HASH_DRAGGING, false);
        visualAnimator.SetBool(HASH_INSPACE, inSpace);

        // 2) เล่น Settle สั้น ๆ
        visualAnimator.CrossFadeInFixedTime(STATE_SETTLE, 0.02f, 0, 0f);

        // (ทางเลือก) ถ้าอยาก “การันตี” เข้าคลื่นหลังจบ Settle แม้กราฟผิดพลาด:
        //StartCoroutine(ForceWaveAfterSettle());
    }

    // ===================== Click-to-Move =====================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked || isBusy || UiGuard.IsBusy) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // Blank ต้องเลือกตัวอักษรก่อน
        if (IsBlank && !IsBlankResolved)
        {
            BlankPopup.Show(ch => { ResolveBlank(ch); });
            return;
        }

        // อยู่บนบอร์ด → คลิกเพื่อย้ายกลับ Space (ถ้ามีช่องว่าง)
        if (IsOnBoard())
        {
            var target = SpaceManager.Instance?.GetFirstEmptySlot();
            if (target == null) return;

            SfxPlayer.Play(SfxId.TileTransfer);
            StartCoroutine(FlyToSlot(target.transform));
            SpaceManager.Instance?.RefreshDiscardButton();
            return;
        }

        // อยู่ Bench → ไป Space
        Transform curParent = transform.parent;
        int benchIdx = BenchManager.Instance ? BenchManager.Instance.IndexOfSlot(curParent) : -1;
        int spaceIdx = SpaceManager.Instance ? SpaceManager.Instance.IndexOfSlot(curParent) : -1;

        if (benchIdx >= 0 && SpaceManager.Instance != null)
        {
            var target = SpaceManager.Instance.GetFirstEmptySlot();
            if (target == null) return;

            BenchManager.Instance.CollapseFrom(benchIdx); // ปรับช่อง Bench ให้ชิด
            SfxPlayer.Play(SfxId.TileTransfer);
            StartCoroutine(FlyToSlot(target.transform));
            return;
        }

        // อยู่ Space → ไป Bench
        if (spaceIdx >= 0 && BenchManager.Instance != null)
        {
            var target = BenchManager.Instance.GetFirstEmptySlot();
            if (target == null) return;

            SpaceManager.Instance.CollapseFrom(spaceIdx);
            SfxPlayer.Play(SfxId.TileTransfer);
            StartCoroutine(FlyToSlot(target.transform));
        }
    }

    // ===================== Blank resolve =====================
    /// <summary>ตั้งอักษรที่เลือกสำหรับ Blank + ปรับ UI (คะแนน Blank = 0)</summary>
    public void ResolveBlank(char ch)
    {
        overrideLetter = char.ToUpperInvariant(ch).ToString();

        if (letterText) letterText.text = overrideLetter;
        if (scoreText) scoreText.text = "0"; // Blank ให้ 0 เสมอ

        // เปลี่ยนภาพ (sprite) ให้เป็นรูปตัวที่เลือก ถ้า TileBag มีข้อมูล
        var bag = TileBag.Instance;
        if (bag != null && icon != null)
        {
            var lc = bag.initialLetters.Find(l =>
                l != null && l.data != null &&
                string.Equals(l.data.letter, overrideLetter, System.StringComparison.OrdinalIgnoreCase));

            if (lc != null && lc.data != null && lc.data.sprite != null)
                icon.sprite = lc.data.sprite;
        }
    }

    public bool IsBlank =>
        isBlankTile ||
        (data != null && (
            string.Equals(data.letter, "BLANK", System.StringComparison.OrdinalIgnoreCase) ||
            data.letter == "?" || data.letter == "_"
        ));

    public bool IsBlankResolved => !IsBlank || !string.IsNullOrEmpty(overrideLetter);

    /// <summary>ตัวอักษร "ที่ใช้จริง" ของไทล์ (ถ้า Blank และเลือกแล้วจะคืน override)</summary>
    public string CurrentLetter => string.IsNullOrEmpty(overrideLetter) ? (data != null ? data.letter : "") : overrideLetter;

    // =========== Fly (animate to slot) =====================

    public void FlyTo(Transform targetSlot)
    {
        StartCoroutine(FlyToSlot(targetSlot));
    }


    private IEnumerator FlyToSlot(Transform targetSlot)
    {
        if (targetSlot == null || rectTf == null) yield break;


        // --- เพิ่มบนหัวเมธอด FlyToSlot หลังเช็ค null ---
        isBusy = true; UiGuard.Push(); canvasGroup.blocksRaycasts = false;

        // ปิด Animator ชั่วคราวตามเดิม...
        var anim = visualAnimator; bool animWasEnabled = false;
        if (anim) { animWasEnabled = anim.enabled; anim.enabled = false; }

        // 1) จำ 'ขนาด local ปัจจุบัน' และ 'ตำแหน่งโลก' ก่อนย้าย
        Vector2 prevLocalSize = rectTf.rect.size;
        Vector3 startPosWorld = rectTf.position;

        // 2) ย้ายขึ้น Canvas
        RefreshCanvasRef();
        if (canvas != null)
        {
            transform.SetParent(canvas.transform, true);
            transform.SetAsLastSibling();
        }

        // 3) ล็อก Rect ไม่ให้ stretch แล้วคง “ขนาดเท่าเดิม”
        rectTf.anchorMin = rectTf.anchorMax = new Vector2(0.5f, 0.5f);
        rectTf.pivot = new Vector2(0.5f, 0.5f);
        rectTf.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, prevLocalSize.x);
        rectTf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, prevLocalSize.y);
        rectTf.position = startPosWorld;
        rectTf.localScale = Vector3.one;


        // จุดเริ่ม/ปลาย (world)
        Vector3 startPos = rectTf.position;
        var targetRt = targetSlot.GetComponent<RectTransform>();
        Vector3 endPos = targetRt ? targetRt.TransformPoint(targetRt.rect.center) : targetSlot.position;


        // --- คำนวณสเกลปลายทางให้พอดีช่อง ---
        Vector2 GetWorldSize(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return new Vector2(Vector3.Distance(c[0], c[3]), Vector3.Distance(c[0], c[1]));
        }


        // 2) วัดขนาดจาก Rect ของตัวไทล์เสมอ (อย่าใช้ visualPivot ถ้ามันเล็ก/0)
        RectTransform srcForSize = rectTf;
        Vector2 startWS = Vector2.one, targetWS = Vector2.one;
        if (srcForSize) startWS = GetWorldSize(srcForSize);
        if (targetRt) targetWS = GetWorldSize(targetRt);

        // 3) คำนวณสเกลแบบกันค่าสุดโต่ง + มี fallback ปลอดภัย
        Transform scaleTarget = visualPivot ? (Transform)visualPivot : (Transform)rectTf;
        Vector3 startScale = scaleTarget.localScale;
        Vector3 endScale = startScale;

        if (startWS.x > 1e-3f && startWS.y > 1e-3f)
        {
            float sx = targetWS.x / startWS.x;
            float sy = targetWS.y / startWS.y;

            // กันพุ่ง: จำกัดช่วงสเกลที่ยอมให้เปลี่ยนระหว่างบิน
            const float MIN_MUL = 0.5f;   // หดสุด 50%
            const float MAX_MUL = 2.0f;   // ขยายสุด 200% (ปรับได้)
            sx = Mathf.Clamp(sx, MIN_MUL, MAX_MUL);
            sy = Mathf.Clamp(sy, MIN_MUL, MAX_MUL);

            if (!float.IsFinite(sx) || !float.IsFinite(sy)) { sx = 1f; sy = 1f; }

            endScale = new Vector3(startScale.x * sx, startScale.y * sy, startScale.z);
        }
        else
        {
            // ถ้าวัดไม่ได้ ให้คงสเกลเดิม
            endScale = startScale;
        }


        float t = 0f, dur = Mathf.Max(0.0001f, flyDuration);


        try
        {
            while (t < 1f)
            {
                while (PauseManager.IsPaused) { yield return null; }
                t += Time.unscaledDeltaTime / dur;
                float a = flyEase.Evaluate(Mathf.Clamp01(t));
                rectTf.position = Vector3.LerpUnclamped(startPos, endPos, a);
                scaleTarget.localScale = Vector3.LerpUnclamped(startScale, endScale, a);
                yield return null;
            }


            // ลงจอดจริง + snap ให้พอดีช่อง
            transform.SetParent(targetSlot, false);
            transform.SetAsLastSibling();
            transform.localPosition = Vector3.zero;


            // รีเซ็ตสเกลบน visualPivot/Rect แล้วฟิตพอดีช่อง
            scaleTarget.localScale = Vector3.one;
            AdjustSizeToParent();
            PlaySettle();
            SpaceManager.Instance?.UpdateDiscardButton();
        }
        finally
        {
            // เปิด Animator คืน
            if (anim) anim.enabled = animWasEnabled;


            canvasGroup.blocksRaycasts = true;
            isBusy = false;
            UiGuard.Pop();
        }
    }

    // ===================== Pulse FX =====================
    static bool AnimatorHasParam(Animator anim, string name, AnimatorControllerParameterType type)
    {
        if (!anim) return false;
        foreach (var p in anim.parameters)
            if (p.type == type && p.name == name) return true;
        return false;
    }

    static readonly System.Collections.Generic.HashSet<Animator> _warned = new();

    Transform GetBounceTarget() => visualPivot ? visualPivot : transform;

    Animator FindAnimatorForPulse()
    {
        var target = GetBounceTarget();
        var anim = target.GetComponent<Animator>();
        if (!anim) anim = GetComponent<Animator>();
        if (!anim) anim = target.GetComponentInChildren<Animator>();
        return anim;
    }

    /// <summary>ทำเอฟเฟกต์เด้งสั้น ๆ (ใช้ Trigger ใน Animator ถ้ามี ไม่งั้นเด้งด้วยโค้ด)</summary>
    public void Pulse()
    {
        var anim = FindAnimatorForPulse();
        if (anim)
        {
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

            if (AnimatorHasParam(anim, "Pulse", AnimatorControllerParameterType.Trigger))
            {
                anim.ResetTrigger("Pulse"); anim.SetTrigger("Pulse"); return;
            }
            if (AnimatorHasParam(anim, "Bounce", AnimatorControllerParameterType.Trigger))
            {
                anim.ResetTrigger("Bounce"); anim.SetTrigger("Bounce"); return;
            }
            if (AnimatorHasParam(anim, "BounceFast", AnimatorControllerParameterType.Trigger))
            {
                anim.ResetTrigger("BounceFast"); anim.SetTrigger("BounceFast"); return;
            }

            if (!_warned.Contains(anim))
            {
                _warned.Add(anim);
                Debug.LogWarning($"[LetterTile] Animator '{anim.runtimeAnimatorController?.name}' ไม่มี Trigger 'Pulse/Bounce/BounceFast' — ใช้โค้ดเด้งแทน");
            }
        }

        // Fallback: เด้งด้วยโค้ด
        StartCoroutine(QuickBounceCo());
    }
    public void PlaySpawnPop()
    {
        var anim = FindAnimatorForPulse(); // หา Animator จาก visualPivot/ตัวเอง/ลูก
        if (anim)
        {
            anim.updateMode = AnimatorUpdateMode.UnscaledTime; // เล่นตอน timeScale=0 ได้
            if (AnimatorHasParam(anim, "Spawn", AnimatorControllerParameterType.Trigger))
            {
                anim.ResetTrigger("Spawn");
                anim.SetTrigger("Spawn");
                return; // ใช้อนิเมชันจริง
            }
        }
        // ถ้าไม่มี Animator/Trigger → เด้งด้วยโค้ดเป็น fallback
        StartCoroutine(QuickBounceCo(0.16f, 1.18f));
    }

    private IEnumerator QuickBounceCo(float dur = 0.12f, float scale = 1.15f)
    {
        var target = GetBounceTarget(); if (!target) yield break;

        Vector3 a = Vector3.one, b = Vector3.one * scale;
        float t = 0f, half = Mathf.Max(0.0001f, dur) * 0.5f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t < half ? (t / half) : (1f - (t - half) / half);
            target.localScale = Vector3.LerpUnclamped(a, b, k);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    // ===================== Data & Utils (public API เดิม) =====================
    /// <summary>ตั้งค่าข้อมูลไทล์ + อัปเดต UI</summary>
    public void Setup(LetterData d)
    {
        data = d;
        if (icon) icon.sprite = data?.sprite;
        if (letterText) letterText.text = data != null ? data.letter : "";
        if (scoreText) scoreText.text = data != null ? data.score.ToString() : "0";

        isSpecialTile = data != null && data.isSpecial;
        isBlankTile = IsBlank; // sync flag ภายในให้ตรงกับกฎ Blank
        if (IsBlank && scoreText) scoreText.text = "0"; // คะแนน Blank = 0

        overrideLetter = null; // ยังไม่เลือกตัวอักษร
        if (specialMark) specialMark.enabled = (data != null && data.isSpecial);
    }

    public void SetSpecial(bool v)
    {
        isSpecialTile = v;
        if (data != null) data.isSpecial = v;
        if (specialMark != null) specialMark.enabled = v;
    }

    public LetterData GetData() => data;

    /// <summary>ปรับขนาด Rect ให้พอดีช่องพาเรนต์</summary>
    public void AdjustSizeToParent()
    {
        var rtTile = GetComponent<RectTransform>();
        var parentRt = transform.parent as RectTransform;
        if (parentRt == null || rtTile == null) return;

        // ถ้ากำลังบิน หรือ พาเรนต์เป็น Canvas → ห้าม stretch เด็ดขาด
        if (_inFlight || (canvas != null && transform.parent == canvas.transform))
        {
            rtTile.anchorMin = rtTile.anchorMax = new Vector2(0.5f, 0.5f);
            rtTile.pivot = new Vector2(0.5f, 0.5f);
            rtTile.localScale = Vector3.one;
            return;
        }

        // ปกติ: ฟิตเต็มช่องพาเรนต์ (Bench/Space/Board)
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
    void LateUpdate()
    {
        if (canvasGroup == null) return;

        // ปล่อยให้ไทล์รับเมาส์ตามปกติทั้งบน Bench/Space/Board
        // (ระหว่างลาก OnBeginDrag จะปิด raycast เอง เพื่อให้ BoardSlot จับ OnDrop ได้)
        if (!dragging && !isBusy)
            canvasGroup.blocksRaycasts = true;
    }
    
    public void SetBenchIssueOverlay(bool on, Color? col = null)
    {
        if (_benchIssueOverlay == null)
        {
            // สร้าง overlay เป็น Image ใสไว้บนสุด
            var go = new GameObject("BenchIssueOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsLastSibling();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(0f, 0f, 0f, 0.55f); // สีเริ่มต้นเทาดำโปร่ง

            _benchIssueOverlay = go;
        }

        if (col.HasValue)
        {
            var img = _benchIssueOverlay.GetComponent<UnityEngine.UI.Image>();
            if (img) img.color = col.Value;
        }

        _benchIssueOverlay.SetActive(on);
    }
}
