using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using SQLite4Unity3d;

public class LetterDatabaseLoader : MonoBehaviour
{
    public static LetterDatabaseLoader Instance { get; private set; }

    public string dbFileName = "data.db";  // ชื่อไฟล์ SQLite ใน StreamingAssets
    private SQLiteConnection db;

    public List<LetterData> allLetters = new(); // เก็บ LetterData ที่โหลดมาแล้ว

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        InitDatabase();
        LoadAllLetters();
    }

    private void InitDatabase()
    {
        string filepath = Path.Combine(Application.persistentDataPath, dbFileName);

#if UNITY_EDITOR
        string sourcePath = Path.Combine(Application.streamingAssetsPath, dbFileName);
        File.Copy(sourcePath, filepath, true);  // copy ใหม่ทุกครั้งตอนทดสอบ
#endif

        db = new SQLiteConnection(filepath, SQLiteOpenFlags.ReadOnly);
    }

    private void LoadAllLetters()
    {
        var rows = db.Table<LetterRecord>().ToList();
        foreach (var row in rows)
        {
            var letterData = ConvertToLetterData(row);
            if (letterData != null)
                allLetters.Add(letterData);
        }

        Debug.Log($"[LetterDatabaseLoader] Loaded {allLetters.Count} letters.");
    }

    private LetterData ConvertToLetterData(LetterRecord record)
    {
        Sprite sp = Resources.Load<Sprite>($"Letters/{record.letter_char.ToUpper()}");
        if (sp == null)
        {
            Debug.LogWarning($"⚠️ Sprite for '{record.letter_char}' not found in Resources/Letters/");
            return null;
        }

        return new LetterData
        {
            letter = record.letter_char,
            score = record.score,
            sprite = sp
        };
    }

    public LetterData GetRandomLetter()
    {
        if (allLetters.Count == 0) return null;
        return allLetters[Random.Range(0, allLetters.Count)];
    }
}
