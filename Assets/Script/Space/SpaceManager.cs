using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class SpaceManager : MonoBehaviour
{
    public static SpaceManager Instance { get; private set; }

    [Header("Slot Positions (10)")]

    [Header("Slots (ซ้าย→ขวา)")]
    public List<Transform> slotTransforms = new List<Transform>();

    [Header("Bench Slots")]
    public List<Transform> benchSlots = new();  // ลาก slotTransforms จาก BenchManager มาที่นี่
    [HideInInspector] public LetterTile draggingTile;
    private int emptyIndex = -1;

    private readonly Dictionary<LetterTile, Coroutine> _moving = new();

    [Header("Lerp Settings")]
    public float shiftDuration = 0.12f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Debug")]
    public bool debug = true;        // เปิด‑ปิด Console log ที่ Inspector
    [Header("Discard Button (ผูกใน Inspector)")]
    public Button discardButton;     // ← ปุ่ม Discard ที่จะลากมาผูก

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
    }
    private void Start()
    {
        // เริ่มต้นซ่อนปุ่ม
        if (discardButton != null)
        {
            discardButton.onClick.AddListener(DiscardAll);
            discardButton.gameObject.SetActive(false);
            RefreshDiscardButton();
        }
    }
    public int IndexOfSlot(Transform t) => slotTransforms.IndexOf(t);

    public Transform GetFirstEmptySlot()
    {
        foreach (var t in slotTransforms)
            if (t.childCount == 0) return t;
        return null;
    }

    public Transform GetCurrentEmptySlot()
    {
        if (emptyIndex >= 0 && emptyIndex < slotTransforms.Count)
            return slotTransforms[emptyIndex];
        return null;
    }

    public void BeginDrag(LetterTile tile, int fromIndex)
    {
        draggingTile = tile;
        emptyIndex = fromIndex;
    }

    public void EndDrag(bool placed)
    {
        draggingTile = null;
        emptyIndex = -1;
    }
    public void OnHoverSlot(Transform targetSlot)
    {
        if (draggingTile == null) return;

        int hover = IndexOfSlot(targetSlot);
        if (hover < 0 || hover == emptyIndex) return;

        if (hover > emptyIndex)
        {
            for (int k = emptyIndex + 1; k <= hover; k++)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        }
        else
        {
            for (int k = emptyIndex - 1; k >= hover; k--)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k + 1]);
        }
        emptyIndex = hover;
    }

    public void EnsureEmptyAt(Transform targetSlot)
    {
        if (draggingTile == null) return;
        int target = IndexOfSlot(targetSlot);
        if (target < 0 || target == emptyIndex) return;

        if (target > emptyIndex)
        {
            for (int k = emptyIndex + 1; k <= target; k++)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        }
        else
        {
            for (int k = emptyIndex - 1; k >= target; k--)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k + 1]);
        }
        emptyIndex = target;
    }
    public void KickOutExistingToNearestEmpty(Transform slot)
    {
        if (slot.childCount == 0) return;

        int i = IndexOfSlot(slot);
        int best = -1;
        for (int step = 1; step < slotTransforms.Count; step++)
        {
            int L = i - step, R = i + step;
            if (L >= 0 && slotTransforms[L].childCount == 0) { best = L; break; }
            if (R < slotTransforms.Count && slotTransforms[R].childCount == 0) { best = R; break; }
        }
        if (best >= 0) MoveChildToSlot(slot, slotTransforms[best]);
    }

    public void CollapseFrom(int removedIndex)
    {
        // รูดปิดช่อง: ขยับทั้งหมดทางขวาเข้าซ้ายทีละ 1
        for (int k = removedIndex + 1; k < slotTransforms.Count; k++)
            MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        // ช่องสุดท้ายจะว่าง
    }

    private void MoveChildToSlot(Transform from, Transform to)
    {
        if (from.childCount == 0) return;

        var tile = from.GetChild(0).GetComponent<LetterTile>();
        if (!tile) return;

        // ถ้ามีคอร์รุตีนเก่า → ยกเลิกแล้ว "Pop" ด้วย (ป้องกัน UiGuard ค้าง)
        if (_moving.TryGetValue(tile, out var running))
        {
            StopCoroutine(running);
            _moving.Remove(tile);
            UiGuard.Pop();                   // <<< เพิ่มบรรทัดนี้
        }

        UiGuard.Push();                      // <<< เริ่มเลื่อนครั้งใหม่: Push
        _moving[tile] = StartCoroutine(AnimateToSlot(tile, to));
    }

    private IEnumerator AnimateToSlot(LetterTile tile, Transform targetSlot)
    {
        var rt = tile.GetComponent<RectTransform>();

        // จัดลำดับให้ tile อยู่บน BG เสมอ
        tile.transform.SetParent(targetSlot, worldPositionStays:true);
        tile.transform.SetAsLastSibling();

        Vector3 startLocal = rt.localPosition;
        Vector3 endLocal   = Vector3.zero;

        float t = 0f, dur = Mathf.Max(0.0001f, shiftDuration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float a = ease.Evaluate(Mathf.Clamp01(t));
            rt.localPosition = Vector3.LerpUnclamped(startLocal, endLocal, a);
            yield return null;
        }

        rt.localPosition = endLocal;
        tile.AdjustSizeToParent();
        tile.transform.SetAsLastSibling();

        _moving.Remove(tile);
        UiGuard.Pop();                       // <<< ปลดล็อกเมื่อเลื่อนจบจริง
    }
    public bool AddTile(LetterTile tile)
    {
        for (int i = 0; i < slotTransforms.Count; i++)
        {
            Transform slot = slotTransforms[i];
            if (slot.childCount == 0)
            {
                tile.transform.SetParent(slot);
                tile.transform.localPosition = Vector3.zero;
                tile.AdjustSizeToParent();
                tile.IsInSpace = true;

                UpdateDiscardButton();
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// นำ LetterTile ออกจาก Space แล้วคืนให้ Bench
    /// </summary>
    public void RemoveTile(LetterTile tile)
    {
        if (tile.isLocked) return;
        BenchSlot targetBench = BenchManager.Instance.GetFirstEmptySlot();
        if (targetBench == null)
        {
            if (debug) Debug.Log("❌ [Space] Bench full, cannot remove");
            return;
        }

        tile.transform.SetParent(targetBench.transform);
        tile.transform.localPosition = Vector3.zero;
        tile.AdjustSizeToParent();
        tile.IsInSpace = false;

        tile.AdjustSizeToParent();

        if (debug) Debug.Log($"[Space] return '{tile.GetData().letter}' to Bench");
        UpdateDiscardButton();
    }
    public void RefreshDiscardButton()
    {
        discardButton?.gameObject.SetActive( GetPreparedTiles().Count > 0 );
    }

    /// <summary>
    /// ดึงรายการ LetterTile ที่เตรียมอยู่ใน Space ตามลำดับซ้าย→ขวา
    /// </summary>
    public List<LetterTile> GetPreparedTiles()
    {
        var list = new List<LetterTile>();
        foreach (var slot in slotTransforms)
            if (slot.childCount > 0)
                list.Add(slot.GetChild(0).GetComponent<LetterTile>());
        return list;
    }
    public List<LetterTile> GetAllBenchTiles()
    {
        var list = new List<LetterTile>();
        foreach (var slot in benchSlots)
            if (slot.childCount > 0)
                list.Add(slot.GetChild(0).GetComponent<LetterTile>());
        return list;
    }
    /// <summary>
    /// ฟังก์ชัน Discard: ลบตัวอักษรทั้งหมดใน Space และหักคะแนน 25%
    /// </summary>
    public void DiscardAll()
    {
        var tiles = GetPreparedTiles();
        if (tiles.Count == 0) return;

        int sumScores = 0;
        foreach (var tile in tiles)
            sumScores += tile.GetData().score;

        // หัก 25% ของคะแนนรวม (ปัดขึ้น)
        int penalty = Mathf.CeilToInt(sumScores * 0.25f);
        TurnManager.Instance.AddScore(-penalty);
        if (debug) Debug.Log($"[Space] Discard all – penalty: {penalty}");

        // ลบตัวอักษร: ทำลาย GameObject หรือคืน Bench ตามต้องการ
        foreach (var tile in tiles)
        {
            // 1. ถอดจาก parent (slot) ทันที
            tile.transform.SetParent(null);
            // 2. ทำลาย gameObject
            Destroy(tile.gameObject);
        }
        BenchManager.Instance.RefillEmptySlots();
        UpdateDiscardButton();
    }
    /// <summary>
    /// อัปเดตสถานะปุ่ม Discard: แสดงถ้ามีตัวอักษร ≥1 ใน Space, ซ่อนถ้าไม่มี
    /// </summary>
    public void UpdateDiscardButton()
    {
        if (discardButton == null) return;
        bool hasAny = GetPreparedTiles().Count > 0;
        discardButton.gameObject.SetActive(hasAny);
    }
}
