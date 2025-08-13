using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

public class FreeWordManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI freeWordText;
    [SerializeField] private float suggestionInterval = 1f;
    [SerializeField] private int maxWordLength = 7;
    private string currentFreeWord;
    private bool freeWordPending;
    private float timer;

    void Start()
    {
        if (freeWordText != null)
            freeWordText.enabled = false;

        TurnManager.Instance.confirmBtn.onClick.AddListener(OnConfirm);

        // เรียก SuggestionRoutine เพียงครั้งเดียว ให้มันวิ่งวนตลอดเกม
        StartCoroutine(SuggestionRoutine());
    }
    private void StartSuggestionTimer()
    {
        StopAllCoroutines();
        StartCoroutine(SuggestionRoutine());
    }

    IEnumerator SuggestionRoutine()
    {
        // วนลูปไม่รู้จบ ตรวจเสมอว่า freeWordPending ยังเป็น false อยู่หรือเปล่า
        while (true)
        {
            if (!freeWordPending)
            {
                // รอจนกว่าจะถึงช่วงเวลาที่กำหนด
                float t = suggestionInterval;
                while (t > 0f)
                {
                    t -= Time.deltaTime;
                    yield return null;
                }

                // ตรวจสอบทุกอย่างเหมือนเดิม
                if (SpaceManager.Instance == null || WordChecker.Instance == null || freeWordText == null)
                {
                    Debug.LogError("[FreeWordManager] Missing dependencies!");
                    yield return null;
                    continue;
                }

                // สุ่มคำใหม่
                currentFreeWord = GetRandomBenchWord();
                if (!string.IsNullOrEmpty(currentFreeWord))
                {
                    freeWordText.text = $"Free Word is {currentFreeWord}";
                    freeWordText.enabled = true;
                    freeWordPending = true;
                    Debug.Log($"[FreeWordManager] Suggested free word: {currentFreeWord}");
                }
            }

            // รอก่อนเช็ครอบถัดไป สักเฟรม
            yield return null;
        }
    }


    public void OnConfirm()
    {
        if (freeWordText != null)
            freeWordText.enabled = false;

        // ทำให้โครูทีนรู้ว่าเราพร้อมสุ่มคำใหม่แล้ว
        freeWordPending = false;
    }

    string GetRandomBenchWord()
    {
        var tiles = SpaceManager.Instance.GetAllBenchTiles();
        if (tiles == null)
        {
            Debug.LogError("[FreeWordManager] GetAllBenchTiles() returned NULL!");
            return string.Empty;
        }

        var letters = tiles
            .Select(t => t.GetData().letter.ToString().ToUpper())
            .ToList();

        var allWords = WordChecker.Instance.GetAllWordsSorted();
        if (allWords == null)
        {
            Debug.LogError("[FreeWordManager] GetAllWordsSorted() returned NULL!");
            return string.Empty;
        }

        int length = UnityEngine.Random.Range(1, maxWordLength + 1);
        var candidates = allWords
            .Where(w => w.Length == length)
            .Where(w => CanBuild(w.ToUpper(), letters))
            .ToList();

        if (candidates.Count > 0)
        {
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            return candidates[idx];
        }

        return string.Empty;
    }

    bool CanBuild(string word, System.Collections.Generic.List<string> letters)
    {
        var temp = new System.Collections.Generic.List<string>(letters);
        foreach (char c in word)
        {
            string s = c.ToString();
            if (!temp.Remove(s))
                return false;
        }
        return true;
    }
}
