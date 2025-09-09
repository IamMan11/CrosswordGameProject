using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SpaceManager
/// - ดูแลแถวช่อง Space (ซ้าย→ขวา): ขยับ/แทรก/เลื่อนช่องว่างระหว่างลากไทล์
/// - รองรับเตะตัวที่ค้างออกไป “ช่องว่างที่ใกล้สุด” อัตโนมัติ
/// - จัดการปุ่ม Discard (ลบตัวอักษรทั้งหมดใน Space และหักคะแนน 25%)
///
/// หมายเหตุ:
/// - คง public API เดิมทั้งหมด (field/method) เพื่อไม่ให้สคริปต์อื่นพัง
/// - เพิ่มกัน NRE และล้างตัวที่ไม่ได้ใช้ (index)
/// </summary>
public class SpaceManager : MonoBehaviour
{
    public static SpaceManager Instance { get; private set; }

    [Header("Slots (ซ้าย→ขวา)")]
    public List<Transform> slotTransforms = new List<Transform>();

    [Header("Bench Slots (ลากจาก BenchManager)")]
    public List<Transform> benchSlots = new(); // ใช้ใน GetAllBenchTiles()

    [HideInInspector] public LetterTile draggingTile; // ตัวที่กำลังลากอยู่จาก Space/Bench
    private int emptyIndex = -1;                      // ตำแหน่ง "ช่องว่าง" ปัจจุบัน (สำหรับแทรก)

    // จับคอร์รุตีนที่กำลังเลื่อน เพื่อหยุด/แทนที่ได้ปลอดภัย
    private readonly Dictionary<LetterTile, Coroutine> _moving = new();

    [Header("Lerp Settings")]
    public float shiftDuration = 0.12f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Debug")]
    public bool debug = true;

    [Header("Discard Button (ผูกใน Inspector)")]
    public Button discardButton;

    private int _lastHoverIndex = -1;

    [Header("Space Clear/Discard Animation")]
    public float clearStagger = 0.06f;      // ดีเลย์ต่อชิ้นตอน Clear
    public float discardStagger = 0.03f;    // ดีเลย์ต่อชิ้นตอน Discard
    [Tooltip("ความยาวคลิป DiscardPop (วินาที) — หน่วงก่อน Destroy")]
    public float discardClipDuration = 0.30f;

    [Tooltip("ชื่อ Trigger บน Animator ของไทล์")]
    public string triggerKick = "Kick";
    public string triggerDiscard = "Discard";
    Animator GetTileAnimator(LetterTile t)
    {
        if (!t) return null;
        var a = t.visualPivot ? t.visualPivot.GetComponent<Animator>() : null;
        if (!a) a = t.GetComponent<Animator>() ?? t.GetComponentInChildren<Animator>();
        if (a) a.updateMode = AnimatorUpdateMode.UnscaledTime;
        return a;
    }


