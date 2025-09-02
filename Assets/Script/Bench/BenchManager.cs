using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ใส่สคริปต์นี้บน Empty GameObject "BenchManager"
/// </summary>
public class BenchManager : MonoBehaviour
{
    private readonly Dictionary<LetterTile, Coroutine> _moving = new();
    [Header("Prefabs & Pool")]
    public GameObject letterTilePrefab;

    [Header("Slot Positions (10)")]
    public List<Transform> slotTransforms = new List<Transform>();
    [HideInInspector] public LetterTile draggingTile;
    private int emptyIndex = -1;

    [Header("Lerp Settings")]
    public float shiftDuration = 0.12f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);

    public static BenchManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    void PlayShiftTick() => SfxPlayer.Play(SfxId.SlotShift);
    public int IndexOfSlot(Transform t) => slotTransforms.IndexOf(t);
    public void BeginDrag(LetterTile tile, int fromIndex)
    {
        draggingTile = tile;
        emptyIndex = fromIndex; // ช่องว่างเริ่มต้นคือช่องที่ดึงออกมา
    }
    // เรียกจาก OnDrop/OnHover: บังคับให้ "ช่อง target" กลายเป็นช่องว่าง
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

    // กันพลาด: ถ้าช่องปลายทางยังมีลูกอยู่ ให้ย้ายลูกนั้นไป "ช่องว่างที่ใกล้ที่สุด"
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
    public void OnHoverSlot(Transform targetSlot)
    {
        if (draggingTile == null) return;

        int hover = IndexOfSlot(targetSlot);
        if (hover < 0 || hover == emptyIndex) return;

        // ถ้าโฮเวอร์ไปทางขวา: เลื่อนเพื่อนบ้านไปซ้าย
        if (hover > emptyIndex)
        {
            for (int k = emptyIndex + 1; k <= hover; k++)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        }
        // ถ้าโฮเวอร์ไปทางซ้าย: เลื่อนเพื่อนบ้านไปขวา
        else
        {
            for (int k = emptyIndex - 1; k >= hover; k--)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k + 1]);
        }

        emptyIndex = hover;
    }
    public Transform GetCurrentEmptySlot()
    {
        if (emptyIndex >= 0 && emptyIndex < slotTransforms.Count)
            return slotTransforms[emptyIndex];
        return null;
    }
    public void EndDrag(bool placed)
    {
        draggingTile = null;
        emptyIndex = -1;
    }
    private void MoveChildToSlot(Transform from, Transform to)
    {
        if (from.childCount == 0) return;

        var tile = from.GetChild(0).GetComponent<LetterTile>();
        if (!tile) return;

        if (_moving.TryGetValue(tile, out var running))
        {
            StopCoroutine(running);
            _moving.Remove(tile);
            UiGuard.Pop();
        }

        UiGuard.Push();

        // ★ เพิ่มเหมือนกัน: เสียงเกิดทุกครั้งที่เลื่อนจริง
        PlayShiftTick();

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
    public void CollapseFrom(int removedIndex)
    {
        for (int k = removedIndex + 1; k < slotTransforms.Count; k++)
            MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        // ช่องสุดท้ายจะว่าง
    }
    private void Start() => RefillEmptySlots();

    /// <summary>เติมทุกช่องว่าง (initial หรือเมื่อใช้ Bench Blitz)</summary>
    public void RefillEmptySlots()
    {
        foreach (Transform slot in slotTransforms)
        {
            if (slot.childCount > 0) continue;
            var data = TileBag.Instance.DrawRandomTile();
            if (data == null) break;
            var tileGO = Instantiate(letterTilePrefab, slot, false);
            tileGO.transform.localPosition = Vector3.zero;
            tileGO.transform.localScale = Vector3.one;
            tileGO.GetComponent<LetterTile>().Setup(data);
        }
    }

    /// <summary>เติมแค่ช่องแรกที่ว่าง</summary>
    public void RefillOneSlot()
    {
        var empty = GetFirstEmptySlot();
        if (empty == null)
        {
            Debug.LogWarning("[BenchManager] ไม่มีช่อง Bench ว่าง");
            return;
        }

        var data = TileBag.Instance.DrawRandomTile();
        if (data == null)
        {
            Debug.LogWarning("[BenchManager] ไม่มีตัวอักษรให้ดึงจาก TileBag");
            return;
        }

        var tileGO = Instantiate(letterTilePrefab, empty.transform, false);
        tileGO.transform.localPosition = Vector3.zero;
        tileGO.transform.localScale = Vector3.one;

        var tile = tileGO.GetComponent<LetterTile>();
        tile.Setup(data);
        tile.AdjustSizeToParent();
    }

    /// <summary>คืน Tile ไปที่ Bench (ใส่ใน slot แรกที่ว่าง)</summary>
    public void ReturnTile(LetterTile tile)
    {
        var empty = GetFirstEmptySlot();
        if (empty != null)
        {
            tile.transform.SetParent(empty.transform, false);
            tile.transform.localPosition = Vector3.zero;
            tile.transform.localScale = Vector3.one;
            tile.AdjustSizeToParent();
        }
        else Destroy(tile.gameObject);
        RefillEmptySlots();
    }

    /// <summary>คืน BenchSlot แรกที่ว่าง</summary>
    public BenchSlot GetFirstEmptySlot()
    {
        foreach (Transform t in slotTransforms)
        {
            if (t.childCount == 0)
            {
                var slot = t.GetComponent<BenchSlot>();
                if (slot != null)
                    return slot;
            }
        }
        return null;
    }

    // ========================== เมธอดช่วยสำหรับเอฟเฟกต์ใหม่ ==========================

    /// <summary>
    /// (8) Full Rerack – ลบตัวอักษรใน Bench ทั้งหมด แล้วดึงตัวอักษรชุดใหม่จาก TileBag
    /// </summary>
    public void FullRerack()
    {
        // 1) ทำให้ Bench ว่าง: ทำลาย GameObject ของ LetterTile ทุกตัวใน Bench
        foreach (Transform slot in slotTransforms)
        {
            if (slot.childCount > 0)
            {
                // child ตัวเดียวคือ GameObject ของ LetterTile
                Destroy(slot.GetChild(0).gameObject);
            }
        }
        // 2) จากนั้นเติมใหม่ทุกช่องว่าง
        RefillEmptySlots();
    }

    /// <summary>
    /// (9/10) ReplaceRandomWithSpecial(count) – 
    /// สุ่มเลือกช่องใน Bench ที่มีตัวอักษรอยู่ แล้วแทนที่ด้วยตัวพิเศษจาก TileBag 
    /// count = จำนวนตัวที่อยากให้เป็นพิเศษ (1 = Glyph Spark, 2 = Twin Sparks)
    /// </summary>
    public void ReplaceRandomWithSpecial(int count)
    {
        // เก็บเฉพาะ slot ที่มีตัวอักษรวางอยู่ (ไม่เอาช่องว่าง)
        var filledSlots = new List<Transform>();
        foreach (var slot in slotTransforms)
        {
            if (slot.childCount > 0)
                filledSlots.Add(slot);
        }
        if (filledSlots.Count == 0) return;

        // เลือกสุ่มและแทนที่ ตามจำนวน count (max = filledSlots.Count หากชนกันจะหยุดก่อน)
        for (int i = 0; i < count && filledSlots.Count > 0; i++)
        {
            int idx = Random.Range(0, filledSlots.Count);
            Transform slot = filledSlots[idx];

            // 1) ทำลายตัวอักษรเดิมใน slot
            Destroy(slot.GetChild(0).gameObject);

            // 2) ดึงตัวอักษรใหม่จาก TileBag จนเจอ isSpecial = true
            LetterData data;
            do
            {
                data = TileBag.Instance.DrawRandomTile();
                if (data == null) break;  // ถ้าถุงหมดก็ออก
            } while (!data.isSpecial);

            if (data != null)
            {
                // สร้างตัว LetterTile ใหม่บน slot เดิม
                var tileGO = Instantiate(letterTilePrefab, slot, false);
                tileGO.transform.localPosition = Vector3.zero;
                tileGO.transform.localScale = Vector3.one;
                tileGO.GetComponent<LetterTile>().Setup(data);
            }

            // เอาช่องนี้ออกจากลิสต์ เพื่อไม่ให้ถูกแทนที่ซ้ำ
            filledSlots.RemoveAt(idx);
        }
    }
    public void OmniSpark()
    {
        bool anyConverted = false;
        foreach (Transform slot in slotTransforms)
        {
            if (slot.childCount == 0) continue;
            var lt = slot.GetChild(0).GetComponent<LetterTile>();
            if (lt != null)
            {
                // ดึง LetterData ของ tile ปัจจุบันมาแก้ไข isSpecial = true
                var data = lt.GetData();
                data.isSpecial = true;

                // อัปเดต UI บน LetterTile ให้เห็นกรอบ special (หากมี)
                lt.Setup(data);
                anyConverted = true;
            }
        }
        if (anyConverted)
            Debug.Log("[BenchManager] OmniSpark: ทุกตัวใน Bench กลายเป็น special แล้ว");
        else
            Debug.Log("[BenchManager] OmniSpark: ไม่มีตัวอักษรใน Bench ให้แปลง");
    }
    /// <summary>รับ LetterTile กลับเข้า Bench ที่ว่างซ้ายสุด</summary>
    public void ReturnTileToBench(LetterTile tile)
    {
        foreach (Transform slot in slotTransforms)          // ซ้าย → ขวา
        {
            if (slot.childCount == 0)                       // เจอช่องว่าง
            {
                tile.transform.SetParent(slot, false);      // ย้ายเป็นลูกของ slot
                tile.transform.localPosition = Vector3.zero;
                tile.transform.localScale    = Vector3.one;
                tile.AdjustSizeToParent();                       // (ถ้ามีเมธอดเซตสถานะ)
                return;
            }
        }

        // ถ้าม้านั่งเต็มจริง ๆ (ไม่ควรเกิด) – ค่อยคืนเข้า TileBag หรือทำลาย
        TileBag.Instance.ReturnTile(tile.GetData());
        Destroy(tile.gameObject);
    }

    

    // ================================================================================
}
