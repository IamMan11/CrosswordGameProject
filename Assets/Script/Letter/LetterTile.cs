using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class LetterTile : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler            // ✅ เพิ่มตรงนี้
{
    [Header("UI References")]
    public Image icon;
    public TMP_Text letterText;
    public TMP_Text scoreText;
    public Image specialMark;

    private Canvas canvas;           // หา Canvas หลัก (สำหรับคำนวณตำแหน่ง)
    private CanvasGroup canvasGroup; // ใช้ปิด Raycast ระหว่างลาก
    private RectTransform rectTf;
    private bool isSpecialTile;

    [HideInInspector]
    public Transform OriginalParent; // ให้ BenchSlot.cs เข้าถึงตอนสลับ

    // === NEW ===
    [HideInInspector] public bool IsInSpace = false;

    public bool isLocked = false;

    // ---------- Drag (เหมือนเดิม ไม่แก้) ---------- //

    // … (OnBeginDrag / OnDrag / OnEndDrag เหมือนเวอร์ชันก่อนหน้า) …

    // ---------- Click ---------- //
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked) return;
        Debug.Log($"[Tile] click {data.letter}  IsInSpace={IsInSpace}");

        if (!IsInSpace)
        {
            bool success = SpaceManager.Instance.AddTile(this);
            Debug.Log($"   → AddTile success={success}");
        }
        else
        {
            SpaceManager.Instance.RemoveTile(this);
            Debug.Log("   → remove from Space");
        }
    }


    private void Awake()
    {
        rectTf = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // === Drag Handling ===
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        OriginalParent = transform.parent;
        transform.SetParent(canvas.transform);      // ดึงออกมาเหนือ UI ทั้งหมด
        canvasGroup.blocksRaycasts = false;         // ให้ Drop ตรวจจับได้
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isLocked) return;
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

        // ถ้าไม่ได้ Drop ลง BenchSlot ใดเลย → กลับที่เดิม
        if (transform.parent == canvas.transform)
        {
            transform.SetParent(OriginalParent);
            transform.localPosition = Vector3.zero;
        }
    }

    // ==== ของเดิม =====
    private LetterData data;
    public void Setup(LetterData _data)
    {
        data = _data;
        isSpecialTile = data.isSpecial;

        icon.sprite = data.sprite;
        letterText.text = data.letter;
        scoreText.text = data.score.ToString();

        specialMark.enabled = isSpecialTile;   // เปิด/ปิดกรอบ
        if (isSpecialTile)
        Debug.Log($"[LetterTile] Instantiate ตัวพิเศษ '{data.letter}' ที่ตำแหน่ง {transform.parent.name}");
    }
    public LetterData GetData() => data;

    public void AdjustSizeToParent()
    {
        RectTransform rtTile = GetComponent<RectTransform>();
        RectTransform rtParent = transform.parent.GetComponent<RectTransform>();

        rtTile.anchorMin = rtTile.anchorMax = new Vector2(0.5f, 0.5f);
        rtTile.pivot = new Vector2(0.5f, 0.5f);
        rtTile.sizeDelta = rtParent.sizeDelta;          // เท่าช่องเป๊ะ
        rtTile.localScale = Vector3.one;
    }

    public void Lock() => isLocked = true;
    public bool IsSpecial => isSpecialTile;
}
