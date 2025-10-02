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
    [Header("Wordle Colors (PlayTraining)")]
    [Tooltip("คำเป้าหมายสำหรับตรวจ (UPPERCASE) — จะถูกตั้งจาก refill ทุกครั้ง")]
    public string trainingTarget = "";

    [Tooltip("สี เขียว/เหลือง/แดง สำหรับ Wordle")]
    public Color wordleGreen  = new Color(0f, 1f, 0f, 0.35f);
    public Color wordleYellow = new Color(1f, 0.92f, 0.016f, 0.35f);
    public Color wordleRed    = new Color(1f, 0f, 0f, 0.35f);
    public Color wordleNone   = new Color(1f, 1f, 1f, 0f); // โปร่งใส

    /// <summary>ฝั่ง refill เรียกทุกครั้งที่เติม "คำฝึก" ใหม่</summary>
    public void SetTrainingTarget(string target)
    {
        trainingTarget = string.IsNullOrWhiteSpace(target) ? "" : target.Trim().ToUpperInvariant();
        RefreshWordleColorsRealtime();
    }
    [Header("Auto Target (PlayTraining)")]
    [Tooltip("ให้ระบบเดาเป้าหมายจากตัวที่เติม (Bench) อัตโนมัติ")]
    public bool autoDetectTarget = true;

    [Tooltip("ขั้นต่ำของความยาวคำที่จะยอมรับเป็น target (กันเคสเติม 1 ตัว)")]
    public int minTargetLength = 2;

    // เก็บลายเซ็นล่าสุดเพื่อรู้ว่า Bench/Space เปลี่ยนจริงไหม
    private string _lastBenchSig = "";
    private string _lastSpaceSig = "";
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
    private void LateUpdate()
    {
        if (autoDetectTarget)
            TryAutoDetectTrainingTarget();
    }
    /// <summary>
    /// เดา target จาก Bench (ก่อน) ถ้า Bench ไม่มีอะไรให้ fallback จาก Space
    /// - เอาตัวอักษรจากซ้าย→ขวา ข้ามช่องว่าง
    /// - จะยอมรับคำใหม่เมื่อแตกต่างจากครั้งก่อนและยาวพอ
    /// </summary>
    public void TryAutoDetectTrainingTarget()
    {
        // 1) จาก Bench ก่อน
        string benchWord = BuildWordFromSlots(benchSlots);
        if (!string.Equals(benchWord, _lastBenchSig, System.StringComparison.Ordinal))
        {
            _lastBenchSig = benchWord;
            if (!string.IsNullOrEmpty(benchWord) && benchWord.Length >= minTargetLength)
            {
                SetTrainingTarget(benchWord);          // ✅ เซ็ต target จาก Bench
                return;                                // จบเลย (Bench มีความสำคัญกว่า)
            }
        }

        // 2) ถ้า Bench ไม่ได้คำที่ใช้ได้ ลอง Space แทน
        string spaceWord = BuildWordFromSlots(slotTransforms);
        if (!string.Equals(spaceWord, _lastSpaceSig, System.StringComparison.Ordinal))
        {
            _lastSpaceSig = spaceWord;
            if (!string.IsNullOrEmpty(spaceWord) && spaceWord.Length >= minTargetLength)
            {
                SetTrainingTarget(spaceWord);          // ✅ เซ็ต target จาก Space (fallback)
            }
        }
    }

    /// <summary>ต่อคำจากรายการช่องซ้าย→ขวา (ข้ามช่องว่าง)</summary>
    private string BuildWordFromSlots(List<Transform> slots)
    {
        if (slots == null) return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(32);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.childCount == 0) continue;

            var t = slot.GetChild(0).GetComponent<LetterTile>();
            if (!t) continue;

            var s = t.CurrentLetter; // ใช้ property ที่คุณมีอยู่แล้ว
            if (!string.IsNullOrEmpty(s))
                sb.Append(char.ToUpperInvariant(s[0]));
        }

        return sb.ToString();
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

        bool movedAny = false;

        if (hover > emptyIndex)
        {
            for (int k = emptyIndex + 1; k <= hover; k++)
                movedAny |= MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        }
        else
        {
            for (int k = emptyIndex - 1; k >= hover; k--)
                movedAny |= MoveChildToSlot(slotTransforms[k], slotTransforms[k + 1]);
        }

        if (hover != _lastHoverIndex && movedAny)
            PlayShiftTick(); // ✅ มีการขยับจริงเท่านั้นถึงจะติ๊ก

        _lastHoverIndex = hover;
        emptyIndex = hover;
    }

    /// <summary>บังคับให้ตำแหน่ง target กลายเป็นช่องว่าง (ใช้ตอน OnDrop)</summary>
    public void EnsureEmptyAt(Transform targetSlot)
    {
        if (draggingTile == null || targetSlot == null) return;

        int target = IndexOfSlot(targetSlot);
        if (target < 0 || target == emptyIndex) return;

        bool movedAny = false;

        if (target > emptyIndex)
            for (int k = emptyIndex + 1; k <= target; k++)
                movedAny |= MoveChildToSlot(slotTransforms[k], slotTransforms[k - 1]);
        else
            for (int k = emptyIndex - 1; k >= target; k--)
                movedAny |= MoveChildToSlot(slotTransforms[k], slotTransforms[k + 1]);

        emptyIndex = target;

        if (movedAny)
            PlayShiftTick(); // ✅ เล่นเสียงเฉพาะเมื่อเกิดการย้าย
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

    private bool MoveChildToSlot(Transform from, Transform to)
    {
        if (from == null || to == null) return false;
        if (from.childCount == 0) return false;

        var tile = from.GetChild(0).GetComponent<LetterTile>();
        if (!tile) return false;

        // ถ้ามีคอร์รุตีนเก่า → หยุด
        if (_moving.TryGetValue(tile, out var running))
        {
            StopCoroutine(running);
            _moving.Remove(tile);
            UiGuard.Pop();
        }

        UiGuard.Push();
        _moving[tile] = StartCoroutine(AnimateToSlot(tile, to));
        return true; // ✅ มีการย้ายจริง
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
                RefreshWordleColorsRealtime();
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
        RefreshWordleColorsRealtime();
        UpdateDiscardButton();
    }
    /// <summary>รีเซ็ตสีทั้งหมดของช่อง</summary>
    void ClearAllSlotColors()
    {
        foreach (var slot in slotTransforms)
        {
            if (!slot) continue;
            var ss = slot.GetComponent<SpaceSlot>();
            if (ss) ss.ClearStateColor();
        }
    }

    /// <summary>
    /// คำนวณสี Wordle ให้ "แต่ละช่อง Space" แบบเรียลไทม์
    /// - ถ้ามีการลากอยู่: จะพรีวิวว่าตัวที่ลากจะไปอยู่ที่ emptyIndex (ตำแหน่งช่องว่างปัจจุบัน)
    /// - สีลงที่ SpaceSlot (Image) ไม่ยุ่งกับ LetterTile
    /// </summary>
    public void RefreshWordleColorsRealtime()
    {
        // ยังไม่กำหนดคำเป้าหมาย → ลบสีทั้งหมด
        if (string.IsNullOrEmpty(trainingTarget))
        {
            ClearAllSlotColors();
            return;
        }

        var target = trainingTarget.ToCharArray();
        int T = target.Length;

        int N = slotTransforms.Count;
        var letters = new char[N]; // ตัวอักษรใน Space ต่อช่อง
        for (int i = 0; i < N; i++)
            letters[i] = '\0';

        // อ่านตัวอักษรจากแต่ละช่อง (ถ้ามีไทล์)
        for (int i = 0; i < N; i++)
        {
            var slot = slotTransforms[i];
            if (slot && slot.childCount > 0)
            {
                var t = slot.GetChild(0).GetComponent<LetterTile>();
                var s = t ? t.CurrentLetter : "";
                letters[i] = string.IsNullOrEmpty(s) ? '\0' : char.ToUpperInvariant(s[0]);
            }
        }

        // ถ้ากำลังลากอยู่ → พรีวิวว่า tile จะไปลงที่ emptyIndex
        if (draggingTile != null && emptyIndex >= 0 && emptyIndex < N)
        {
            var s = draggingTile.CurrentLetter;
            char ch = string.IsNullOrEmpty(s) ? '\0' : char.ToUpperInvariant(s[0]);
            letters[emptyIndex] = ch; // ✅ สมมุติชั่วคราวเพื่อพรีวิวสี
        }

        // เตรียมความถี่ของ target
        var remain = new System.Collections.Generic.Dictionary<char, int>();
        for (int i = 0; i < T; i++)
        {
            char ch = target[i];
            if (!remain.ContainsKey(ch)) remain[ch] = 0;
            remain[ch]++;
        }

        // Pass 1: เขียวก่อน
        var state = new int[N]; // 0=none, 1=green, 2=yellow, 3=red
        for (int i = 0; i < N; i++)
        {
            char ch = letters[i];
            if (ch == '\0') { state[i] = 0; continue; }

            if (i < T && ch == target[i])
            {
                state[i] = 1;
                remain[ch] = Mathf.Max(0, remain[ch] - 1);
            }
        }

        // Pass 2: เหลือง/แดง
        for (int i = 0; i < N; i++)
        {
            if (state[i] == 1) continue; // เขียวแล้ว

            char ch = letters[i];
            if (ch == '\0')
            {
                ApplySlotColor(i, wordleNone);
                continue;
            }

            if (i >= T)
            {
                ApplySlotColor(i, wordleRed);
                state[i] = 3;
                continue;
            }

            if (remain.TryGetValue(ch, out int left) && left > 0)
            {
                ApplySlotColor(i, wordleYellow);
                remain[ch] = left - 1;
                state[i] = 2;
            }
            else
            {
                ApplySlotColor(i, wordleRed);
                state[i] = 3;
            }
        }

        // ช่องที่เป็นเขียวใน pass 1 → ลงสีตอนท้าย (กันโดนทับ)
        for (int i = 0; i < N; i++)
        {
            if (state[i] == 1) ApplySlotColor(i, wordleGreen);
            else if (state[i] == 0) ApplySlotColor(i, wordleNone);
        }
    }

    void ApplySlotColor(int index, Color c)
    {
        if (index < 0 || index >= slotTransforms.Count) return;
        var slot = slotTransforms[index];
        if (!slot) return;

        var ss = slot.GetComponent<SpaceSlot>();
        if (ss) ss.SetStateColor(c);
        // ถ้าไม่ได้ใส่ SpaceSlot ไว้ทุกตัว: รองรับกรณีมี Image อยู่บน slot ตรง ๆ
        else
        {
            var img = slot.GetComponent<Image>();
            if (img) img.color = c;
        }
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
        if (tiles.Count > 0)
            SfxPlayer.Play(SfxId.TileTransfer); // ✅ อย่างน้อย 1 ครั้ง

        foreach (var t in tiles)
            BenchManager.Instance?.ReturnTileToBench(t);

        UpdateDiscardButton();
    }

    IEnumerator ClearAllToBenchCo()
    {
        var tiles = GetPreparedTiles();
        if (tiles.Count == 0) yield break;

        BenchManager.Instance?.PauseAutoRefill();
        UiGuard.Push();
        try
        {
            var empties = new List<Transform>();
            if (BenchManager.Instance && BenchManager.Instance.slotTransforms != null)
                foreach (var s in BenchManager.Instance.slotTransforms)
                    if (s && s.childCount == 0) empties.Add(s);

            int target = 0;
            foreach (var tile in tiles)
            {
                if (!tile) continue;

                // ✅ เล่นเสียงตอนเริ่มย้ายแต่ละตัว
                SfxPlayer.Play(SfxId.TileTransfer);

                var anim = GetTileAnimator(tile);
                TriggerAnim(anim, triggerKick);
                tile.IsInSpace = false;

                Transform dst = (target < empties.Count)
                    ? empties[target++]
                    : BenchManager.Instance?.GetFirstEmptySlot()?.transform;

                if (dst)
                {
                    float bak = tile.flyDuration;
                    tile.flyDuration = Mathf.Min(bak, 0.18f);
                    tile.FlyTo(dst);
                    tile.flyDuration = bak;
                }
                else
                {
                    BenchManager.Instance?.ReturnTileToBench(tile);
                }

                // เดินจังหวะให้เสียงไม่ทับกัน (ใช้ค่าใน Inspector)
                yield return new WaitForSecondsRealtime(clearStagger);
            }
        }
        finally
        {
            UiGuard.Pop();
            BenchManager.Instance?.RefillEmptySlots();
            UpdateDiscardButton();
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
                Destroy(tile.gameObject, Mathf.Max(0.05f, discardClipDuration));
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, discardStagger));
            }

            // ✅ รอให้ชิ้นสุดท้ายถูก Destroy จริง ๆ
            float wait = Mathf.Max(0f, discardClipDuration - discardStagger);
            if (wait > 0f)
                yield return new WaitForSecondsRealtime(wait);
        }
        finally
        {
            UiGuard.Pop();
            BenchManager.Instance?.RefillEmptySlots();   // ตอนนี้มือ “ว่าง” จริง จึงเติมได้
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
