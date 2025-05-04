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
    [SerializeField]  Transform colWord;      // คอลัมน์ซ้าย
    [SerializeField]  Transform colType;      // คอลัมน์กลาง
    [SerializeField]  Transform colTrans;     // คอลัมน์ขวา

    [Header("Text Prefabs")]
    [SerializeField]  TMP_Text prefabWord;    // ใช้ฟอนต์/สีต่างกันได้
    [SerializeField]  TMP_Text prefabType;
    [SerializeField]  TMP_Text prefabTrans;

    [Header("Navigation")]
    [SerializeField]  Button btnPrev;
    [SerializeField]  Button btnNext;

    [Header("Create‑Word Buttons (1‑10)")]
    [SerializeField]  Button[] btnLen;        // index 0 = 1, …, 9 = 10

    /* ---------- Data ---------- */
    const int PAGE_SIZE = 10;
    int  pageIdx = 0;

    List<WordChecker.Entry> allEntries  = new();   // จาก WordChecker
    List<WordChecker.Entry> viewEntries = new();   // หลังกรอง

    /* ---------- Awake ---------- */
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        allEntries  = WordChecker.Instance.GetAllEntries();
        viewEntries = new List<WordChecker.Entry>(allEntries);

        BindButtons();
        panel.SetActive(false);
    }

    /* ---------- Public API ---------- */
    public void Open()
    {
        TurnManager.Instance.SetDictionaryUsed();   // โดนลดคะแนน 50 %
        panel.SetActive(true);
        ResetFilter();
        RenderPage();
    }
    public void Close() => panel.SetActive(false);

    /* ---------- Buttons ---------- */
    void BindButtons()
    {
        btnPrev.onClick.AddListener(() => { if (pageIdx > 0) { pageIdx--; RenderPage(); } });
        btnNext.onClick.AddListener(() =>
        {
            if ((pageIdx + 1) * PAGE_SIZE < viewEntries.Count) { pageIdx++; RenderPage(); }
        });

        for (int i = 0; i < btnLen.Length; i++)
        {
            int len = i + 1;
            btnLen[i].onClick.RemoveAllListeners();
            btnLen[i].onClick.AddListener(() => FilterMakeableWords(len));
        }
    }

    /* ---------- Filtering ---------- */
    void ResetFilter()
    {
        viewEntries = new List<WordChecker.Entry>(allEntries);
        pageIdx = 0;
    }

    void FilterMakeableWords(int len)
    {
        var benchCnt = GetBenchLetterCounts();
        viewEntries = allEntries
            .Where(e => e.Word.Length == len && CanMake(e.Word, benchCnt))
            .ToList();
        pageIdx = 0;
        RenderPage();
    }

    Dictionary<char, int> GetBenchLetterCounts()
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

    bool CanMake(string word, Dictionary<char, int> benchCnt)
    {
        Dictionary<char, int> tmp = new(benchCnt);
        foreach (char c in word.ToUpper())
        {
            if (!tmp.ContainsKey(c) || tmp[c] == 0) return false;
            tmp[c]--;
        }
        return true;
    }

    /* ---------- Render ---------- */
    void RenderPage()
    {
        ClearColumn(colWord);
        ClearColumn(colType);
        ClearColumn(colTrans);

        int start = pageIdx * PAGE_SIZE;
        for (int i = 0; i < PAGE_SIZE; i++)
        {
            int idx = start + i;
            if (idx >= viewEntries.Count) break;

            var e = viewEntries[idx];

            Instantiate(prefabWord , colWord ,  false).text = e.Word;
            Instantiate(prefabType , colType ,  false).text = e.Type;
            Instantiate(prefabTrans, colTrans, false).text = e.Translation;
        }

        btnPrev.interactable = pageIdx > 0;
        btnNext.interactable = (pageIdx + 1) * PAGE_SIZE < viewEntries.Count;
    }

    void ClearColumn(Transform parent)
    {
        foreach (Transform t in parent) Destroy(t.gameObject);
    }
}
