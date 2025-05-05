using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WordChecker : MonoBehaviour
{
    public static WordChecker Instance { get; private set; }

    [Header("Dictionary (json)")]
    public TextAsset wordJson;          // ลาก wordlist_20k_with_translation.json

    /* ---------- โครงสร้างข้อมูล ---------- */
    [Serializable] public class Entry
    {
        public string Word;
        public string Type;
        public string Translation;
    }

    /* ---------- เก็บข้อมูล ---------- */
    readonly HashSet<string> dict = new();     // ไว้เช็กเร็ว ๆ
    List<Entry> entries = new();               // ไว้โชว์ Dictionary

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        LoadEntries();
    }

    /* ---------- LOAD ---------- */
    void LoadEntries()
    {
        if (wordJson == null)
        {
            Debug.LogError("[WordChecker] Assign wordJson"); return;
        }

        string txt = wordJson.text.TrimStart();

        // -----------------------------------------------------------------
        // เคส A) Json = [{ "Word":"that", "Type":"adj", "Translation":"" }, ...]
        // -----------------------------------------------------------------
        if (txt.StartsWith("[") && txt.Contains("\"Word\""))
        {
            string json = "{\"items\":" + wordJson.text + "}";
            var wrap = JsonUtility.FromJson<ObjWrapper>(json);

            foreach (var it in wrap.items)
            {
                if (string.IsNullOrEmpty(it.Word)) continue;

                var e = new Entry
                {
                    Word        = it.Word.ToUpper(),
                    Type        = it.Type,
                    Translation = it.Translation
                };
                entries.Add(e);
                dict.Add(e.Word);
            }
        }
        // -----------------------------------------------------------------
        // เคส B) Json = ["APPLE", "DOG", ...]  (ไม่มีฟิลด์ Type/Trans)
        // -----------------------------------------------------------------
        else if (txt.StartsWith("["))
        {
            string json = "{\"arr\":" + wordJson.text + "}";
            var wrap = JsonUtility.FromJson<StrWrapper>(json);

            foreach (string w in wrap.arr.Where(s => !string.IsNullOrEmpty(s)))
            {
                var up = w.ToUpper();
                entries.Add(new Entry { Word = up, Type = "", Translation = "" });
                dict.Add(up);
            }
        }
        // -----------------------------------------------------------------
        // เคส C) .txt / .json บรรทัดละคำ
        // -----------------------------------------------------------------
        else
        {
            foreach (string ln in wordJson.text.Split('\n', '\r'))
            {
                string w = ln.Trim();
                if (w == "") continue;

                var up = w.ToUpper();
                entries.Add(new Entry { Word = up, Type = "", Translation = "" });
                dict.Add(up);
            }
        }

        // เรียงตามตัวอักษร A‑Z
        entries = entries.OrderBy(e => e.Word).ToList();

        Debug.Log($"[WordChecker] loaded {entries.Count} words");
    }

    /* ---------- API ---------- */
    public bool IsWordValid(string w) => dict.Contains(w.ToUpper());

    /// <summary>คืนลิสต์คำ (Word / Type / Translation) เรียง A‑Z สำหรับ DictionaryUI</summary>
    public List<Entry> GetAllEntries() => entries;

    /// <summary>คืนแค่รายการคำ (string) ตามที่โค้ดเก่าของคุณใช้</summary>
    public List<string> GetAllWordsSorted() => entries.Select(e => e.Word).ToList();

    /* ---------- helper wrapper ---------- */
    [Serializable] private class StrWrapper { public string[] arr; }
    [Serializable] private class ObjWrapper { public RawItem[] items; }
    [Serializable] private class RawItem   // ตรงกับฟิลด์ในไฟล์ json ที่คุณใช้
    {
        public string Word;
        public string Type;
        public string Translation;
    }
}
