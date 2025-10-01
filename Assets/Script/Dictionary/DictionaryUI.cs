using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// DictionaryUI
/// - หน้าพจนานุกรมในเกม: เลือกความยาวคำ → ดึงคำจากฐาน → กรองด้วยตัวอักษรที่มี (Bench+Space+Blank)
/// - คลิกคำเพื่อเตรียมไทล์ใน Space ให้พร้อมวาง (รองรับ BLANK ที่ยังไม่ resolve และกรณี resolve แล้ว)
/// - รองรับแบ่งหน้า (PAGE_SIZE = 10)
/// </summary>
[DisallowMultipleComponent]
public class DictionaryUI : MonoBehaviour
{
    public static DictionaryUI Instance { get; private set; }

    [Header("Dictionary → Space Animation")]
    [SerializeField, Tooltip("ดีเลย์ระหว่างตัวอักษรแต่ละตัว (วินาที)")]
    float pickStagger = 0.06f;

    [SerializeField, Tooltip("เล่นเสียงโอนย้ายระหว่างบิน")]
    bool sfxOnTransfer = true;

    /* ---------- Parents / Prefabs ---------- */
    [Header("Root & Panel")]
    [SerializeField] GameObject panel;

    [Header("Columns (Vertical Layout)")]
    [SerializeField] Transform colWord;      // คอลัมน์ซ้าย (ปุ่มคำ)
    [SerializeField] Transform colType;      // คอลัมน์กลาง (ชนิดคำ)
    [SerializeField] Transform colTrans;     // คอลัมน์ขวา  (คำแปล)

    [Header("Text/Btn Prefabs")]
    [SerializeField] Button prefabWordButton;
    [SerializeField] TMP_Text prefabType;
    [SerializeField] TMP_Text prefabTrans;

    [Header("Navigation")]
    [SerializeField] Button btnPrev;
    [SerializeField] Button btnNext;
    [SerializeField] Button btnClear;
    [SerializeField] TMP_Text pageLabel;    // "Page X / Y"

    [Header("Create-Word Buttons (1-10)")]
    [SerializeField] Button[] btnLen;       // index 0 = 1, …, 9 = 10

    [Header("Query Settings")]
    [SerializeField, Tooltip("ดึงคำจากฐานต่อความยาวสูงสุดกี่คำ ก่อนคัดกรองกับตัวอักษรบน Bench/Space")]
    private int fetchLimitPerLen = 8000;

    [SerializeField, Tooltip("ความยาวคำสูงสุดที่อนุญาตให้กดเลือกได้")]
    private int maxLenSelectable = 10;
    [SerializeField] Animator panelAnimator;
    [SerializeField] CanvasGroup panelGroup;
    [SerializeField] string showTrigger = "Show";
    [SerializeField] string hideTrigger = "Hide";

    /* ---------- State ---------- */
    const int PAGE_SIZE = 10;
    int pageIdx = 0;
    int currentLen = 0;                  // ความยาวที่กำลังแสดง (0 = ยังไม่เลือก)
    readonly List<string> viewWords = new(); // คำที่ “ประกอบได้จริง” หลังกรองแล้ว

