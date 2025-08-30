using UnityEngine;
using UnityEngine.EventSystems;

public class BenchSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (BenchManager.Instance != null && BenchManager.Instance.draggingTile != null)
            BenchManager.Instance.OnHoverSlot(transform);
    }
    public void OnDrop(PointerEventData eventData)
    {
        var go = eventData.pointerDrag;
        if (!go) return;

        var tile = go.GetComponent<LetterTile>();
        if (!tile) return;

        // A) ทำให้ช่องนี้ว่างก่อน (ในกรณีสอดแทรก/เลื่อนแถว)
        if (BenchManager.Instance && BenchManager.Instance.draggingTile)
            BenchManager.Instance.EnsureEmptyAt(transform);

        // B) กันพลาด: ถ้ายังมีของค้างอยู่ เตะออกไปช่องว่างใกล้สุดก่อน
        if (transform.childCount > 0 && BenchManager.Instance)
            BenchManager.Instance.KickOutExistingToNearestEmpty(transform);

        // C) วางลง
        tile.OriginalParent = transform;
        go.transform.SetParent(transform, false);
        tile.AdjustSizeToParent();

        // ดึ้งเล็กน้อย
        tile.PlaySettle();
        SpaceManager.Instance?.UpdateDiscardButton();
    }
}
