// BoardSlot.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public enum SlotType { Normal, DoubleLetter, TripleLetter, DoubleWord, TripleWord }

/// <summary>
/// BoardSlot = ช่องหนึ่งช่องบนกระดาน
/// - แสดงพื้นหลัง/ไฮไลต์/ไอคอนช่องพิเศษ
/// - โต้ตอบเมาส์: โฮเวอร์ส่งให้ PlacementManager, คลิกซ้ายวางตัวอักษร
/// - มีเอฟเฟกต์ Flash ไฮไลต์ (รองรับ timeScale=0)
/// หมายเหตุ: คงชื่อฟิลด์/เมธอดเดิมทั้งหมด เพื่อไม่ให้กระทบสคริปต์อื่น
/// </summary>
[DisallowMultipleComponent]
public class BoardSlot : MonoBehaviour,
    IDropHandler,
    IPointerEnterHandler,
    IPointerClickHandler,
    IPointerDownHandler,     // ← เพิ่ม
    IPointerUpHandler       // ← เพิ่ม
{
    [Header("UI")]
    [Tooltip("ภาพพื้นหลังของช่อง")]
    public Image bg;                 // พื้นหลัง (Image หลักของ Prefab)
    [Tooltip("ภาพไฮไลต์ (เปิด/ปิดเพื่อกระพริบ)")]
    public Image highlight;          // Image ลูกชื่อ “Highlight” (Raycast Target OFF)

    [Header("Icon")]
    [Tooltip("ภาพไอคอนช่องพิเศษ/ช่องกลาง (วางเป็น First Sibling)")]
    public Image icon;               // Image สำหรับโชว์รูปพิเศษ/รูปช่องกลาง

    [Header("Special BG (runtime)")]
    [SerializeField] private Image specialBg; // ลาก Image ว่าง (เต็มช่อง) ใส่ช่องนี้ใน Prefab/Scene
    [Header("Special BG Override")]
    private bool specialBgOverride = false;
    private Color specialBgColor;

    // ---------- Runtime ----------
    [HideInInspector] public int manaGain;
    [HideInInspector] public int row;
    [HideInInspector] public int col;
    [HideInInspector] public SlotType type = SlotType.Normal;

    [HideInInspector] public bool IsLocked = false;

    private Coroutine _flashCo;
    [Header("Hover (Animator optional)")]
    public RectTransform pulseRoot;                  // รากที่ใช้สเกล (ครอบทั้ง highlight และกราฟิกช่อง)
    public Animator hoverAnimator;                   // Animator ที่อยู่บน pulseRoot (ถ้าไม่ใส่จะ auto-get)
    public string pulseTrigger = "Pulse";            // ชื่อ Trigger ที่ให้เด้ง
    public string pulseStateTag = "Pulse";           // Tag ของสเตตเด้ง (ตั้งใน Animator)
    public string previewBool = "PreviewOn";         // (ทางเลือก) ไว้ set bool ให้ Animator รู้ว่ากำลัง preview
    public bool useUnscaledTimeForAnimator = true;   // ให้เด้งตอน Time.timeScale=0 ได้
    Coroutine _hoverCo;       // สำหรับ fallback
    Coroutine _waitAnimCo;    // รอ Animator จบ
    bool _pendingHide;        // ขอซ่อนหลังจบแอนิเมชัน
    bool _pulsePlaying;       // กำลังเด้งอยู่ไหม
    // Fallback (ถ้าไม่ใช้ Animator)
    [Range(1f, 1.2f)] public float hoverScale = 1.08f;
    [Range(0.05f, 0.4f)] public float hoverDuration = 0.18f;   // เวลา “ขยายเข้า”
    [Range(0.05f, 0.4f)] public float hoverOutDuration = 0.06f; // เวลา “หดออก”
    public AnimationCurve hoverEase = AnimationCurve.EaseInOut(0,0,1,1);
    // ชื่อ state Idle ใน Animator (สะกดตามคลิปคุณ)
    public string idleStateName = "Idel";
    static BoardSlot s_currentPreview;
    
    RectTransform PulseTarget => pulseRoot ? pulseRoot : (transform as RectTransform);

    // ===================== Unity Lifecycle =====================

    void ResetPulseScale()
    {
        var t = PulseTarget; if (t) t.localScale = Vector3.one;
    }
    void Awake()
    {
        if (highlight) { highlight.raycastTarget = false; highlight.enabled = false; }
        if (icon) icon.raycastTarget = false;

        if (bg) bg.raycastTarget = true;  // ← สำคัญ: ต้องรับ raycast เพื่อให้ Drop ตกใส่ช่องได้

        if (!pulseRoot) pulseRoot = transform as RectTransform;
        if (!hoverAnimator && pulseRoot) hoverAnimator = pulseRoot.GetComponent<Animator>();
        if (hoverAnimator && useUnscaledTimeForAnimator)
            hoverAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    void OnDisable()
    {
        CancelFlash();

        if (_hoverCo != null) { StopCoroutine(_hoverCo); _hoverCo = null; }
        if (_waitAnimCo != null) { StopCoroutine(_waitAnimCo); _waitAnimCo = null; }
        _pendingHide = false; _pulsePlaying = false;

        if (highlight) highlight.enabled = false;
        if (PulseTarget) PulseTarget.localScale = Vector3.one;
    }
    public void OnDrop(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (eventData == null) return;

        var tileGO = eventData.pointerDrag;
        var tile   = tileGO ? tileGO.GetComponent<LetterTile>() : null;
        if (tile == null) return;

        // ต้องลากมาจาก "สลอตบนบอร์ด" และทั้งสองสลอตต้องเป็น Garbled ชุดเดียวกัน
        var src = tile.OriginalParent ? tile.OriginalParent.GetComponent<BoardSlot>() : null;
        var garbled = Level1GarbledIT.Instance;

        if (src == null || garbled == null || !garbled.AreInSameActiveSet(src, this))
            return; // ไม่ใช่เคสที่อนุญาต → ปล่อยให้ LetterTile บินกลับเองใน OnEndDrag

        // วางลงที่เดิม ก็แค่ใส่กลับ
        if (src == this)
        {
            this.ForcePlaceLetter(tile);
            garbled.MarkSetTouched(this);
            return;
        }

        // สลับแบบ snap (ไม่ทำแอนิเมชัน)
        var other = GetLetterTile();    // ตัวที่อยู่ในปลายทาง (ถ้ามี)
        if (other) src.ForcePlaceLetter(other);
        this.ForcePlaceLetter(tile);

        garbled.MarkSetTouched(this);
        SfxPlayer.Play(SfxId.TileDrop);
    }

    // ===================== Setup/Visual =====================
    /// <summary>กำหนดพิกัด/ชนิด/มานา และวาดสีกับไอคอนเริ่มต้น</summary>
    public void Setup(int r, int c, SlotType t, int _manaGain, Sprite overlaySprite = null)
    {
        row = r;
        col = c;
        type = t;
        manaGain = _manaGain;
        ApplyVisual();
        SetIcon(overlaySprite);
    }

    /// <summary>ลงสีพื้นตามชนิดช่อง (DL/TL/DW/TW/Normal)</summary>
    public void ApplyVisual()
    {
        if (bg == null) return;
        if (specialBgOverride) { bg.color = specialBgColor; return; }

        bg.color = type switch
        {
            SlotType.DoubleLetter => new Color32(88, 184, 255, 255),
            SlotType.TripleLetter => new Color32(0, 120, 255, 255),
            SlotType.DoubleWord   => new Color32(255, 136, 136, 255),
            SlotType.TripleWord   => new Color32(255, 64,  64,  255),
            _ => Color.white
        };
    }

    /// <summary>ตั้ง/เอาไอคอนของช่อง (ยืดเต็มสลอตและไม่รับเมาส์)</summary>
    public void SetIcon(Sprite s)
    {
        if (icon == null) return;

        icon.sprite = s;
        icon.enabled = (s != null);
        icon.raycastTarget = false;

        if (s != null)
        {
            icon.preserveAspect = false;
            var rt = icon.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            icon.type = Image.Type.Simple;
            icon.transform.SetAsFirstSibling(); // ไอคอนอยู่ล่างสุด (พื้น)
        }
    }
    public void SetSpecialBg(Color c)
    {
        specialBgOverride = true;
        specialBgColor    = c;
        if (bg) bg.color = c;
    }

    public void ClearSpecialBg()
    {
        specialBgOverride = false;
        ApplyVisual();
    }

    /// <summary>บังคับวางตัวอักษรลงช่องนี้ทันที (ไม่เล่นอนิเมชันบิน)</summary>
    public void ForcePlaceLetter(LetterTile t)
    {
        if (!t) return;
        var current = GetLetterTile();
        if (current && current != t) current.transform.SetParent(null, false);

        t.transform.SetParent(transform, false);
        t.transform.SetAsLastSibling();
        t.transform.localPosition = Vector3.zero;
        t.AdjustSizeToParent();
        if (icon != null) icon.transform.SetAsFirstSibling();
        t.IsInSpace = false;
    }

    public void SwapWith(BoardSlot other)
    {
        if (other == null || other == this) return;
        var A = GetLetterTile();
        var B = other.GetLetterTile();
        if (!A && !B) return;
        if (B) ForcePlaceLetter(B);
        if (A) other.ForcePlaceLetter(A);
    }

    // ===================== Mouse Interactions =====================
    /// <summary>โฮเวอร์: แจ้ง PlacementManager เพื่อพรีวิว/ตำแหน่งวาง</summary>
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
    {
        Level1GarbledIT.Instance?.BeginDrag(this);
    }
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e)
    {
        Level1GarbledIT.Instance?.EndDrag(this);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        var pm = PlacementManager.Instance;
        if (pm != null)
            pm.HoverSlot(this);
    }

    /// <summary>คลิกซ้าย: โหมด Targeted Flux ก่อน, ไม่งั้นให้วางตัวอักษร</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (Level1GarbledIT.Instance != null &&
            Level1GarbledIT.Instance.HandleClickSlot(this)) return;
        if (eventData == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // ถ้าอยู่ในโหมด Targeted Flux ให้ส่งให้ BoardManager จัดการก่อน
        var bm = BoardManager.Instance;
        if (bm != null && bm.targetedFluxRemaining > 0)
        {
            bm.HandleTargetedFluxClick(row, col);
            return;
        }

        // ปกติ: วางตัวอักษรผ่าน PlacementManager
        var pm = PlacementManager.Instance;
        if (pm != null)
            pm.TryPlaceFromSlot(this);
    }

    // ===================== Highlight / Flash =====================
    /// <summary>
    /// Flash ไฮไลต์สี <paramref name="col"/> จำนวนครั้ง <paramref name="times"/> (หน่วย: วินาทีจริง)
    /// ปลอดภัยต่อ timeScale=0 และการถูกปิด/ทำลายกลางทาง
    /// </summary>
    public void Flash(Color col, int times = 1, float dur = 0.1f)
    {
        if (!this || !gameObject || highlight == null) return;

        if (_flashCo != null) { StopCoroutine(_flashCo); _flashCo = null; }
        _flashCo = StartCoroutine(FlashCo(col, times, dur));
    }

    /// <summary>หยุดแฟลชและปิดไฮไลต์ทันที</summary>
    public void CancelFlash()
    {
        if (_flashCo != null) { StopCoroutine(_flashCo); _flashCo = null; }
        if (highlight != null) highlight.enabled = false;
    }

    /// <summary>คอร์รุตีนกระพริบไฮไลต์ ใช้ WaitForSecondsRealtime รองรับ timeScale=0</summary>
    IEnumerator FlashCo(Color col, int times, float dur)
    {
        if (highlight == null) yield break;

        // เอาไฮไลต์ขึ้นบนสุดของสลอต
        highlight.transform.SetAsLastSibling();

        var c = col; c.a = 0.6f;
        for (int i = 0; i < times; i++)
        {
            if (!this || !gameObject || highlight == null) yield break;
            highlight.enabled = true;  highlight.color = c;
            yield return new WaitForSecondsRealtime(dur);

            if (!this || !gameObject || highlight == null) yield break;
            highlight.enabled = false;
            yield return new WaitForSecondsRealtime(dur);
        }

        if (highlight != null) highlight.enabled = false;
        _flashCo = null;
    }

    /// <summary>ให้ PlacementManager เรียก: เปิดพรีวิวไฮไลต์สีที่กำหนด</summary>
    // === ShowPreview: ขยาย + ค้าง (Animator ถ้ามี, ไม่งั้น fallback) ===
    public void ShowPreview(Color color)
    {
        if (!highlight) return;

        // เอาไฮไลต์ขึ้นบนสุดและเปิดสี
        highlight.transform.SetAsLastSibling();
        highlight.enabled = true;
        highlight.color = color;

        if (hoverAnimator) // --- ใช้ Animator ---
        {
            if (useUnscaledTimeForAnimator)
                hoverAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

            // ยกเลิกคอร์รุตีนก่อนหน้า (ถ้ามี)
            if (_hoverCo != null) { StopCoroutine(_hoverCo); _hoverCo = null; }
            if (_waitAnimCo != null) { StopCoroutine(_waitAnimCo); _waitAnimCo = null; }

            // ถือค้างด้วย PreviewOn และ (ถ้าต้องการ) เด้งสั้นๆ หนึ่งทีตอนเริ่ม
            if (!string.IsNullOrEmpty(previewBool))
                hoverAnimator.SetBool(previewBool, true);

            if (!string.IsNullOrEmpty(pulseTrigger))
            {
                hoverAnimator.ResetTrigger(pulseTrigger);
                hoverAnimator.SetTrigger(pulseTrigger);   // optional
            }
            return;
        }

        // --- Fallback (ไม่มี Animator) → ใช้โค้ดคุมสเกล ---
        if (_hoverCo != null) StopCoroutine(_hoverCo);
        _hoverCo = StartCoroutine(HoverHoldCo(targetScale: hoverScale, dur: hoverDuration, disableAtEnd:false));
    }

    // === HidePreview: ค่อยๆ หดกลับ แล้วค่อยปิดไฮไลต์ ===
    public void HidePreview()
    {
        if (!highlight) return;

        if (hoverAnimator) // --- ใช้ Animator ---
        {
            if (!string.IsNullOrEmpty(previewBool))
                hoverAnimator.SetBool(previewBool, false);

            // รอเวลาหดแล้วค่อยปิดไฮไลต์ (อิงเวลา transition ออกจาก Inspector)
            if (_waitAnimCo != null) StopCoroutine(_waitAnimCo);
            _waitAnimCo = StartCoroutine(WaitPreviewOffCo());
            return;
        }

        // --- Fallback (ไม่มี Animator) ---
        if (_hoverCo != null) StopCoroutine(_hoverCo);
        _hoverCo = StartCoroutine(HoverHoldCo(targetScale: 1f, dur: hoverOutDuration, disableAtEnd:true));
    }

    // คอร์รุตีน “เลื่อนสเกลไปยังเป้าหมาย” แล้ว (ถ้า disableAtEnd=true) ค่อยปิดไฮไลต์ตอนจบ
    IEnumerator HoverHoldCo(float targetScale, float dur, bool disableAtEnd)
    {
        var t = PulseTarget; if (!t) { _hoverCo = null; yield break; }   // PulseTarget = pulseRoot ถ้ามี, ไม่งั้นใช้ RectTransform ของตัวช่อง
        dur = Mathf.Max(0.0001f, dur);

        Vector3 from = t.localScale;
        Vector3 to   = Vector3.one * targetScale;

        float time = 0f;
        while (time < dur && t)
        {
            time += Time.unscaledDeltaTime;
            float a = hoverEase.Evaluate(Mathf.Clamp01(time / dur));
            t.localScale = Vector3.LerpUnclamped(from, to, a);
            yield return null;
        }

        if (t) t.localScale = to;
        _hoverCo = null;

        if (disableAtEnd)
            highlight.enabled = false;
    }

    IEnumerator WaitPreviewOffCo()
    {
        // safety: รอเท่ากับเวลาทรานซิชันออก (ตั้งใน Inspector)
        float t = 0f, wait = Mathf.Max(0.05f, hoverOutDuration);
        while (t < wait)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (highlight) highlight.enabled = false;
        if (PulseTarget) PulseTarget.localScale = Vector3.one;
        _waitAnimCo = null;
    }

    // (ถ้าต้องการแบบ event-driven) ใส่ Animation Event ปลายคลิปเรียกเมธอดนี้
    public void Animator_PulseComplete()
    {
        _pulsePlaying = false;
        if (_pendingHide)
        {
            _pendingHide = false;
            if (highlight) highlight.enabled = false;
            if (PulseTarget) PulseTarget.localScale = Vector3.one;
            if (hoverAnimator) hoverAnimator.SetBool(previewBool, false);
        }
    }

    // ===== Fallback: เด้งด้วยโค้ด โดยสเกลที่ pulseRoot (ช่องทั้งก้อน + highlight จะเด้งพร้อมกัน) =====
    IEnumerator HoverPulseCo()
    {
        _pendingHide = false;
        var t = PulseTarget; if (!t) { _hoverCo = null; yield break; }

        float dur = Mathf.Max(0.0001f, hoverDuration);
        float half = dur * 0.5f;

        float time = 0f;
        Vector3 from = Vector3.one;
        Vector3 to = Vector3.one * hoverScale;
        while (time < half && t)
        {
            time += Time.unscaledDeltaTime;
            float a = hoverEase.Evaluate(Mathf.Clamp01(time / half));
            t.localScale = Vector3.LerpUnclamped(from, to, a);
            yield return null;
        }

        time = 0f;
        from = t ? t.localScale : Vector3.one * hoverScale;
        to = Vector3.one;
        while (time < half && t)
        {
            time += Time.unscaledDeltaTime;
            float a = hoverEase.Evaluate(Mathf.Clamp01(time / half));
            t.localScale = Vector3.LerpUnclamped(from, to, a);
            yield return null;
        }

        if (t) t.localScale = Vector3.one;
        _hoverCo = null;

        if (_pendingHide)
        {
            _pendingHide = false;
            if (highlight) highlight.enabled = false;
            if (PulseTarget) PulseTarget.localScale = Vector3.one;
        }
    }

    // ===================== Tile helpers =====================
    /// <summary>หาตัวอักษร (LetterTile) ลูกของสลอตนี้ (ไล่ดูทุกลูก ไม่นับไอคอน/ไฮไลต์)</summary>
    public LetterTile GetLetterTile()
    {
        foreach (Transform child in transform)
        {
            var lt = child.GetComponent<LetterTile>();
            if (lt != null) return lt;
        }
        return null; // none found
    }

    /// <summary>สลอตนี้มีตัวอักษรอยู่หรือไม่ (ตรวจจาก LetterTile จริง ๆ)</summary>
    public bool HasLetterTile() => GetLetterTile() != null;

    /// <summary>
    /// ลบตัวอักษรออกจากช่องและคืนวัตถุ LetterTile (ไม่ทำลาย)
    /// ผู้เรียกเป็นคนจัดการปลายทาง/ทำลายเอง
    /// </summary>
    public LetterTile RemoveLetter()
    {
        var tile = GetLetterTile();
        if (tile == null) return null;   // กัน NRE
        tile.transform.SetParent(null);  // หลุดจากสลอต
        return tile;
    }

    // ===================== Lock =====================
    /// <summary>ล็อกช่อง (ปรับสีเป็นเทาเข้ม) — หมายเหตุ: ApplyVisual ภายหลังอาจทับสีนี้ได้</summary>
    public void Lock()
    {
        IsLocked = true;
        if (bg != null)
            bg.color = new Color32(120, 120, 120, 255); // สีช่องที่ถูกล็อก
        // ถ้าต้องการเอฟเฟกต์เพิ่ม: Flash(Color.black);
    }
}
