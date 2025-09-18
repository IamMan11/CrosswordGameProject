using UnityEngine;
using UnityEngine.UI;

/// ใส่ไว้บน Image ของ Overlay (ต้องเปิด Image.raycastTarget = true)
/// ทำหน้าที่: บังคลิกทั้งจอ "ยกเว้น" พื้นที่รู (holes)
public class OverlayHoleRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    [Tooltip("RectTransform ที่อนุญาตให้คลิกทะลุ")]
    public RectTransform[] holes;

    [Tooltip("ขยายขนาดรู (พิกเซล)")]
    public float padding = 16f;

    /// ถ้า return true = Overlay 'รับ' Raycast (บังด้านล่าง)
    /// ถ้า return false = Overlay 'ไม่รับ' Raycast (ปล่อยให้ของข้างล่างโดนคลิก)
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        // ไม่มีรายการรู  => บล็อกทั้งจอ
        if (holes == null || holes.Length == 0) return true;

        for (int i = 0; i < holes.Length; i++)
        {
            var rt = holes[i];
            if (!rt) continue;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, sp, eventCamera, out Vector2 local);

            Rect r = rt.rect;
            r.xMin -= padding; r.xMax += padding;
            r.yMin -= padding; r.yMax += padding;

            // ถ้าอยู่ "ในรู" => อย่าบัง (ปล่อยผ่าน)
            if (r.Contains(local)) return false;
        }

        // นอกทุกๆ รู => บัง
        return true;
    }
}
