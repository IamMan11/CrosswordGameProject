using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class BenchManager : MonoBehaviour
{
    // -------------------- Singleton --------------------
    public static BenchManager Instance { get; private set; }

    // -------------------- Inspector --------------------
    [Header("Prefabs & Pool")]
    [Tooltip("Prefab ของ LetterTile ที่จะถูกสร้างลงในช่อง Bench")]
    public GameObject letterTilePrefab;

    [Header("Slot Positions (ซ้าย→ขวา)")]
    [Tooltip("ลิสต์ Transform ของแต่ละช่อง Bench (เรียงซ้ายไปขวา)")]
    public List<Transform> slotTransforms = new List<Transform>();

    [Header("Lerp Settings")]
    [Tooltip("ระยะเวลาเลื่อนแถว Bench ต่อ 1 สเต็ป")]
    public float shiftDuration = 0.12f;

    [Tooltip("คีย์เฟรมโค้ง easing สำหรับการเลื่อน")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // -------------------- Runtime State --------------------
    /// <summary>จดจำคอร์รุตีนเลื่อนของแต่ละไทล์ เพื่อหยุด/คุมไม่ให้ซ้อน</summary>
    private readonly Dictionary<LetterTile, Coroutine> _moving = new();

    /// <summary>ไทล์ที่กำลังถูกลากอยู่นอก Bench</summary>
    [HideInInspector] public LetterTile draggingTile;

    /// <summary>ดัชนีช่องว่างปัจจุบัน (ขณะลาก)</summary>
    private int emptyIndex = -1;

    /// <summary>นับจำนวนครั้งที่ Push ให้ตรงกับ Pop กรณีถูกปิด/ยกเลิกกลางคัน</summary>
    private int _uiGuardDepth = 0;

    // ======================================================
    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        RefillEmptySlots();
    }

    private void OnDisable()
    {
        // กันค้าง: หยุดคอร์รุตีนเลื่อนทั้งหมด และเคลียร์สมุดจด
        foreach (var kv in _moving)
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
        }
        _moving.Clear();

        // ให้ UiGuard กลับสู่สมดุล (เท่าจำนวนครั้งที่เคย Push ค้าง)
        while (_uiGuardDepth-- > 0) UiGuard.Pop();
        _uiGuardDepth = 0;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (shiftDuration < 0.0001f) shiftDuration = 0.0001f;
    }
