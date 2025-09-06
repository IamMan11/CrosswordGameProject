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
public class BoardSlot : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("UI")]
    [Tooltip("ภาพพื้นหลังของช่อง")]
    public Image bg;                 // พื้นหลัง (Image หลักของ Prefab)
    [Tooltip("ภาพไฮไลต์ (เปิด/ปิดเพื่อกระพริบ)")]
    public Image highlight;          // Image ลูกชื่อ “Highlight” (Raycast Target OFF)

    [Header("Icon")]
    [Tooltip("ภาพไอคอนช่องพิเศษ/ช่องกลาง (วางเป็น First Sibling)")]
    public Image icon;               // Image สำหรับโชว์รูปพิเศษ/รูปช่องกลาง

    // ---------- Runtime ----------
    [HideInInspector] public int manaGain;
    [HideInInspector] public int row;
    [HideInInspector] public int col;
    [HideInInspector] public SlotType type = SlotType.Normal;

    [HideInInspector] public bool IsLocked = false;

    private Coroutine _flashCo;

    // ===================== Unity Lifecycle =====================
    void Awake()
    {
        if (highlight != null)
        {
            highlight.raycastTarget = false;
            highlight.enabled = false; // เริ่มปิดไว้
        }
        if (icon != null) icon.raycastTarget = false;
    }

    void OnDisable()
    {
        // กันคอร์รุตีนค้างอ้างอิงวัตถุที่ถูกทำลาย/ปิด
        CancelFlash();
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

    // ===================== Mouse Interactions =====================
    /// <summary>โฮเวอร์: แจ้ง PlacementManager เพื่อพรีวิว/ตำแหน่งวาง</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        var pm = PlacementManager.Instance;
        if (pm != null)
            pm.HoverSlot(this);
    }

    /// <summary>คลิกซ้าย: โหมด Targeted Flux ก่อน, ไม่งั้นให้วางตัวอักษร</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
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
    public void ShowPreview(Color color)
    {
        if (highlight == null) return;
        highlight.transform.SetAsLastSibling();
        highlight.enabled = true;
        highlight.color = color;
    }

    /// <summary>ให้ PlacementManager เรียก: ปิดพรีวิวไฮไลต์</summary>
    public void HidePreview()
    {
        if (highlight == null) return;
        highlight.enabled = false;
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