    /* ===================== Unity ===================== */

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // เริ่มต้นซ่อนปุ่ม + hookup คลิก
        if (discardButton != null)
        {
            discardButton.onClick.AddListener(DiscardAll);
            discardButton.gameObject.SetActive(false);
            RefreshDiscardButton();
        }
    }

    /* ===================== Query / Index ===================== */
    /// <summary>คืน index ของช่อง (ไม่พบ = -1)</summary>
    public int IndexOfSlot(Transform t) => slotTransforms.IndexOf(t);

    void PlayShiftTick() => SfxPlayer.Play(SfxId.SlotShift);

    /// <summary>หา "ช่องว่างแรก" จากซ้าย→ขวา</summary>
    public Transform GetFirstEmptySlot()
    {
        foreach (var t in slotTransforms)
            if (t != null && t.childCount == 0) return t;
        return null;
    }

    /// <summary>คืน Transform ของช่องว่างปัจจุบัน (emptyIndex)</summary>
    public Transform GetCurrentEmptySlot()
    {
        if (emptyIndex >= 0 && emptyIndex < slotTransforms.Count)
            return slotTransforms[emptyIndex];
        return null;
    }

    /* ===================== Drag lifecycle ===================== */

    public void BeginDrag(LetterTile tile, int fromIndex)
    {
        draggingTile = tile;
        emptyIndex = fromIndex;
        _lastHoverIndex = -1;
    }

    public void EndDrag(bool placed)
    {
        draggingTile = null;
        emptyIndex = -1;
        _lastHoverIndex = -1;
    }

    /// <summary>โฮเวอร์ระหว่างลาก: ขยับเพื่อนบ้านให้เกิด "ช่องว่าง" ที่ตำแหน่ง hover</summary>
    public void OnHoverSlot(Transform targetSlot)
    {
        if (draggingTile == null || targetSlot == null) return;

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

        // เล่นเสียง 1 ครั้งต่อ "ตำแหน่ง hover ใหม่"
        if (hover != _lastHoverIndex)
        {
            PlayShiftTick();
            _lastHoverIndex = hover;
        }

        emptyIndex = hover;
    }

    /// <summary>บังคับให้ตำแหน่ง target กลายเป็นช่องว่าง (ใช้ตอน OnDrop)</summary>
    public void EnsureEmptyAt(Transform targetSlot)
    {
        if (draggingTile == null || targetSlot == null) return;

        int target = IndexOfSlot(targetSlot);
        if (target < 0 || target == emptyIndex) return;

        if (target > emptyIndex)
            for (int k = emptyIndex + 1; k <= target; k++)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        else
            for (int k = emptyIndex - 1; k >= target; k--)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k + 1]);

        emptyIndex = target;
        PlayShiftTick(); // มีการแทรกจริง → เล่น 1 ครั้ง
    }

    /// <summary>ถ้าช่องเป้าหมายมีของอยู่ ให้เตะตัวนั้นไป "ช่องว่างที่ใกล้ที่สุด"</summary>
    public void KickOutExistingToNearestEmpty(Transform slot)
    {
        if (slot == null || slot.childCount == 0) return;

        int i = IndexOfSlot(slot);
        int best = -1;

        for (int step = 1; step < slotTransforms.Count; step++)
        {
            int L = i - step, R = i + step;
            if (L >= 0 && slotTransforms[L].childCount == 0) { best = L; break; }
            if (R < slotTransforms.Count && slotTransforms[R].childCount == 0) { best = R; break; }
        }

        if (best >= 0)
            MoveChildToSlot(slot, slotTransforms[best]);
    }

    /// <summary>เมื่อเอาไทล์ออกจาก index ใด ให้ยุบซ้าย→ขวาให้ชิด</summary>
    public void CollapseFrom(int removedIndex)
    {
        if (removedIndex < 0 || removedIndex >= slotTransforms.Count) return;

        for (int k = removedIndex; k < slotTransforms.Count - 1; k++)
            MoveChildToSlot(slotTransforms[k + 1], slotTransforms[k]);

        emptyIndex = slotTransforms.Count - 1;
        _lastHoverIndex = -1;
    }

    /* ===================== Move/Animate ===================== */

    private void MoveChildToSlot(Transform from, Transform to)
    {
        if (from == null || to == null || from.childCount == 0) return;

        var tile = from.GetChild(0).GetComponent<LetterTile>();
        if (!tile) return;

        // ถ้ามีคอร์รุตีนเก่าย้ายไทล์ตัวเดิม → หยุด และ Pop UiGuard ค้าง
        if (_moving.TryGetValue(tile, out var running))
        {
            StopCoroutine(running);
            _moving.Remove(tile);
            UiGuard.Pop();
        }

        UiGuard.Push(); // เริ่มการเลื่อนครั้งใหม่
        _moving[tile] = StartCoroutine(AnimateToSlot(tile, to));
    }

    private IEnumerator AnimateToSlot(LetterTile tile, Transform targetSlot)
    {
        if (tile == null || targetSlot == null) yield break;

        var rt = tile.GetComponent<RectTransform>();
        if (rt == null) yield break;

        // ให้ tile อยู่บนสุดในช่อง
        tile.transform.SetParent(targetSlot, worldPositionStays: true);
        tile.transform.SetAsLastSibling();

        Vector3 startLocal = rt.localPosition;
        Vector3 endLocal = Vector3.zero;

        float t = 0f, dur = Mathf.Max(0.0001f, shiftDuration);

        while (t < 1f && tile != null && rt != null)
        {
            t += Time.unscaledDeltaTime / dur;
            float a = ease.Evaluate(Mathf.Clamp01(t));
            rt.localPosition = Vector3.LerpUnclamped(startLocal, endLocal, a);
            yield return null;
        }

        if (tile != null && rt != null)
        {
            rt.localPosition = endLocal;
            tile.AdjustSizeToParent();
            tile.transform.SetAsLastSibling();
        }

        _moving.Remove(tile);
        UiGuard.Pop(); // ปลดล็อกเมื่อเลื่อนจบจริง
    }

    /* ===================== Add/Remove ===================== */

    /// <summary>พยายามใส่ไทล์ลงช่องว่างแรกใน Space</summary>
    public bool AddTile(LetterTile tile)
    {
        if (tile == null) return false;

        for (int i = 0; i < slotTransforms.Count; i++)
        {
            var slot = slotTransforms[i];
            if (slot != null && slot.childCount == 0)
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

    /// <summary>นำ LetterTile ออกจาก Space แล้วคืนไป Bench</summary>
    public void RemoveTile(LetterTile tile)
    {
        if (tile == null || tile.isLocked) return;

        var targetBench = BenchManager.Instance?.GetFirstEmptySlot();
        if (targetBench == null)
        {
            if (debug) Debug.Log("❌ [Space] Bench full, cannot remove");
            return;
        }

        tile.transform.SetParent(targetBench.transform);
        tile.transform.localPosition = Vector3.zero;
        tile.AdjustSizeToParent();
        tile.IsInSpace = false;

        if (debug && tile.GetData() != null)
            Debug.Log($"[Space] return '{tile.GetData().letter}' to Bench");

        UpdateDiscardButton();
    }

    /* ===================== Query tiles ===================== */

    /// <summary>คืนลิสต์ไทล์ใน Space ตามลำดับซ้าย→ขวา</summary>
    public List<LetterTile> GetPreparedTiles()
    {
        var list = new List<LetterTile>();
        foreach (var slot in slotTransforms)
            if (slot != null && slot.childCount > 0)
            {
                var lt = slot.GetChild(0).GetComponent<LetterTile>();
                if (lt != null) list.Add(lt);
            }
        return list;
    }

    public List<LetterTile> GetAllBenchTiles()
    {
        var list = new List<LetterTile>();
        foreach (var slot in benchSlots)
            if (slot != null && slot.childCount > 0)
            {
                var lt = slot.GetChild(0).GetComponent<LetterTile>();
                if (lt != null) list.Add(lt);
            }
        return list;
    }
    static void TriggerAnim(Animator a, string trig)
    {
        if (!a || string.IsNullOrEmpty(trig)) return;
        a.ResetTrigger(trig);
        a.SetTrigger(trig);
    }

    public void ClearAllToBench()
    {
        if (!isActiveAndEnabled) { ClearAllImmediate(); return; }
        StartCoroutine(ClearAllToBenchCo());
    }

    void ClearAllImmediate()
    {
        var tiles = GetPreparedTiles();
        foreach (var t in tiles) BenchManager.Instance?.ReturnTileToBench(t);
        UpdateDiscardButton();
    }

    IEnumerator ClearAllToBenchCo()
    {
        var tiles = GetPreparedTiles();
        if (tiles.Count == 0) yield break;

        // 1) หยุด Auto Refill ชั่วคราว (กัน Bench เติมจากถุง)
        BenchManager.Instance?.PauseAutoRefill();
        UiGuard.Push();
        try
        {
            // 2) snapshot ช่อง Bench ที่ "ว่าง" ตอนเริ่ม (ซ้าย→ขวา)
            var empties = new System.Collections.Generic.List<Transform>();
            if (BenchManager.Instance && BenchManager.Instance.slotTransforms != null)
                foreach (var s in BenchManager.Instance.slotTransforms)
                    if (s && s.childCount == 0) empties.Add(s);

            int target = 0;
            foreach (var tile in tiles)
            {
                if (!tile) continue;

                // หยุดคลื่น / ออกสถานะ Space
                var anim = GetTileAnimator(tile);               // หา Animator ของไทล์
                TriggerAnim(anim, triggerKick);  // เด้งนิดก่อนบิน (ไม่บังคับ)
                tile.IsInSpace = false;

                // กำหนดจุดหมายเป็นช่องว่างที่ snapshot ไว้
                Transform dst = null;
                if (target < empties.Count) dst = empties[target++];
                else
                {
                    // เผื่อกรณีผิดปกติ ไม่มี snapshot พอ
                    dst = BenchManager.Instance?.GetFirstEmptySlot()?.transform;
                }

                if (dst)
                {
                    // บินเร็วหน่อย
                    float bak = tile.flyDuration;
                    tile.flyDuration = Mathf.Min(bak, 0.18f);
                    tile.FlyTo(dst);
                    tile.flyDuration = bak;
                }
                else
                {
                    // fallback ปลอดภัย
                    BenchManager.Instance?.ReturnTileToBench(tile);
                }

                yield return new WaitForSecondsRealtime(0.04f); // หรือ clearStagger
            }
        }
        finally
        {
            // 3) ปลดล็อกและอัปเดต UI (แต่ "ไม่ refill")
            UiGuard.Pop();
            BenchManager.Instance?.ResumeAutoRefill();
            UpdateDiscardButton();
            // ❌ อย่าเรียก BenchManager.Instance.RefillEmptySlots() ที่นี่
        }
    }

    // ===================== DISCARD → ลบพร้อมอนิเมชัน =====================
    // NOTE: คงชื่อ DiscardAll เดิม เพื่อไม่ให้ที่อื่นพัง
    public void DiscardAll()
    {
        var tiles = GetPreparedTiles();
        if (tiles.Count == 0) { UpdateDiscardButton(); return; }

        // โทษ 25% (รวมจากคะแนนตัวอักษร)
        int sum = 0;
        foreach (var t in tiles) { var d = t?.GetData(); if (d != null) sum += d.score; }
        int penalty = Mathf.CeilToInt(sum * 0.25f);
        TurnManager.Instance?.AddScore(-penalty);

        StartCoroutine(DiscardAllCo());
    }

    IEnumerator DiscardAllCo()
    {
        var tiles = GetPreparedTiles();
        if (tiles.Count == 0) yield break;

        UiGuard.Push();
        try
        {
            foreach (var tile in tiles)
            {
                if (!tile) continue;
                TileAnimatorBinder.Trigger(GetTileAnimator(tile), triggerDiscard);
                Destroy(tile.gameObject, Mathf.Max(0.05f, discardClipDuration)); // ลบหลังคลิปจบ
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, discardStagger));
            }
        }
        finally
        {
            UiGuard.Pop();
            BenchManager.Instance?.RefillEmptySlots();
            UpdateDiscardButton();
        }
    }

    /* ===================== UI ===================== */

    /// <summary>รีเฟรชสถานะปุ่ม Discard (โชว์เมื่อมีตัว ≥ 1)</summary>
    public void RefreshDiscardButton()
    {
        if (discardButton != null)
            discardButton.gameObject.SetActive(GetPreparedTiles().Count > 0);
    }

    /// <summary>อัปเดตปุ่ม Discard (ชื่อเดิมในโค้ดส่วนอื่นเรียกใช้)</summary>
    public void UpdateDiscardButton() => RefreshDiscardButton();
}
