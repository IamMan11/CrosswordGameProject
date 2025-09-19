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

    // ==== Hand capacity (Bench + Space) ====
    [Header("Hand Limit")]
    [Tooltip("จำนวนไทล์สูงสุดที่ผู้เล่นถือได้ (Bench + Space)")]
    public int handCapacity = 10;

    [Header("Slot Positions (ซ้าย→ขวา)")]
    [Tooltip("ลิสต์ Transform ของแต่ละช่อง Bench (เรียงซ้ายไปขวา)")]
    public List<Transform> slotTransforms = new List<Transform>();

    [Header("Lerp Settings")]
    [Tooltip("ระยะเวลาเลื่อนแถว Bench ต่อ 1 สเต็ป")]
    public float shiftDuration = 0.12f;
    [Tooltip("คีย์เฟรมโค้ง easing สำหรับการเลื่อน")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Auto Refill (จากถุง)")]
    [SerializeField] bool autoRefillEnabled = true;
    int _refillPause;   // >0 = งดเติมชั่วคราว

    // >>> NEW: Refill Animation
    [Header("Refill Animation")]
    [Tooltip("ให้เติมแบบลอยจากถุงเข้ามาใน Bench ทีละตัว")]
    public bool animateRefill = true;

    [Tooltip("จุด Anchor เหนือถุง (TilePack) ให้ตัวอักษรเกิด/ลอยอยู่ตรงนี้ก่อนบินเข้าช่อง")]
    public RectTransform tileSpawnAnchor;     // ตั้งใน Inspector

    [Tooltip("ให้ถุงเด้งตอนเริ่มเติม (ถ้ามี Animator และ Trigger ชื่อ 'Refill')")]
    public Animator tileBagAnimator;          // optional

    [Tooltip("เวลาหน่วงแต่ละตัวก่อนเกิด/บินเข้า (Realtime)")]
    [Range(0f, 0.5f)] public float refillStagger = 0.08f;

    [Tooltip("เวลาที่ลอยอยู่เหนือถุงก่อนเริ่มบิน (Realtime)")]
    [Range(0f, 0.6f)] public float spawnHoverTime = 0.25f;

    [Tooltip("ระยะยกสูงจาก anchor (พิกัด local Y)")]
    public float spawnHoverHeight = 80f;

    // -------------------- Runtime State --------------------
    private readonly Dictionary<LetterTile, Coroutine> _moving = new();
    [HideInInspector] public LetterTile draggingTile;
    private int emptyIndex = -1;

    private int _uiGuardDepth = 0;

    // >>> NEW: กันเรียกเติมซ้ำช่วงคิดคะแนน / กันซ้อนคอร์รุตีน
    Coroutine _refillCo;
    bool _refillQueued;

    // ======================================================
    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        if (tileSpawnAnchor)
            for (int i = tileSpawnAnchor.childCount - 1; i >= 0; --i)
                Destroy(tileSpawnAnchor.GetChild(i).gameObject);
    }

    private void Start()
    {
        RefillEmptySlots(); // ตัวแรกๆ ตอนเริ่มเกม
    }

    private void OnDisable()
    {
        foreach (var kv in _moving)
            if (kv.Value != null) StopCoroutine(kv.Value);
        _moving.Clear();

        while (_uiGuardDepth-- > 0) UiGuard.Pop();
        _uiGuardDepth = 0;

        // >>> NEW
        if (_refillCo != null) { StopCoroutine(_refillCo); _refillCo = null; }
        _refillQueued = false;
    }
    public void PauseAutoRefill() { _refillPause++; }
    public void ResumeAutoRefill() { _refillPause = Mathf.Max(0, _refillPause - 1); }
    bool CanAutoRefill() => autoRefillEnabled && _refillPause == 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (shiftDuration < 0.0001f) shiftDuration = 0.0001f;
    }
