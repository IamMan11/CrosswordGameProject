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

    private Animator visualAnimator;
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

    // ================= Drag =================
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked || isBusy) return;

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
        transform.localPosition = Vector3.zero;
        AdjustSizeToParent();
    }

    private void SetDragging(bool v)
    {
        if (visualAnimator != null)
            visualAnimator.SetBool("Dragging", v);
    }

    public void PlaySettle()
    {
        if (visualAnimator != null)
            visualAnimator.SetTrigger("Settle");
    }

    // ================= Click to Move =================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked || isBusy) return;
        // คลิกซ้ายเท่านั้น (กัน double tap ขวา)
        if (eventData.button != PointerEventData.InputButton.Left) return;

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

    private IEnumerator FlyToSlot(Transform targetSlot)
    {
        isBusy = true;
        canvasGroup.blocksRaycasts = false;

        // ย้ายขึ้น Canvas เพื่ออนิเมตแบบ worldPosition
        transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();

        Vector3 start = rectTf.position;
        Vector3 end   = (targetSlot as RectTransform).TransformPoint(Vector3.zero); // center

        float t = 0f, dur = Mathf.Max(0.0001f, flyDuration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float a = flyEase.Evaluate(Mathf.Clamp01(t));
            rectTf.position = Vector3.LerpUnclamped(start, end, a);
            yield return null;
        }

        // เข้าช่องปลายทาง
        transform.SetParent(targetSlot, false);
        transform.localPosition = Vector3.zero;
        AdjustSizeToParent();
        PlaySettle();

        canvasGroup.blocksRaycasts = true;
        isBusy = false;
    }

    // ====== Data & Utils (ของเดิม) ======
    private LetterData data;
    public void Setup(LetterData d)
    {
        data = d;
        icon.sprite     = data.sprite;
        letterText.text = data.letter;
        scoreText.text  = data.score.ToString();
        specialMark.enabled = data.isSpecial;
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
