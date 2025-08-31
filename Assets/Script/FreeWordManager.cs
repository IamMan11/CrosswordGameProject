using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class FreeWordManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI freeWordText;

    [Header("Suggestion Settings")]
    [SerializeField, Tooltip("เวลาห่างระหว่างการสุ่มคำ (วินาที)")]
    private float suggestionInterval = 1f;

    [SerializeField, Tooltip("ความยาวคำสูงสุดที่จะสุ่ม")]
    private int maxWordLength = 7;

    [SerializeField, Tooltip("จำนวนคำที่ดึงจาก DB ต่อความยาว (ยิ่งมากยิ่งมีโอกาสเจอ แต่จะช้าลง)")]
    private int perLengthFetchLimit = 8000;

    [SerializeField, Tooltip("จำนวนครั้งสูงสุดที่พยายามสุ่มคำจากพูลต่อความยาว")]
    private int perLengthTry = 120;

    [SerializeField, Tooltip("ห้ามแนะนำคำซ้ำกับครั้งก่อนหน้าถ้าเป็นไปได้")]
    private bool avoidRepeatSuggestion = true;

    private string currentFreeWord;
    private string lastSuggestedWord;
    private bool freeWordPending;

    void Start()
    {
        if (freeWordText != null)
            freeWordText.enabled = false;

        // ผูกกับปุ่มยืนยันคำ
        TurnManager.Instance.confirmBtn.onClick.AddListener(OnConfirm);

        // เริ่มโครูทีนสุ่มคำ (วนตลอดเกม)
        StartCoroutine(SuggestionRoutine());
    }

    IEnumerator SuggestionRoutine()
    {
        // รอจน WordChecker พร้อมจริง ๆ
        while (WordChecker.Instance == null || !WordChecker.Instance.IsReady())
        {
            yield return null;
        }

        // วนลูปไม่รู้จบ
        while (true)
        {
            // รอครบช่วงเวลา และต้องยังไม่มี free word แขวนค้างอยู่
            if (!freeWordPending)
            {
                float t = suggestionInterval;
                while (t > 0f)
                {
                    t -= Time.deltaTime;
                    yield return null;
                }

                if (SpaceManager.Instance == null || freeWordText == null)
                {
                    Debug.LogError("[FreeWordManager] Missing dependencies!");
                    yield return null;
                    continue;
                }

                // พยายามสุ่มคำจากเบนช์
                currentFreeWord = GetRandomBenchWord();
                if (!string.IsNullOrEmpty(currentFreeWord))
                {
                    if (avoidRepeatSuggestion && string.Equals(currentFreeWord, lastSuggestedWord, StringComparison.OrdinalIgnoreCase))
                    {
                        // ถ้าเผลอสุ่มได้เหมือนคำก่อนหน้า ลองสุ่มอีกรอบ (อย่างรวดเร็ว 1 ครั้ง)
                        var retry = GetRandomBenchWord();
                        if (!string.IsNullOrEmpty(retry))
                            currentFreeWord = retry;
                    }

                    lastSuggestedWord = currentFreeWord;
                    freeWordText.text = $"Free Word is {currentFreeWord}";
                    freeWordText.enabled = true;
                    freeWordPending = true;
                    Debug.Log($"[FreeWordManager] Suggested free word: {currentFreeWord}");
                }
                else
                {
                    // ไม่เจอคำที่ประกอบได้จากอักษรที่มี — เว้นวรรคแล้วลองใหม่รอบหน้า
                    freeWordText.enabled = false;
                    freeWordPending = false;
                }
            }

            yield return null;
        }
    }

    public void OnConfirm()
    {
        if (freeWordText != null)
            freeWordText.enabled = false;

        // พร้อมสุ่มคำใหม่ในรอบถัดไป
        freeWordPending = false;
    }

    /// <summary>
    /// สุ่มคำที่ "ประกอบได้จริง" จากตัวอักษรที่อยู่บน Bench
    /// โดยไม่ต้องโหลดดิกทั้งหมด: ดึงจากฐานตามความยาว แล้วค่อยกรองด้วยถุงตัวอักษร
    /// </summary>
    string GetRandomBenchWord()
    {
        var tiles = SpaceManager.Instance.GetAllBenchTiles();
        if (tiles == null)
        {
            Debug.LogError("[FreeWordManager] GetAllBenchTiles() returned NULL!");
            return string.Empty;
        }

        // สร้างถุงตัวอักษร (นับจำนวนแบบเคสใหญ่)
        var letterBag = new Dictionary<char, int>();
        foreach (var t in tiles)
        {
            char ch = char.ToUpperInvariant(t.GetData().letter);
            if (!letterBag.ContainsKey(ch)) letterBag[ch] = 1;
            else letterBag[ch]++;
        }

        // ไม่มีตัวอักษรเลย
        if (letterBag.Count == 0) return string.Empty;

        // ความยาวที่เป็นไปได้: 2..min(maxWordLength, lettersCount)
        int availableLetters = letterBag.Values.Sum();
        int maxLen = Mathf.Clamp(maxWordLength, 1, Mathf.Max(1, availableLetters));
        var lengths = Enumerable.Range(2, Math.Max(0, maxLen - 1)) // เริ่มที่ 2 ตัวอักษร
                                .OrderBy(_ => UnityEngine.Random.value) // สุ่มลำดับความยาว
                                .ToList();

        // ลองทีละความยาว (แบบสุ่มลำดับ) จนกว่าจะเจอคำ
        foreach (int len in lengths)
        {
            // ดึงพูลคำจากฐานข้อมูลตามความยาว (จำกัดจำนวนเพื่อลดงานกรอง)
            var pool = WordChecker.Instance.GetWordsByLength(len, perLengthFetchLimit);
            if (pool == null || pool.Count == 0) continue;

            // สุ่มหยิบจากพูลมาทดสอบจำนวนหนึ่ง (หลีกเลี่ยงวนเช็คทั้งพูล)
            int tries = Mathf.Min(perLengthTry, pool.Count);

            for (int i = 0; i < tries; i++)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                string w = pool[idx];
                if (string.IsNullOrWhiteSpace(w)) continue;

                if (CanBuildFromBag(w, letterBag))
                {
                    return w;
                }
            }

            // ถ้ายังไม่เจอ ลองสแกนเร็ว ๆ เพิ่มอีกนิดโดยไม่สุ่ม (เพื่อไม่พลาดคำง่าย)
            foreach (var w in pool)
            {
                if (CanBuildFromBag(w, letterBag))
                    return w;
            }
        }

        // ไม่พบคำใด ๆ ที่ประกอบได้
        return string.Empty;
    }

    /// <summary>
    /// ตรวจว่า word (ไม่แคสตัวพิมพ์) สามารถประกอบได้จากถุงตัวอักษร letterBag
    /// </summary>
    bool CanBuildFromBag(string word, Dictionary<char, int> letterBag)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        // ทำงานบนสำเนาเพื่อไม่กระทบของเดิม
        _tempBag.Clear();
        foreach (var kv in letterBag) _tempBag[kv.Key] = kv.Value;

        for (int i = 0; i < word.Length; i++)
        {
            char ch = char.ToUpperInvariant(word[i]);
            if (!_tempBag.TryGetValue(ch, out int cnt) || cnt <= 0)
                return false;
            _tempBag[ch] = cnt - 1;
        }
        return true;
    }

    // ถุงชั่วคราวสำหรับ CanBuildFromBag เพื่อลด alloc
    private readonly Dictionary<char, int> _tempBag = new Dictionary<char, int>();
}