#endif
    #endregion
    // ======================================================

    void PlayShiftTick() => SfxPlayer.Play(SfxId.SlotShift);
    public int IndexOfSlot(Transform t) => slotTransforms.IndexOf(t);

    // ======================================================
    #region Drag Orchestration (ทำช่องว่าง + เลื่อนเพื่อนบ้าน)
    public void BeginDrag(LetterTile tile, int fromIndex)
    {
        draggingTile = tile;
        emptyIndex = fromIndex;
    }

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
    }

    public void OnHoverSlot(Transform targetSlot)
    {
        if (draggingTile == null || targetSlot == null) return;

        int hover = IndexOfSlot(targetSlot);
        if (hover < 0 || hover == emptyIndex) return;

        if (hover > emptyIndex)
            for (int k = emptyIndex + 1; k <= hover; k++)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        else
            for (int k = emptyIndex - 1; k >= hover; k--)
                MoveChildToSlot(slotTransforms[k], slotTransforms[k + 1]);

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

    #region Movement Core
    private void MoveChildToSlot(Transform from, Transform to)
    {
        if (from == null || to == null) return;
        if (from == to) return;
        if (from.childCount == 0) return;

        var tile = from.GetChild(0).GetComponent<LetterTile>();
        if (!tile) return;

        if (_moving.TryGetValue(tile, out var running))
        {
            if (running != null) StopCoroutine(running);
            _moving.Remove(tile);
            SafeUiGuardPop();
        }

        SafeUiGuardPush();
        PlayShiftTick();

        _moving[tile] = StartCoroutine(AnimateToSlot(tile, to));
    }

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

        tile.transform.SetParent(targetSlot, worldPositionStays: true);
        tile.transform.SetAsLastSibling();

        Vector3 startLocal = rt.localPosition;
        Vector3 endLocal = Vector3.zero;

        float t = 0f, dur = Mathf.Max(0.0001f, shiftDuration);
        while (t < 1f)
        {
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

        rt.localPosition = endLocal;
        tile.AdjustSizeToParent();
        tile.transform.SetAsLastSibling();

        CleanupMove(tile);
    }

    private void CleanupMove(LetterTile tile)
    {
        if (tile != null) _moving.Remove(tile);
        SafeUiGuardPop();
    }

    private void SafeUiGuardPush() { UiGuard.Push(); _uiGuardDepth++; }
    private void SafeUiGuardPop() { if (_uiGuardDepth > 0) { UiGuard.Pop(); _uiGuardDepth--; } }
    #endregion
    // ======================================================

    #region Bench Fill / Return / Collapse (IMMEDIATE + ANIMATED)
    /// <summary>
    /// สาธารณะ: เติมทุกช่องว่างของ Bench
    /// - ถ้าอยู่ในช่วงคิดคะแนน จะ "คิวไว้" แล้วรอให้คิดคะแนนเสร็จก่อนจึงเริ่มอนิเมชัน
    /// - กันเรียกซ้ำซ้อนด้วย _refillQueued/_refillCo
    /// </summary>
    private int CountBenchTiles()
    {
        int c = 0;
        if (slotTransforms != null)
            foreach (var s in slotTransforms)
                if (s != null && s.childCount > 0) c++;
        return c;
    }

    private int CountSpaceTiles()
    {
        // ใช้ SpaceManager ถ้ามี
        if (SpaceManager.Instance == null) return 0;
        var list = SpaceManager.Instance.GetPreparedTiles();
        return (list != null) ? list.Count : 0;
    }

    private int CurrentHandCount() => CountBenchTiles() + CountSpaceTiles();
    private int MissingToCapacity() => Mathf.Max(0, handCapacity - CurrentHandCount());
    public void RefillEmptySlots()
    {
        if (!CanAutoRefill()) return;

        int missing = MissingToCapacity();
        if (missing <= 0) return; // มือเต็มแล้ว ไม่ต้องเติม

        bool wantAnim = animateRefill && tileSpawnAnchor != null && letterTilePrefab != null;

        // ถ้ากำลังคิดคะแนน → คิวไว้ครั้งเดียวพอ
        if (wantAnim && TurnManager.Instance != null && TurnManager.Instance.IsScoringAnimation)
        {
            if (_refillQueued) return;                 // กันคิวซ้ำ
            _refillQueued = true;
            StartCoroutine(RefillAfterScoringThenAnimate());
            return;
        }

        // ถ้ามีคอร์รุตีนเติมทำงานอยู่แล้ว → ปล่อยให้ของเดิมทำต่อ ห้ามเริ่มใหม่
        if (_refillCo != null) return;

        if (wantAnim)
            _refillCo = StartCoroutine(RefillAnimatedCo());
        else
            RefillImmediate();
    }


    /// <summary>เติมแบบเดิม (อินสแตนซ์ในช่องเลย)</summary>
    private void RefillImmediate()
    {
        if (letterTilePrefab == null) { Debug.LogError("[BenchManager] letterTilePrefab is null."); return; }
        if (TileBag.Instance == null) { Debug.LogWarning("[BenchManager] TileBag.Instance is null."); return; }

        int missing = MissingToCapacity();
        if (missing <= 0) return;

        foreach (Transform slot in slotTransforms)
        {
            if (missing <= 0) break;           // เติมพอแค่ขาด
            if (slot == null) continue;
            if (slot.childCount > 0) continue;

            var data = TileBag.Instance.DrawRandomTile();
            if (data == null) break;

            CreateTileInSlot(slot, data);
            missing--;                          // นับลดลงเมื่อเติม 1 ตัว
        }
    }

    /// <summary>รอจน Scoring จบก่อน แล้วค่อยเติมแบบแอนิเมชัน</summary>
    private IEnumerator RefillAfterScoringThenAnimate()
    {
        while (TurnManager.Instance != null && TurnManager.Instance.IsScoringAnimation)
            yield return null;

        _refillQueued = false;

        if (_refillCo != null) yield break;           // ยังมีการเติมค้างอยู่ → ไม่เริ่มซ้ำ
        _refillCo = StartCoroutine(RefillAnimatedCo());
    }

    /// <summary>
    /// เติมด้วยอนิเมชัน: โผล่เหนือถุง → ลอยนิ่งสั้น ๆ → บินเข้าสล็อต
    /// </summary>
    private IEnumerator RefillAnimatedCo()
    {
        if (letterTilePrefab == null || TileBag.Instance == null) yield break;
        if (tileSpawnAnchor == null) { RefillImmediate(); yield break; }
        if (!CanAutoRefill()) { _refillCo = null; yield break; }

        if (tileBagAnimator) tileBagAnimator.SetTrigger("Refill");

        // ลบเศษจากรอบก่อน
        for (int i = tileSpawnAnchor.childCount - 1; i >= 0; --i)
            Destroy(tileSpawnAnchor.GetChild(i).gameObject);

        // รวมช่องว่างทั้งหมด
        var emptySlots = new List<Transform>();
        foreach (var slot in slotTransforms)
            if (slot != null && slot.childCount == 0) emptySlots.Add(slot);

        int missing = MissingToCapacity();
        if (missing <= 0) { _refillCo = null; yield break; }

        int filled = 0;
        foreach (var slot in emptySlots)
        {
            if (!CanAutoRefill()) { _refillCo = null; yield break; }
            if (filled >= missing) break;

            var data = TileBag.Instance.DrawRandomTile();
            if (data == null) break;

            // (โค้ดสร้าง go/tile + hover + FlyTo เดิมตามที่มีอยู่)
            var go = Instantiate(letterTilePrefab, tileSpawnAnchor, false);
            var tile = go.GetComponent<LetterTile>();
            if (tile == null)
            {
                Destroy(go);
                CreateTileInSlot(slot, data);
                yield return new WaitForSecondsRealtime(refillStagger);
                filled++;
                continue;
            }

            tile.Setup(data);
            tile.AdjustSizeToParent();                     // ขนาด = TileSpawn
            tile.PlaySpawnPop();                           // pop ก่อน

            // ลอยนิ่ง/โยกสั้น ๆ เหนือ anchor
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(0f, spawnHoverHeight);
                float t = 0f, dur = Mathf.Max(0.0001f, spawnHoverTime);
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / dur);
                    float bob = Mathf.Sin(k * Mathf.PI) * 6f;
                    rt.anchoredPosition = new Vector2(0f, spawnHoverHeight + bob);
                    yield return null;
                }
            }

            // บินเข้า "ช่องปลายทาง" (เหลือ Bench แค่ 10 ช่องตามจริง)
            SfxPlayer.Play(SfxId.TileTransfer);
            tile.FlyTo(slot);
            yield return new WaitForSecondsRealtime(refillStagger);

            filled++;
        }

        _refillCo = null;
    }

    // เติม 1 ช่อง (ถ้าไม่ส่ง prefer จะหา First Empty ให้เอง)
    public void RefillOneSlot(BenchSlot prefer = null, bool forceImmediate = false)
    {
        if (!CanAutoRefill()) return;

        // มือเต็มแล้ว → นับเครดิตคืนถุงแทน (ใช้สำหรับเคสจากการ์ด)
        if (MissingToCapacity() <= 0)
        {
            TileBag.Instance?.AddExtraLetters(1);
            return;
        }

        // เลือกช่องเป้าหมาย
        Transform slotT = null;
        if (prefer != null && prefer.transform.childCount == 0) slotT = prefer.transform;
        else
        {
            foreach (var s in slotTransforms)
                if (s != null && s.childCount == 0) { slotT = s; break; }
        }
        if (slotT == null || TileBag.Instance == null) return;

        bool wantAnim = animateRefill && !forceImmediate && tileSpawnAnchor && letterTilePrefab;

        // ถ้ากำลังคิดคะแนน → รอก่อนค่อยเติมแบบอนิเมชัน
        if (wantAnim && TurnManager.Instance != null && TurnManager.Instance.IsScoringAnimation)
        {
            StartCoroutine(RefillOneAfterScoring(slotT));
            return;
        }

        if (wantAnim) StartCoroutine(RefillOneAnimated(slotT));
        else
        {
            var data = TileBag.Instance.DrawRandomTile();
            if (data == null) return;
            CreateTileInSlot(slotT, data);
        }
    }

    private IEnumerator RefillOneAfterScoring(Transform slotT)
    {
        while (TurnManager.Instance != null && TurnManager.Instance.IsScoringAnimation)
            yield return null;
        yield return RefillOneAnimated(slotT);
    }

    private IEnumerator RefillOneAnimated(Transform slotT)
    {
        if (slotT == null || TileBag.Instance == null || letterTilePrefab == null || tileSpawnAnchor == null)
            yield break;

        if (tileBagAnimator) tileBagAnimator.SetTrigger("Refill");

        // กันของค้างใน Anchor จากรอบที่แล้ว (เผื่อมี)
        for (int i = tileSpawnAnchor.childCount - 1; i >= 0; --i)
            Destroy(tileSpawnAnchor.GetChild(i).gameObject);

        var data = TileBag.Instance.DrawRandomTile();
        if (data == null) yield break;

        var go = Instantiate(letterTilePrefab, tileSpawnAnchor, false);
        var tile = go.GetComponent<LetterTile>();
        if (tile == null)
        {
            Destroy(go);
            CreateTileInSlot(slotT, data);                 // fallback
            yield break;
        }

        tile.Setup(data);
        tile.AdjustSizeToParent();                         // ขนาด = TileSpawn
        tile.PlaySpawnPop();                               // pop ก่อน

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(0f, spawnHoverHeight);
            float t = 0f, dur = Mathf.Max(0.0001f, spawnHoverTime);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float bob = Mathf.Sin(k * Mathf.PI) * 6f;
                rt.anchoredPosition = new Vector2(0f, spawnHoverHeight + bob);
                yield return null;
            }
        }

        SfxPlayer.Play(SfxId.TileTransfer);
        tile.FlyTo(slotT);
        yield return new WaitForSecondsRealtime(refillStagger);
    }
    #endregion
    // ======================================================

    #region Bench Utils / Specials (เดิม)
    public void ReturnTileToBench(LetterTile tile)
    {
        if (tile == null) return;

        foreach (Transform slot in slotTransforms)
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

        if (TileBag.Instance != null) TileBag.Instance.ReturnTile(tile.GetData());
        Destroy(tile.gameObject);
    }

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

    public void CollapseFrom(int removedIndex)
    {
        for (int k = removedIndex + 1; k < slotTransforms.Count; k++)
            MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
    }

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

    public void FullRerack()
    {
        foreach (Transform slot in slotTransforms)
        {
            if (slot == null) continue;
            if (slot.childCount > 0)
                Destroy(slot.GetChild(0).gameObject);
        }
        RefillEmptySlots();
    }

    public void ReplaceRandomWithSpecial(int count)
    {
        if (TileBag.Instance == null || letterTilePrefab == null) return;

        var filledSlots = new List<Transform>();
        foreach (var slot in slotTransforms)
            if (slot != null && slot.childCount > 0) filledSlots.Add(slot);
        if (filledSlots.Count == 0) return;

        for (int i = 0; i < count && filledSlots.Count > 0; i++)
        {
            int idx = Random.Range(0, filledSlots.Count);
            Transform slot = filledSlots[idx];
            if (slot == null) { filledSlots.RemoveAt(idx); continue; }

            if (slot.childCount > 0) Destroy(slot.GetChild(0).gameObject);

            LetterData data = null;
            int safety = 200;
            while (safety-- > 0)
            {
                var d = TileBag.Instance.DrawRandomTile();
                if (d == null) break;
                if (d.isSpecial) { data = d; break; }
            }
            if (data != null) CreateTileInSlot(slot, data);

            filledSlots.RemoveAt(idx);
        }
    }

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
                    lt.Setup(data);
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
    public bool IsRefilling()
    {
        // กำลัง Refill แบบคิวหรือคอร์รุตีนอยู่หรือไม่
        return _refillQueued || _refillCo != null;
    }

    public IEnumerable<LetterTile> GetAllBenchTiles()
    {
        foreach (var t in slotTransforms)
        {
            if (t == null || t.childCount == 0) continue;
            var lt = t.GetChild(0).GetComponent<LetterTile>();
            if (lt) yield return lt;
        }
    }
}