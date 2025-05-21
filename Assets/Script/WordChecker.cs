using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using SQLite4Unity3d;

public class WordChecker : MonoBehaviour
{
    public static WordChecker Instance { get; private set; }

    [Header("SQLite4Unity3d Settings")]
    [Tooltip("ชื่อไฟล์ฐานข้อมูล .db ให้วางใน StreamingAssets")]
    public string databaseFileName = "data.db";

    [Table("Dictionary")]
    public class Entry
    {
        [PrimaryKey, Column("Word")]
        public string Word { get; set; }

        [Column("Type")]
        public string Type { get; set; }

        [Column("Translation")]
        public string Translation { get; set; }
    }

    private SQLiteConnection dbConnection;
    private List<Entry> entries = new();
    private HashSet<string> dict = new();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitDatabase()
    {
        string dbPath = Path.Combine(Application.streamingAssetsPath, databaseFileName);
        Debug.Log($"[WordChecker] DB Path: {dbPath}");

        if (!File.Exists(dbPath))
        {
            Debug.LogError("[WordChecker] ❌ File not found at path: " + dbPath);
            return;
        }

        try
        {
            dbConnection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly);
            entries = dbConnection.Table<Entry>().ToList();

            dict = new HashSet<string>(
                entries.Where(e => !string.IsNullOrWhiteSpace(e.Word))
                       .Select(e => e.Word.Trim().ToUpper())
            );

            Debug.Log($"[WordChecker] ✅ Loaded {entries.Count} entries. Unique words in dict: {dict.Count}");
            Debug.Log($"[WordChecker] ตัวอย่างคำใน dict: {string.Join(", ", dict.Take(10))}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WordChecker] ❌ Failed to load database: {ex.Message}");
        }
    }

    // ========== API ==========

    public bool IsWordValid(string w)
    {
        if (string.IsNullOrWhiteSpace(w)) return false;

        string key = w.Trim().ToUpper();
        bool found = dict.Contains(key);

        Debug.Log($"[WordChecker] ตรวจคำ: '{w}' → '{key}' → {(found ? "✅" : "❌")}");
        return found;
    }

    public List<string> GetAllWordsSorted()
    {
        return entries.Select(e => e.Word).OrderBy(w => w).ToList();
    }

    public List<Entry> GetAllEntries()
    {
        return entries;
    }

    void OnDestroy()
    {
        dbConnection?.Close();
    }
}
