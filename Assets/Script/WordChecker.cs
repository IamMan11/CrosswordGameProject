using System.Collections.Generic;
using UnityEngine;

public class WordChecker : MonoBehaviour
{
    public static WordChecker Instance { get; private set; }

    [Header("Dictionary (json)")]
    public TextAsset wordJson;     // ลากไฟล์ wordlist.json มาวางตรงนี้ใน Inspector

    private readonly HashSet<string> dict = new();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        LoadDict();
    }

    // ---------- LOAD ----------
void LoadDict()
{
    if (wordJson == null) { Debug.LogError("[WordChecker] assign wordJson"); return; }

    string txt = wordJson.text.TrimStart();

    // --------------------------------------------
    //  A) JSON = array of objects  [{ "Word": "...", ... }, ... ]
    // --------------------------------------------
    if (txt.StartsWith("[") && txt.Contains("\"Word\""))
    {
        // ทำ wrapper ให้ JsonUtility อ่านได้
        string json = "{\"items\":" + wordJson.text + "}";
        ObjWrapper wrap = JsonUtility.FromJson<ObjWrapper>(json);

        foreach (var item in wrap.items)
            if (!string.IsNullOrEmpty(item.Word))
                dict.Add(item.Word.ToUpper());
    }
    // --------------------------------------------
    //  B) JSON = array of plain strings  ["APPLE", ...]
    // --------------------------------------------
    else if (txt.StartsWith("["))
    {
        string json = "{\"arr\":" + wordJson.text + "}";
        StrWrapper w = JsonUtility.FromJson<StrWrapper>(json);
        foreach (string s in w.arr) dict.Add(s.ToUpper());
    }
    // --------------------------------------------
    //  C) ไฟล์ .txt / .json แบบบรรทัดละคำ
    // --------------------------------------------
    else
    {
        foreach (string ln in wordJson.text.Split('\n', '\r'))
        {
            string w = ln.Trim();
            if (w != "") dict.Add(w.ToUpper());
        }
    }

    Debug.Log($"[WordChecker] loaded {dict.Count} words");
}

[System.Serializable] private class StrWrapper { public string[] arr; }
[System.Serializable] private class ObjWrapper { public WordItem[] items; }
[System.Serializable] private class WordItem  { public string Word; }
    // ---------- API ----------
    public bool IsWordValid(string w) => dict.Contains(w.ToUpper());
}