#endif
    #endregion
    // ======================================================

    // -------------------- เสียงเลื่อน --------------------
    void PlayShiftTick() => SfxPlayer.Play(SfxId.SlotShift);

    // -------------------- Helper (ดัชนีช่อง) --------------------
    public int IndexOfSlot(Transform t) => slotTransforms.IndexOf(t);

    // ======================================================
    #region Drag Orchestration (ทำช่องว่าง + เลื่อนเพื่อนบ้าน)
    /// <summary>
    /// เริ่มลากไทล์ออกจาก Bench
    /// </summary>
    public void BeginDrag(LetterTile tile, int fromIndex)
    {
        draggingTile = tile;
        emptyIndex = fromIndex; // บันทึกจุดว่างเริ่มต้น = ช่องเดิมที่ดึงออก
    }

    /// <summary>
    /// ให้ช่อง target กลายเป็น “ช่องว่าง” แล้วเลื่อนเพื่อนบ้านเข้าหาช่องว่าง (ใช้ตอน OnDrop/OnHover)
    /// </summary>
    public void EnsureEmptyAt(Transform targetSlot)
    {
        if (draggingTile == null || targetSlot == null) return;

        int target = IndexOfSlot(targetSlot);
        if (target < 0 || target == emptyIndex) return;

        if (target > emptyIndex)
        {
            // โฮเวอร์/ดรอปไปทางขวา → ไล่เลื่อน k ไปซ้าย
            for (int k = emptyIndex + 1; k <= target; k++)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        }
        else
        {
            // โฮเวอร์/ดรอปไปทางซ้าย → ไล่เลื่อน k ไปขวา
            for (int k = emptyIndex - 1; k >= target; k--)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k + 1]);
        }
        emptyIndex = target;
    }

    /// <summary>
    /// ใช้ตอนลาก: เมื่อชี้ช่องไหน ให้ช่องนั้นเป็น “ช่องว่าง” แล้วไหล Bench ตามทิศ
    /// </summary>
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

        emptyIndex = hover;
    }

    /// <summary>คืน Transform ของ “ช่องว่าง” ปัจจุบัน (ถ้าไม่มีคืน null)</summary>
    public Transform GetCurrentEmptySlot()
    {
        if (emptyIndex >= 0 && emptyIndex < slotTransforms.Count)
            return slotTransforms[emptyIndex];
        return null;
    }

    /// <summary>จบการลาก (ถ้าวางสำเร็จ placed=true, แต่ที่นี่ไม่ต้องทำอะไรเพิ่มเติม)</summary>
    public void EndDrag(bool placed)
    {
        draggingTile = null;
        emptyIndex = -1;
    }

    /// <summary>
    /// ถ้าช่องปลายทางยังมีลูกอยู่ ให้ย้ายลูกนั้นไปยังช่องว่างที่ใกล้ที่สุด (ป้องกันชน)
    /// </summary>
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

        if (best >= 0) MoveChildToSlot(slot, slotTransforms[best]);
    }
    #endregion
    // ======================================================

    #region Movement Core (ย้ายลูกจากช่อง A → B พร้อมแอนิเมชัน)
    /// <summary>
    /// ย้ายลูก (LetterTile เด็กคนแรกของ from) ไปยังช่อง to พร้อมแอนิเมชัน
    /// - กันคอร์รุตีนซ้อน: ถ้ากำลังเลื่อน tile ตัวเดิมอยู่ ให้หยุดก่อนแล้วเริ่มใหม่
    /// - ปรับเลเยอร์ให้ tile อยู่บนสุดของช่องปลายทาง
    /// </summary>
    private void MoveChildToSlot(Transform from, Transform to)
    {
        if (from == null || to == null) return;
        if (from == to) return;                          // ไม่ต้องย้ายถ้าช่องเดียวกัน
        if (from.childCount == 0) return;                // ไม่มีลูกให้ย้าย

        var tile = from.GetChild(0).GetComponent<LetterTile>();
        if (!tile) return;

        // ถ้ามีคอร์รุตีนเลื่อนของไทล์นี้อยู่ ให้หยุดก่อน
        if (_moving.TryGetValue(tile, out var running))
        {
            if (running != null) StopCoroutine(running);
            _moving.Remove(tile);
            SafeUiGuardPop(); // ปลดล็อกเก่า (ถ้ามี)
        }

        SafeUiGuardPush(); // ล็อค UI ระหว่างเลื่อน
        PlayShiftTick();   // ติ๊กเสียงทุกครั้งที่มีการเลื่อนจริง

        _moving[tile] = StartCoroutine(AnimateToSlot(tile, to));
    }

    /// <summary>
    /// คอร์รุตีน: เลื่อนตำแหน่งไทล์เข้า localPosition(0,0) ของช่องปลายทาง ด้วยเวลา/โค้งที่กำหนด
    /// - ใช้ unscaledDeltaTime ให้ทำงานได้ขณะ timeScale=0
    /// - ปลอดภัยต่อกรณีถูกทำลายกลางคัน
    /// </summary>
    private IEnumerator AnimateToSlot(LetterTile tile, Transform targetSlot)
    {
        if (tile == null || targetSlot == null)
        {
            CleanupMove(tile);
            yield break;
        }

        var rt = tile.GetComponent<RectTransform>();
        if (rt == null)
        {
            CleanupMove(tile);
            yield break;
        }

        // จัดลำดับ: เป็นลูกของช่องปลายทาง + อยู่บนสุด
        tile.transform.SetParent(targetSlot, worldPositionStays: true);
        tile.transform.SetAsLastSibling();

        Vector3 startLocal = rt.localPosition;
        Vector3 endLocal = Vector3.zero;

        float t = 0f, dur = Mathf.Max(0.0001f, shiftDuration);
        while (t < 1f)
        {
            // ถ้าถูกทำลาย/ปิด GameObject กลางทาง ให้ยุติอย่างปลอดภัย
            if (tile == null || rt == null || targetSlot == null || !tile.gameObject)
            {
                CleanupMove(tile);
                yield break;
            }

            t += Time.unscaledDeltaTime / dur;
            float a = ease.Evaluate(Mathf.Clamp01(t));
            rt.localPosition = Vector3.LerpUnclamped(startLocal, endLocal, a);
            yield return null;
        }

        // จัดเข้าที่ + ปรับขนาดให้พอดีช่อง
        rt.localPosition = endLocal;
        tile.AdjustSizeToParent();
        tile.transform.SetAsLastSibling();

        CleanupMove(tile);
    }

    /// <summary>ทำความสะอาดสถานะหลังจบ/ยุติการเลื่อน</summary>
    private void CleanupMove(LetterTile tile)
    {
        if (tile != null) _moving.Remove(tile);
        SafeUiGuardPop();
    }

    private void SafeUiGuardPush()
    {
        UiGuard.Push();
        _uiGuardDepth++;
    }
    private void SafeUiGuardPop()
    {
        if (_uiGuardDepth > 0)
        {
            UiGuard.Pop();
            _uiGuardDepth--;
        }
    }
    #endregion
    // ======================================================

    #region Bench Fill / Return / Collapse
    /// <summary>
    /// เติมทุกช่องว่างของ Bench ด้วยไทล์สุ่มจาก TileBag (สำหรับเริ่มเกม/หลัง rerack)
    /// </summary>
    public void RefillEmptySlots()
    {
        if (letterTilePrefab == null)
        {
            Debug.LogError("[BenchManager] letterTilePrefab is null.");
            return;
        }
        if (TileBag.Instance == null) { Debug.LogWarning("[BenchManager] TileBag.Instance is null."); return; }

        foreach (Transform slot in slotTransforms)
        {
            if (slot == null) continue;
            if (slot.childCount > 0) continue;

            var data = TileBag.Instance.DrawRandomTile();
            if (data == null) break;

            CreateTileInSlot(slot, data);
        }
    }

    /// <summary>
    /// เติมแค่ช่องแรกที่ว่าง (ถ้าไม่มีให้เตือน)
    /// </summary>
    public void RefillOneSlot()
    {
        if (letterTilePrefab == null)
        {
            Debug.LogError("[BenchManager] letterTilePrefab is null.");
            return;
        }
        if (TileBag.Instance == null) { Debug.LogWarning("[BenchManager] TileBag.Instance is null."); return; }

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

        CreateTileInSlot(empty.transform, data);
    }

    /// <summary>
    /// คืน LetterTile กลับเข้าสล็อตว่างซ้ายสุด (ถ้าเต็มจะทำลายและคืนตัวอักษรกลับถุง)
    /// </summary>
    public void ReturnTileToBench(LetterTile tile)
    {
        if (tile == null) return;

        foreach (Transform slot in slotTransforms) // ซ้าย → ขวา
        {
            if (slot == null) continue;
            if (slot.childCount == 0)
            {
                tile.transform.SetParent(slot, false);
                tile.transform.localPosition = Vector3.zero;
                tile.transform.localScale = Vector3.one;
                tile.AdjustSizeToParent();
                return;
            }
        }

        // Bench เต็ม (ไม่น่าจะเกิดบ่อย) → คืนตัวอักษรเข้า TileBag แล้วทำลาย GameObject
        if (TileBag.Instance != null) TileBag.Instance.ReturnTile(tile.GetData());
        Destroy(tile.gameObject);
    }

    /// <summary>
    /// เวอร์ชันเดิม: คืน Tile ไปยัง “ช่องแรกที่ว่าง” (ชื่อคงไว้ เผื่อมีที่อื่นเรียก)
    /// </summary>
    public void ReturnTile(LetterTile tile)
    {
        if (tile == null) return;

        var empty = GetFirstEmptySlot();
        if (empty != null)
        {
            tile.transform.SetParent(empty.transform, false);
            tile.transform.localPosition = Vector3.zero;
            tile.transform.localScale = Vector3.one;
            tile.AdjustSizeToParent();
        }
        else
        {
            if (TileBag.Instance != null) TileBag.Instance.ReturnTile(tile.GetData());
            Destroy(tile.gameObject);
        }

        RefillEmptySlots();
    }

    /// <summary>
    /// เลื่อนช่องตั้งแต่ถัดจาก removedIndex ให้ไหลมาชิดซ้าย (ช่องท้ายสุดจะว่าง)
    /// </summary>
    public void CollapseFrom(int removedIndex)
    {
        for (int k = removedIndex + 1; k < slotTransforms.Count; k++)
            MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
    }

    /// <summary>คืน BenchSlot แรกที่ว่าง</summary>
    public BenchSlot GetFirstEmptySlot()
    {
        foreach (Transform t in slotTransforms)
        {
            if (t == null) continue;
            if (t.childCount == 0)
            {
                var slot = t.GetComponent<BenchSlot>();
                if (slot != null) return slot;
            }
        }
        return null;
    }

    /// <summary>
    /// เมธอดช่วย: สร้าง LetterTile ลงใน slot ที่กำหนด และ SetUp + ปรับขนาดให้พอดี
    /// </summary>
    private void CreateTileInSlot(Transform slot, LetterData data)
    {
        if (slot == null || data == null || letterTilePrefab == null) return;

        var tileGO = Instantiate(letterTilePrefab, slot, false);
        tileGO.transform.localPosition = Vector3.zero;
        tileGO.transform.localScale = Vector3.one;

        var tile = tileGO.GetComponent<LetterTile>();
        if (tile != null)
        {
            tile.Setup(data);
            tile.AdjustSizeToParent();
        }
    }
    #endregion
    // ======================================================

    #region Bench FX / Specials
    /// <summary>
    /// (8) Full Rerack – ลบไทล์ทั้งหมดใน Bench แล้วสุ่มชุดใหม่จาก TileBag เติมเต็มทุกช่อง
    /// </summary>
    public void FullRerack()
    {
        foreach (Transform slot in slotTransforms)
        {
            if (slot == null) continue;
            if (slot.childCount > 0)
            {
                // child ตัวเดียวคือ LetterTile
                Destroy(slot.GetChild(0).gameObject);
            }
        }
        RefillEmptySlots();
    }

    /// <summary>
    /// (9/10) ReplaceRandomWithSpecial(count)
    /// - สุ่มช่องที่มีไทล์อยู่ แล้วแทนที่ด้วยตัวพิเศษจาก TileBag จำนวน count ช่อง
    /// </summary>
    public void ReplaceRandomWithSpecial(int count)
    {
        if (TileBag.Instance == null || letterTilePrefab == null) return;

        var filledSlots = new List<Transform>();
        foreach (var slot in slotTransforms)
        {
            if (slot != null && slot.childCount > 0)
                filledSlots.Add(slot);
        }
        if (filledSlots.Count == 0) return;

        for (int i = 0; i < count && filledSlots.Count > 0; i++)
        {
            int idx = Random.Range(0, filledSlots.Count);
            Transform slot = filledSlots[idx];
            if (slot == null) { filledSlots.RemoveAt(idx); continue; }

            // ลบตัวเดิม
            if (slot.childCount > 0) Destroy(slot.GetChild(0).gameObject);

            // ดึงตัวพิเศษจาก TileBag (วนจนเจอหรือถุงหมด)
            LetterData data = null;
            int safety = 200; // ป้องกันลูปยาวเกินไป
            while (safety-- > 0)
            {
                var d = TileBag.Instance.DrawRandomTile();
                if (d == null) break;
                if (d.isSpecial) { data = d; break; }
            }

            if (data != null)
            {
                CreateTileInSlot(slot, data);
            }

            filledSlots.RemoveAt(idx); // กันแทนที่ช่องเดิมซ้ำ
        }
    }

    /// <summary>
    /// OmniSpark – ทำให้ทุกตัวใน Bench กลายเป็น special (ถ้ามีตัวอยู่)
    /// </summary>
    public void OmniSpark()
    {
        bool anyConverted = false;

        foreach (Transform slot in slotTransforms)
        {
            if (slot == null || slot.childCount == 0) continue;

            var lt = slot.GetChild(0).GetComponent<LetterTile>();
            if (lt != null)
            {
                var data = lt.GetData();
                if (data != null)
                {
                    data.isSpecial = true;
                    lt.Setup(data); // อัปเดต UI ให้เห็นกรอบ special (ถ้า LetterTile รองรับ)
                    anyConverted = true;
                }
            }
        }

        if (anyConverted)
            Debug.Log("[BenchManager] OmniSpark: ทุกตัวใน Bench กลายเป็น special แล้ว");
        else
            Debug.Log("[BenchManager] OmniSpark: ไม่มีตัวอักษรใน Bench ให้แปลง");
    }
    #endregion
}
