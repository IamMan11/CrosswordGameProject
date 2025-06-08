using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

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

        if (IsInSpace && data.letter.Equals("Blank", StringComparison.OrdinalIgnoreCase))
        {
            BlankPopup.Show(chosen =>
            {
                // 1. เปลี่ยนตัวอักษรและรีเซ็ตคะแนน
                data.letter = chosen.ToString();
                data.score  = 0;
                letterText.text = data.letter;
                scoreText .text = "0";

                // 2. หา sprite ของตัวที่เลือกจาก TileBag (LetterCount.data.sprite)
                var lc = TileBag.Instance.initialLetters
                        .Find(x => x.data.letter.Equals(chosen.ToString(), StringComparison.OrdinalIgnoreCase));
                if (lc != null)
                {
                    // อัปเดต data.sprite และ icon.sprite
                    data.sprite = lc.data.sprite;
                    if (icon != null)
                    {
                        icon.sprite = data.sprite;
                        // ปรับให้ไอคอนยืดเต็มขนาด Tile
                        var rtIcon = icon.GetComponent<RectTransform>();
                        rtIcon.anchorMin   = Vector2.zero;
                        rtIcon.anchorMax   = Vector2.one;
                        rtIcon.offsetMin   = Vector2.zero;
                        rtIcon.offsetMax   = Vector2.zero;
                        rtIcon.localScale  = Vector3.one;
                    }
                }

                Debug.Log($"[Blank] Changed to {chosen}");
            });
            return;
        }
        else if (!IsInSpace)
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
        if (transform.parent == canvas.transform)
        {
            transform.SetParent(OriginalParent, false);
            AdjustSizeToParent();
        }
    }
    void OnTransformParentChanged()
    {
        // ถ้า Tile เคยอยู่ใน Space แล้วถูกย้ายออก / ย้ายกลับ ให้รีเฟรชปุ่ม
        if (SpaceManager.Instance != null)
            SpaceManager.Instance.RefreshDiscardButton();
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
        var rtTile   = GetComponent<RectTransform>();
        var parentRt = transform.parent as RectTransform;
        if (parentRt == null) return;

        // ให้ Stretch เต็มพาเรนต์
        rtTile.anchorMin      = Vector2.zero;
        rtTile.anchorMax      = Vector2.one;
        rtTile.anchoredPosition = Vector2.zero;
        rtTile.offsetMin      = Vector2.zero;
        rtTile.offsetMax      = Vector2.zero;
        rtTile.localScale     = Vector3.one;
    }

    public void Lock() => isLocked = true;
    public bool IsSpecial => isSpecialTile;
}