    /* ---------- Awake ---------- */
    void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }
        BindButtons();
        if (panel) panel.SetActive(false);
        if (panel && !panelAnimator) panelAnimator = panel.GetComponent<Animator>();
        if (panel && !panelGroup)
        {
            panelGroup = panel.GetComponent<CanvasGroup>();
            if (!panelGroup) panelGroup = panel.AddComponent<CanvasGroup>();
        }
    }

    /* ---------- Public API ---------- */
    // เรียกตอนเปิด (เหมือนเดิมแต่สั่ง Animator แทน)
    public void Open()
    {
        TurnManager.Instance?.SetDictionaryUsed();

        if (panel && !panel.activeSelf) panel.SetActive(true);
        if (panelGroup)
        {
            panelGroup.blocksRaycasts = true;
            panelGroup.interactable   = true;
        }
        if (panelAnimator) { panelAnimator.ResetTrigger(hideTrigger); panelAnimator.SetTrigger(showTrigger); }

        int blank;
        var avail = GetAvailableLetterCounts(out blank);
        int totalLetters = avail.Values.Sum() + blank;
        int startLen = Mathf.Clamp(Mathf.Max(2, totalLetters), 2, Math.Max(2, maxLenSelectable));
        ApplyLengthFilter(startLen, autoFallbackToShorter: true);
        RenderPage();
    }

    // เรียกตอนปิด (ปล่อยให้ Animator เล่น Hide แล้วค่อย inactive ด้วย Animation Event)
    public void Close()
    {
        if (panelAnimator)
        {
            if (panelGroup)
            {
                panelGroup.blocksRaycasts = false; // กันจิ้มระหว่างอนิเมชันออก
                panelGroup.interactable   = false;
            }
            panelAnimator.ResetTrigger(showTrigger);
            panelAnimator.SetTrigger(hideTrigger);
        }
        else
        {
            if (panel) panel.SetActive(false);
        }
    }

    // ====== ถูกเรียกจาก Animation Event ======
    public void AnimEvt_OnDictionaryPanelShownBegin()
    {
        if (!panel) return;
        if (panelGroup)
        {
            panelGroup.blocksRaycasts = true;
            panelGroup.interactable   = true;
        }
        panel.SetActive(true);
    }

    public void AnimEvt_OnDictionaryPanelHiddenBegin()
    {
        if (!panel) return;
        // ซ่อนไว้ตั้งแต่ต้นคลิป Hide ก็ได้ หรือจะไปใส่เป็น ...HiddenEnd() ที่เฟรมท้ายคลิปก็ได้ตามถนัด
        if (panelGroup)
        {
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable   = false;
        }
        panel.SetActive(false);
    }

    /* ---------- Buttons ---------- */
    void BindButtons()
    {
        if (btnPrev) btnPrev.onClick.AddListener(OnPrevPage);
        if (btnNext) btnNext.onClick.AddListener(OnNextPage);
        if (btnClear) btnClear.onClick.AddListener(OnClear);

        if (btnLen == null) return;
        for (int i = 0; i < btnLen.Length; i++)
        {
            var b = btnLen[i];
            if (b == null) continue;

            int len = i + 1;
            if (len > maxLenSelectable)
            {
                b.gameObject.SetActive(false);
                continue;
            }
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() =>
            {
                ApplyLengthFilter(len, autoFallbackToShorter: false);
                RenderPage();
            });
        }
    }

    /* ---------- Filtering / Fetching ---------- */

    /// <summary>
    /// ดึงคำจากฐานตามความยาว len แล้วกรองให้ “ประกอบได้จริง” จากตัวอักษรบน Bench+Space
    /// ถ้าไม่เจอคำเลยและ autoFallbackToShorter=true จะลองลดความยาวลงทีละ 1 จนถึง 2
    /// </summary>
    void ApplyLengthFilter(int len, bool autoFallbackToShorter)
    {
        if (WordChecker.Instance == null || !WordChecker.Instance.IsReady())
        {
            Debug.LogWarning("[DictionaryUI] WordChecker not ready.");
            return;
        }

        int blank;
        var avail = GetAvailableLetterCounts(out blank);

        // ถ้า len มากกว่าจำนวนตัวทั้งหมด (รวม blank) → ลดลง
        int capLen = Mathf.Min(len, Mathf.Max(0, avail.Values.Sum() + blank));
        int tryLen = Mathf.Clamp(capLen, 1, maxLenSelectable);
        bool found = false;

        while (tryLen >= 2)
        {
            var pool = WordChecker.Instance.GetWordsByLength(tryLen, fetchLimitPerLen) ?? new List<string>();
            viewWords.Clear();

            foreach (var w in pool)
                if (CanMake(w, avail, blank)) viewWords.Add(w);

            if (viewWords.Count > 0)
            {
                currentLen = tryLen;
                found = true;
                break;
            }

            if (!autoFallbackToShorter) break;
            tryLen--;
        }

        if (!found) { currentLen = 0; viewWords.Clear(); }
        pageIdx = 0;
    }

    /// <summary>
    /// นับจำนวนตัวอักษรที่ “ใช้ได้จริงตอนนี้”
    /// - รวมทั้งจาก BENCH และจาก SPACE
    /// - ถ้าเป็น BLANK ที่ยัง “ไม่ resolve” → นับเป็นโควต้า blank
    /// - ถ้า BLANK ถูก resolve แล้ว → นับตามตัวอักษรที่เลือก (เช็คผ่าน LetterTile.CurrentLetter)
    /// </summary>
    Dictionary<char, int> GetAvailableLetterCounts(out int blankCount)
    {
        var cnt = new Dictionary<char, int>();
        int blanks = 0;

        // นับจาก BENCH
        if (BenchManager.Instance != null)
            blanks += CountAndTallyFromSlots(BenchManager.Instance.slotTransforms, cnt);

        // นับจาก SPACE
        if (SpaceManager.Instance != null)
            blanks += CountAndTallyFromSlots(SpaceManager.Instance.slotTransforms, cnt);

        blankCount = blanks;
        return cnt;
    }
    // เพิ่ม helper เมธอดนี้ (นอกเมธอดอื่น ๆ ในคลาส DictionaryUI)
    int CountAndTallyFromSlots(IEnumerable<Transform> slots, Dictionary<char, int> cnt)
    {
        if (slots == null) return 0;

        int blanks = 0;
        foreach (var slot in slots)
        {
            if (slot == null || slot.childCount == 0) continue;

            var tile = slot.GetChild(0).GetComponent<LetterTile>();
            if (tile == null) continue;

            // เคส BLANK
            if (tile.IsBlank)
            {
                if (tile.IsBlankResolved)
                {
                    var cl = tile.CurrentLetter;
                    if (!string.IsNullOrEmpty(cl))
                    {
                        char ch = char.ToUpperInvariant(cl[0]);
                        if (!cnt.ContainsKey(ch)) cnt[ch] = 0;
                        cnt[ch]++;
                    }
                }
                else
                {
                    blanks++; // ยังไม่ resolve → นับเป็นโควต้า blank
                }
                continue;
            }

            // ตัวอักษรปกติ
            var data = tile.GetData();
            if (data == null || string.IsNullOrEmpty(data.letter)) continue;

            char c = char.ToUpperInvariant(data.letter[0]);
            if (!cnt.ContainsKey(c)) cnt[c] = 0;
            cnt[c]++;
        }
        return blanks;
    }

    /// <summary>ตรวจว่า word นี้ประกอบได้จาก pool + โควต้า blank หรือไม่</summary>
    bool CanMake(string word, Dictionary<char, int> pool, int blankCount)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;

        var tmp = new Dictionary<char, int>(pool);
        int blanks = blankCount;

        foreach (char c in word.ToUpperInvariant())
        {
            if (tmp.TryGetValue(c, out int n) && n > 0)
            {
                tmp[c] = n - 1;      // ใช้ตัวตรง
            }
            else if (blanks > 0)
            {
                blanks--;            // ใช้ BLANK แทน
            }
            else
            {
                return false;        // ไม่มีพอ
            }
        }
        return true;
    }

    /* ---------- Rendering & Paging ---------- */

    void RenderPage()
    {
        ClearColumn(colWord);
        ClearColumn(colType);
        ClearColumn(colTrans);

        if (currentLen == 0 || viewWords.Count == 0)
        {
            if (pageLabel) pageLabel.text = "No results";
            if (btnPrev) btnPrev.interactable = false;
            if (btnNext) btnNext.interactable = false;
            return;
        }

        int totalPages = Mathf.CeilToInt(viewWords.Count / (float)PAGE_SIZE);
        pageIdx = Mathf.Clamp(pageIdx, 0, Math.Max(0, totalPages - 1));

        int start = pageIdx * PAGE_SIZE;
        int end = Mathf.Min(start + PAGE_SIZE, viewWords.Count);

        for (int i = start; i < end; i++)
        {
            string word = viewWords[i];

            // ปุ่มคำ (ซ้าย)
            var btn = Instantiate(prefabWordButton, colWord, false);
            var label = btn ? btn.GetComponentInChildren<TMP_Text>() : null;
            if (label) label.text = word;
            if (btn) btn.onClick.AddListener(() => OnWordButtonClicked(word));

            // ชนิด/คำแปล (คิวรีทีละคำ — PAGE_SIZE=10 จึงโอเค)
            string pos, th;
            if (WordChecker.Instance != null && WordChecker.Instance.TryGetInfo(word, out pos, out th))
            {
                if (prefabType) Instantiate(prefabType, colType, false).text = pos ?? "";
                if (prefabTrans) Instantiate(prefabTrans, colTrans, false).text = th ?? "";
            }
            else
            {
                if (prefabType) Instantiate(prefabType, colType, false).text = "";
                if (prefabTrans) Instantiate(prefabTrans, colTrans, false).text = "";
            }
        }

        if (btnPrev) btnPrev.interactable = pageIdx > 0;
        if (btnNext) btnNext.interactable = pageIdx < totalPages - 1;
        if (pageLabel) pageLabel.text = $"Len {currentLen} — Page {pageIdx + 1}/{Mathf.Max(1, totalPages)}";
    }

    void OnPrevPage()
    {
        if (pageIdx > 0) { pageIdx--; RenderPage(); }
    }

    void OnNextPage()
    {
        int totalPages = Mathf.CeilToInt(viewWords.Count / (float)PAGE_SIZE);
        if ((pageIdx + 1) < totalPages) { pageIdx++; RenderPage(); }
    }

    public void OnClear()
    {
        int blank;
        var avail = GetAvailableLetterCounts(out blank);
        int totalLetters = avail.Values.Sum() + blank;

        int startLen = Mathf.Clamp(Mathf.Max(2, totalLetters), 2, Math.Max(2, maxLenSelectable));
        ApplyLengthFilter(startLen, autoFallbackToShorter: true);
        RenderPage();
    }

    void ClearColumn(Transform parent)
    {
        if (!parent) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    /* ---------- Create-Word Action ---------- */

    /// <summary>
    /// ผู้ใช้คลิกคำ → เคลียร์ Space เดิม → ดึงตัวจาก Bench ไปไว้ที่ Space ตามคำที่เลือก
    /// - ถ้าไม่พอ: พยายามใช้ BLANK (และ resolve ให้เป็นตัวที่ต้องการ)
    /// </summary>
    void OnWordButtonClicked(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || SpaceManager.Instance == null) return;
        StartCoroutine(PlaceWordAnimated(word));
    }
    System.Collections.IEnumerator PlaceWordAnimated(string word)
    {
        // 0) เคลียร์ Space เดิมแบบเดิม (เร็วและชัวร์)
        foreach (var tile in SpaceManager.Instance.GetPreparedTiles().ToArray())
            SpaceManager.Instance.RemoveTile(tile);

        // 1) เตรียมช่องเป้าหมาย (ซ้าย→ขวา) ให้พอจำนวนอักษร
        var space = SpaceManager.Instance;
        var targets = new List<Transform>();
        foreach (var slot in space.slotTransforms)
            if (slot && slot.childCount == 0) targets.Add(slot);
        if (targets.Count == 0) yield break;

        // 2) หยุด Auto-refill กันถุงเติมระหว่างบิน
        BenchManager.Instance?.PauseAutoRefill();

        int needed = word.Length;
        int placed = 0;
        int targetIdx = 0;

        // 3) ลูปตามตัวอักษรของคำ
        foreach (char raw in word.ToUpperInvariant())
        {
            if (targetIdx >= targets.Count) break; // ช่อง Space ไม่พอ
            var targetSlot = targets[targetIdx];

            // ดึงรายการไทล์ที่มีใน Bench ตอนนี้
            var benchTiles = space.GetAllBenchTiles();
            if (benchTiles == null || benchTiles.Count == 0) break;

            // 3.1) หาตัวตรงก่อน (รวม BLANK ที่ resolve แล้ว)
            LetterTile found = benchTiles.Find(t =>
            {
                if (!t) return false;
                if (t.IsBlank && t.IsBlankResolved)
                    return string.Equals(t.CurrentLetter, raw.ToString(), System.StringComparison.OrdinalIgnoreCase);

                var d = t.GetData();
                return d != null && string.Equals(d.letter, raw.ToString(), System.StringComparison.OrdinalIgnoreCase);
            });

            // 3.2) ถ้าไม่เจอ ใช้ BLANK ที่ยังไม่ resolve แล้ว resolve ให้เป็นตัวนี้
            if (found == null)
            {
                found = benchTiles.Find(t => t && t.IsBlank && !t.IsBlankResolved);
                if (found != null) found.ResolveBlank(raw);
            }

            if (found == null) break; // ไม่มีตัวอักษรพอ

            // 3.3) ทำ Bench ให้ชิดเหมือนย้ายเอง: หา index ช่องเดิมแล้ว collapse
            int benchIdx = BenchManager.Instance != null
                ? BenchManager.Instance.IndexOfSlot(found.transform.parent)
                : -1;
            if (benchIdx >= 0) BenchManager.Instance.CollapseFrom(benchIdx);

            // 3.4) เล่นเสียงแล้วบินเข้า Space
            if (sfxOnTransfer) SfxPlayer.Play(SfxId.TileTransfer);
            found.FlyTo(targetSlot);
            placed++; targetIdx++;

            // 3.5) ดีเลย์ให้เห็นว่า “มาเป็นขบวนทีละตัว”
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, pickStagger));
        }

        // 4) จบงาน → ปล่อยให้ Bench เติมได้แล้ว
        BenchManager.Instance?.ResumeAutoRefill();

        // 5) แจ้งผล + ให้ระบบพยายามวางลงบอร์ดถ้ามี
        if (placed == needed)
            UIManager.Instance?.ShowMessageDictionary("Done!");
        else
            UIManager.Instance?.ShowMessageDictionary("Letter Not Enough!");

        PlacementManager.Instance?.TryPlace();
    }
}
