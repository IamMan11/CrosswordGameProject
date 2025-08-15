using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class CardDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int slotIndex;           // index ของสล็อตปัจจุบัน (UIManager จะเซ็ตให้)
    public CardData cardData;       // การ์ดในภาพนี้ (UIManager จะเซ็ตให้)
    public Transform OriginalParent { get; private set; }

    RectTransform rect;
    CanvasGroup cg;
    Canvas rootCanvas;
    Vector2 originalAnchoredPos;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cardData == null) return;
        OriginalParent = transform.parent;
        originalAnchoredPos = rect.anchoredPosition;

        // ให้ DropTargets รับอีเวนต์
        cg.blocksRaycasts = false;
        cg.alpha = 0.9f;

        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        rect.SetParent(rootCanvas.transform, true);
        rect.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (cardData == null || rootCanvas == null) return;
        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out localPoint);
        rect.anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // ไม่ว่าฟิวชันจะสำเร็จหรือไม่ คืนกลับ (UIManager จะรีเฟรชเองเมื่อมีการเปลี่ยน)
        cg.blocksRaycasts = true;
        cg.alpha = 1f;

        rect.SetParent(OriginalParent, false);
        rect.anchoredPosition = originalAnchoredPos;
    }

    public void SetData(int index, CardData data, Canvas canvas = null)
    {
        slotIndex = index;
        cardData = data;
        if (canvas != null) rootCanvas = canvas;
    }
}
