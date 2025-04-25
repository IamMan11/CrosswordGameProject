using System.Collections.Generic;
using UnityEngine;

/// <summary>โหลด word.json แล้วให้เมธอด IsValid()</summary>
public class WordChecker : MonoBehaviour
{
    public static WordChecker Instance { get; private set; }

    [Header("Dictionary (.json)")]
    public TextAsset wordJson;          // ลาก word.json ลง Inspector

    private HashSet<string> dict = new();

    [System.Serializable]            // รูปข้อมูลในไฟล์
    private class WordRecord { public string Word; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (wordJson == null) { Debug.LogError("WordChecker: No json!"); return; }

        WordRecord[] records = JsonUtility.FromJson<Wrapper>( $"{{\"arr\":{wordJson.text}}}" ).arr;
        foreach (var rec in records) dict.Add(rec.Word.ToLower());

        Debug.Log($"[WordChecker] Loaded {dict.Count} words");
    }
    [System.Serializable] class Wrapper { public WordRecord[] arr; }

    public bool IsValid(string w) => dict.Contains(w.ToLower());
}
