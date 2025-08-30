using UnityEngine;
using UnityEngine.EventSystems;

public class SpaceSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SpaceManager.Instance != null && SpaceManager.Instance.draggingTile != null)
            SpaceManager.Instance.OnHoverSlot(transform);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var go = eventData.pointerDrag;
        if (!go) return;
        var tile = go.GetComponent<LetterTile>();
        if (!tile) return;

        // สร้างช่องว่าง ณ จุดนี้ก่อน
        if (SpaceManager.Instance && SpaceManager.Instance.draggingTile)
            SpaceManager.Instance.EnsureEmptyAt(transform);

        // เข็มขัดนิรภัย ถ้ายังมีของค้าง เตะไปช่องว่างใกล้สุด
        if (transform.childCount > 0 && SpaceManager.Instance)
            SpaceManager.Instance.KickOutExistingToNearestEmpty(transform);

        tile.OriginalParent = transform;
        go.transform.SetParent(transform, false);
        tile.AdjustSizeToParent();
        tile.PlaySettle();
        SpaceManager.Instance?.UpdateDiscardButton();
    }
}
