using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// แสดง Dictionary Popup, paging 10 คำ (5 คำ/คอลัมน์) + สร้างคำจาก Bench
/// </summary>
public class DictionaryUI : MonoBehaviour
{
    public static DictionaryUI Instance { get; private set; }

    [Header("Root & Pages")]
    [SerializeField] private GameObject panel;          // Root Popup
    [SerializeField] private Transform leftPageParent;  // คอลัมน์ซ้าย (5 Text)
    [SerializeField] private Transform rightPageParent; // คอลัมน์ขวา (5 Text)
    [SerializeField] private TMP_Text wordItemPrefab;   // Prefab <TextMeshPro>

    [Header("Navigation Buttons")]                     // Next / Prev
    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;

    [Header("Create‑Word Buttons (1‑10)")]
    [SerializeField] private Button[] btnLen;           // ขนาดเท่ากับ 10, index=0→1 … 9→10

    private List<string> allWords  = new();    // ดึงจาก WordChecker
    private readonly List<string> viewWords = new();    // list ปัจจุบัน (หลัง filter)

    private int pageIdx = 0;        // เริ่มหน้า 0
    private const int PAGE_SIZE = 10;

private void Awake()
{
    if (Instance == null) Instance = this; else { Destroy(gameObject); return; }

    panel.SetActive(false);          // ซ่อน popup
    BindButtons();                   // รวมโค้ดผูกปุ่ม Prev/Next + Len[1‑10]
}

private void Start()
{
    allWords = WordChecker.Instance.GetAllWordsSorted();
    viewWords.AddRange(allWords);
}

    /* ================= PUBLIC API ================= */
    public void Open()
    {
        // เรียกจากปุ่ม Confirm ของ Popup Dictionary penalty
        TurnManager.Instance.SetDictionaryUsed();   // ลดคะแนน 50% ที่ตานี้

        ResetFilter();
        RenderPage();
        panel.SetActive(true);
    }

    public void Close() => panel.SetActive(false);

    /* =============== Navigation =============== */
    private void OnPrev() { if (pageIdx > 0) { pageIdx--; RenderPage(); } }
    private void OnNext() { if ((pageIdx + 1) * PAGE_SIZE < viewWords.Count) { pageIdx++; RenderPage(); } }

    /* =============== Filter Logic =============== */
    private void ResetFilter()
    {
        viewWords.Clear();
        viewWords.AddRange(allWords);
        pageIdx = 0;
    }
    private void BindButtons()
    {
        // Prev / Next
        btnPrev.onClick.AddListener(OnPrev);
        btnNext.onClick.AddListener(OnNext);

        // ปุ่มสร้างคำ 1‑10
        for (int i = 0; i < btnLen.Length; i++)
        {
            int len = i + 1;
            btnLen[i].onClick.RemoveAllListeners();   // กันซ้อน
            btnLen[i].onClick.AddListener(() => FilterMakeableWords(len));
        }
    }

    private void FilterMakeableWords(int length)
    {
        // นับตัวอักษรบน Bench
        Dictionary<char, int> benchCnt = GetBenchLetterCounts();

        viewWords.Clear();
        foreach (var w in allWords)
        {
            if (w.Length != length) continue;
            if (CanMake(w, benchCnt)) viewWords.Add(w);
        }
        pageIdx = 0;
        RenderPage();
    }

    private Dictionary<char, int> GetBenchLetterCounts()
    {
        Dictionary<char, int> cnt = new();
        foreach (Transform slot in BenchManager.Instance.slotTransforms)
        {
            if (slot.childCount == 0) continue;
            var tile = slot.GetChild(0).GetComponent<LetterTile>();
            char ch = tile.GetData().letter.ToUpper()[0];
            if (!cnt.ContainsKey(ch)) cnt[ch] = 0;
            cnt[ch]++;
        }
        return cnt;
    }

    private bool CanMake(string word, Dictionary<char, int> benchCnt)
    {
        Dictionary<char, int> tmp = new(benchCnt);
        foreach (char c in word.ToUpper())
        {
            if (!tmp.ContainsKey(c) || tmp[c] == 0) return false;
            tmp[c]--;
        }
        return true;
    }

    /* =============== Render =============== */
    private void RenderPage()
    {
        // ล้างคอลัมน์ก่อน
        foreach (Transform t in leftPageParent)  Destroy(t.gameObject);
        foreach (Transform t in rightPageParent) Destroy(t.gameObject);

        int start = pageIdx * PAGE_SIZE;
        for (int i = 0; i < PAGE_SIZE; i++)
        {
            int idx = start + i;
            if (idx >= viewWords.Count) break;
            string w = viewWords[idx];

            Transform parent = i < 5 ? leftPageParent : rightPageParent;
            var txt = Instantiate(wordItemPrefab, parent, false);
            txt.text = w;
        }

        // ปุ่ม Prev/Next enable/disable
        btnPrev.interactable = pageIdx > 0;
        btnNext.interactable = (pageIdx + 1) * PAGE_SIZE < viewWords.Count;
    }
}
