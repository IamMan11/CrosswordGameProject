using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// พฤติกรรมของช่องการ์ดใน HUD (hover เพื่อโชว์รายละเอียด / drop เพื่อย้ายหรือ fusion)
/// + เด้งตอนชี้/กดผ่าน Animator
/// + เล่นอนิเมชันหดจนหายตอนกดใช้การ์ด
/// </summary>
[DisallowMultipleComponent]
public class CardSlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IDropHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Data (UIManager จะอัปเดตให้)")]
    public CardData cardInSlot;
    public int slotIndex;

    [Header("FX / Animator")]
    public RectTransform graphicRoot;     // ถ้าเว้นว่างจะใช้ตัวเอง
    public Animator animator;             // ควรอยู่ที่ graphicRoot (UpdateMode = UnscaledTime)
    [Tooltip("ความยาวคลิป Use/Hide (วินาที) ไว้รอให้อนิเมชันจบก่อนเรียกใช้การ์ดจริง")]
    public float useHideDuration = 0.18f;

    // --- Animator param hashes ---
    static readonly int HashHover   = Animator.StringToHash("isHover");
    static readonly int HashPressed = Animator.StringToHash("isPressed");
    static readonly int HashUse     = Animator.StringToHash("Use");

    void Awake()
    {
        if (!graphicRoot) graphicRoot = transform as RectTransform;
        if (!animator)    animator    = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        if (animator)     animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    /* ================= Hover / Press ================= */

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardInSlot != null) UICardInfo.Instance?.Show(cardInSlot); // ให้ CardInfo เลื่อนจากขวา
        if (animator) animator.SetBool(HashHover, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UICardInfo.Instance?.Hide();
        if (animator) animator.SetBool(HashHover, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (animator) animator.SetBool(HashPressed, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (animator) animator.SetBool(HashPressed, false);
    }

    /* ================= Drop (เดิม) ================= */

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;

        var draggedGO = eventData.pointerDrag;
        if (draggedGO == null) return;

        var draggable = draggedGO.GetComponent<CardDraggable>();
        if (draggable == null || draggable.cardData == null) return;

        // ปล่อยใส่ช่องเดิม → ไม่ทำอะไร
        if (draggable.slotIndex == slotIndex) return;

        // ถ้ามีการ์ดอยู่ → ลองฟิวชัน
        if (cardInSlot != null)
        {
            bool ok = CardManager.Instance != null && CardManager.Instance.TryFuseByIndex(draggable.slotIndex, slotIndex);
            if (!ok) UIManager.Instance?.ShowMessage("ไม่สามารถ fusion ได้", 1.2f);
            return;
        }

        // ช่องว่าง → ย้ายการ์ดมาช่องนี้
        CardManager.Instance?.MoveCard(draggable.slotIndex, slotIndex);
    }

    /* ================= Use FX ================= */

    /// <summary>
    /// เล่นคลิป Use (หดจนหาย) แล้วค่อยเรียก onAfter (เช่น ใช้การ์ดจริง)
    /// </summary>
    public IEnumerator PlayUseThen(System.Action onAfter)
    {
        if (animator)
        {
            animator.ResetTrigger(HashUse);
            animator.SetTrigger(HashUse);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, useHideDuration));
        }
        else
        {
            // fallback scale-to-zero
            float t = 0f, dur = Mathf.Max(0.06f, useHideDuration);
            Vector3 s0 = graphicRoot ? graphicRoot.localScale : Vector3.one;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                var s = Vector3.LerpUnclamped(s0, Vector3.zero, 1f - Mathf.Pow(1f - t, 3f));
                if (graphicRoot) graphicRoot.localScale = s;
                yield return null;
            }
        }

        onAfter?.Invoke();

        // ★ สำคัญ: รีเซ็ตช่องให้กลับสู่สถานะ Idle เสมอ
        if (animator)
        {
            animator.SetBool(HashHover, false);
            animator.SetBool(HashPressed, false);
            animator.ResetTrigger(HashUse);
            animator.Rebind();   // กลับ default pose/clip (Idle)
            animator.Update(0f);
        }
        if (graphicRoot)
            graphicRoot.localScale = Vector3.one; // เผื่อ fallback path
    }

}
