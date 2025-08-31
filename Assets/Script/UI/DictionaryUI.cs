using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DictionaryUI : MonoBehaviour
{
    public static DictionaryUI Instance { get; private set; }

    /* ---------- Parents / Prefabs ---------- */
    [Header("Root & Panel")]
    [SerializeField]  GameObject panel;

    [Header("Columns (Vertical Layout)")]
    [SerializeField]  Transform colWord;      // คอลัมน์ซ้าย (ปุ่มคำ)
    [SerializeField]  Transform colType;      // คอลัมน์กลาง (ชนิดคำ)
    [SerializeField]  Transform colTrans;     // คอลัมน์ขวา  (คำแปล)

    [Header("Text/Btn Prefabs")]
    [SerializeField]  Button   prefabWordButton;
    [SerializeField]  TMP_Text prefabType;
    [SerializeField]  TMP_Text prefabTrans;

    [Header("Navigation")]
    [SerializeField]  Button   btnPrev;
    [SerializeField]  Button   btnNext;
    [SerializeField]  Button   btnClear;
    [SerializeField]  TMP_Text pageLabel;    // "Page X / Y"

    [Header("Create-Word Buttons (1-10)")]
    [SerializeField]  Button[] btnLen;        // index 0 = 1, …, 9 = 10

    [Header("Query Settings")]
    [SerializeField, Tooltip("ดึงคำจากฐานต่อความยาวสูงสุดกี่คำ ก่อนคัดกรองกับตัวอักษรบน Bench")]
    private int fetchLimitPerLen = 8000;

    [SerializeField, Tooltip("ความยาวคำสูงสุดที่อนุญาตให้กดเลือกได้")]
    private int maxLenSelectable = 10;

    /* ---------- State ---------- */
    const int PAGE_SIZE = 10;
    int  pageIdx = 0;
    int  currentLen = 0;                // ความยาวที่กำลังแสดง (0 = ยังไม่เลือก)
    readonly List<string> viewWords = new(); // รายการคำที่ “ประกอบได้จริง” หลังกรองแล้ว

    /* ---------- Awake ---------- */
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        BindButtons();
        if (panel) panel.SetActive(false);
    }

    /* ---------- Public API ---------- */
    public void Open()
    {
        // แจ้ง TurnManager ว่าผู้เล่นเปิดดิก (จะถูกลดคะแนนในเทิร์นนี้ ถ้าไม่ได้ FreePass)
        TurnManager.Instance.SetDictionaryUsed();

        if (panel) panel.SetActive(true);

        // เลือกความยาวเริ่มต้นให้ฉลาด: min(จำนวนตัวอักษรบน Bench, maxLenSelectable) แต่ไม่น้อยกว่า 2
        int benchLetters = GetBenchLetterCounts().Values.Sum();
        int startLen = Mathf.Clamp(Mathf.Max(2, benchLetters), 2, Math.Max(2, maxLenSelectable));

        ApplyLengthFilter(startLen, autoFallbackToShorter:true);
        RenderPage();
    }

    public void Close() { if (panel) panel.SetActive(false); }

    /* ---------- Buttons ---------- */
    void BindButtons()
    {
        if (btnPrev) btnPrev.onClick.AddListener(OnPrevPage);
        if (btnNext) btnNext.onClick.AddListener(OnNextPage);
        if (btnClear) btnClear.onClick.AddListener(OnClear);

        // map ปุ่มยาว 1..N เข้าฟังก์ชันกรองตามความยาว
        for (int i = 0; i < btnLen.Length; i++)
        {
            int len = i + 1;
            if (len > maxLenSelectable) { btnLen[i].gameObject.SetActive(false); continue; }

            btnLen[i].onClick.RemoveAllListeners();
            btnLen[i].onClick.AddListener(() => {
                ApplyLengthFilter(len, autoFallbackToShorter:false);
                RenderPage();
            });
        }
    }

    /* ---------- Filtering / Fetching ---------- */

    /// <summary>
    /// ดึงคำจากฐานตามความยาว len แล้วกรองให้ “ประกอบได้จริง” จากตัวอักษรบน Bench
    /// ถ้าไม่เจอคำเลยและ autoFallbackToShorter=true จะลองลดความยาวลงทีละ 1 จนถึง 2
    /// </summary>
    void ApplyLengthFilter(int len, bool autoFallbackToShorter)
    {
        if (WordChecker.Instance == null || !WordChecker.Instance.IsReady())
        {
            Debug.LogWarning("[DictionaryUI] WordChecker not ready.");
            return;
        }

        var benchCnt = GetBenchLetterCounts();

        // ถ้า len มากกว่าจำนวนตัวอักษรในมือ ให้หดลงมา
        int capLen = Mathf.Min(len, Math.Max(0, benchCnt.Values.Sum()));

        // เริ่มลองที่ capLen แล้วลดลง (ถ้าอนุญาต)
        int tryLen = Mathf.Clamp(capLen, 1, maxLenSelectable);
        bool found = false;

        while (tryLen >= 2) // ไม่แนะนำคำยาว 1 โดยทั่วไป
        {
            var pool = WordChecker.Instance.GetWordsByLength(tryLen, fetchLimitPerLen) ?? new List<string>();
            viewWords.Clear();

            // กรองให้ประกอบได้จริง
            foreach (var w in pool)
                if (CanMake(w, benchCnt)) viewWords.Add(w);

            if (viewWords.Count > 0)
            {
                currentLen = tryLen;
                found = true;
                break;
            }

            if (!autoFallbackToShorter) break;
            tryLen--;
        }

        if (!found)
        {
            currentLen = 0;
            viewWords.Clear();
        }

        pageIdx = 0;
    }

    Dictionary<char, int> GetBenchLetterCounts()
    {
        var cnt = new Dictionary<char, int>();
        foreach (Transform slot in BenchManager.Instance.slotTransforms)
        {
            if (slot.childCount == 0) continue;
            var tile = slot.GetChild(0).GetComponent<LetterTile>();
            char ch = char.ToUpperInvariant(tile.GetData().letter[0]);
            if (!cnt.ContainsKey(ch)) cnt[ch] = 0;
            cnt[ch]++;
        }
        return cnt;
    }

    bool CanMake(string word, Dictionary<char, int> benchCnt)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;

        // ทำงานบนสำเนาเพื่อลด GC (ใช้ temp dict ภายใน method จะอ่านง่าย/ปลอดภัย)
        var tmp = new Dictionary<char, int>(benchCnt);
        foreach (char c in word.ToUpperInvariant())
        {
            if (!tmp.TryGetValue(c, out int n) || n <= 0) return false;
            tmp[c] = n - 1;
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
            pageLabel.text = "No results";
            if (btnPrev) btnPrev.interactable = false;
            if (btnNext) btnNext.interactable = false;
            return;
        }

        int totalPages = Mathf.CeilToInt(viewWords.Count / (float)PAGE_SIZE);
        pageIdx = Mathf.Clamp(pageIdx, 0, Math.Max(0, totalPages - 1));

        int start = pageIdx * PAGE_SIZE;
        int end   = Mathf.Min(start + PAGE_SIZE, viewWords.Count);

        for (int i = start; i < end; i++)
        {
            string word = viewWords[i];

            // ปุ่มคำ (ซ้าย)
            var btn = Instantiate(prefabWordButton, colWord, false);
            btn.GetComponentInChildren<TMP_Text>().text = word;
            btn.onClick.AddListener(() => OnWordButtonClicked(word));

            // ชนิด/คำแปล (คิวรีทีละคำ — PAGE_SIZE=10 จึงโอเค)
            string pos, th;
            if (WordChecker.Instance.TryGetInfo(word, out pos, out th))
            {
                Instantiate(prefabType , colType , false).text = pos    ?? "";
                Instantiate(prefabTrans, colTrans, false).text = th     ?? "";
            }
            else
            {
                Instantiate(prefabType , colType , false).text = "";
                Instantiate(prefabTrans, colTrans, false).text = "";
            }
        }

        if (btnPrev) btnPrev.interactable = pageIdx > 0;
        if (btnNext) btnNext.interactable = pageIdx < totalPages - 1;

        pageLabel.text = $"Len {currentLen} — Page {pageIdx + 1}/{Mathf.Max(1,totalPages)}";
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
        // รีเฟรชด้วยความยาวที่เหมาะกับตัวอักษร ณ ตอนนี้
        int benchLetters = GetBenchLetterCounts().Values.Sum();
        int startLen = Mathf.Clamp(Mathf.Max(2, benchLetters), 2, Math.Max(2, maxLenSelectable));

        ApplyLengthFilter(startLen, autoFallbackToShorter:true);
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
    /// ผู้ใช้คลิกคำ → นำไทล์จาก Bench ไปเตรียมวาง (รองรับ BLANK ด้วย)
    /// </summary>
    void OnWordButtonClicked(string word)
    {
        // เคลียร์ของเดิม
        foreach (var tile in SpaceManager.Instance.GetPreparedTiles().ToArray())
            SpaceManager.Instance.RemoveTile(tile);

        int needed = word.Length;
        int placed = 0;

        foreach (char ch in word.ToUpperInvariant())
        {
            var benchTiles = SpaceManager.Instance.GetAllBenchTiles();

            // 1) หาตัวตรงก่อน
            LetterTile found = benchTiles.Find(t => string.Equals(
                t.GetData().letter, ch.ToString(), StringComparison.OrdinalIgnoreCase));

            // 2) ถ้าไม่เจอ — ใช้ BLANK
            if (found == null)
            {
                found = benchTiles.Find(t => t.GetData().letter.Equals("Blank", StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    var data = found.GetData();
                    data.letter = ch.ToString();
                    data.score  = 0;

                    // อัปเดต UI ของ Tile
                    found.letterText.text = data.letter;
                    found.scoreText .text = "0";

                    // อัปเดตสไปรต์ตามตัวอักษรเป้าหมาย (ถ้ามี)
                    var lc = TileBag.Instance.initialLetters
                        .Find(x => x.data.letter.Equals(data.letter, StringComparison.OrdinalIgnoreCase));
                    if (lc != null && found.icon != null)
                    {
                        data.sprite       = lc.data.sprite;
                        found.icon.sprite = data.sprite;

                        var rt = found.icon.GetComponent<RectTransform>();
                        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                    }
                }
            }

            if (found != null)
            {
                SpaceManager.Instance.AddTile(found);
                placed++;
            }
        }

        // แจ้งผล
        if (placed == needed)
            UIManager.Instance.ShowMessageDictionary("Done!");
        else
            UIManager.Instance.ShowMessageDictionary("Letter Not Enough!");

        // พรีวิวการวางลงกระดาน
        PlacementManager.Instance.TryPlace();
    }
}
