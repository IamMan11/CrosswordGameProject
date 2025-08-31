using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using SQLite4Unity3d;

public class WordChecker : MonoBehaviour
{
    public static WordChecker Instance { get; private set; }

    [Header("Database")]
    [Tooltip("ชื่อไฟล์ฐานข้อมูล .db ใน Assets/StreamingAssets")]
    public string databaseFileName = "data.db";

    [Tooltip("Preload คำทั้งหมดเข้าหน่วยความจำ (ไวมากแต่ใช้ RAM)")]
    public bool preloadAllOnStart = false;

    private SQLiteConnection conn;
    private string tableName = "word"; // จะตรวจและตั้งให้อัตโนมัติอีกที
    private readonly HashSet<string> dict = new();  // เก็บเป็น UPPER
    private readonly List<Entry> entries = new();   // ใช้เฉพาะถ้า preload

    [Serializable]
    public class Entry
    {
        public string Word;
        public string Type;
        public string Translation;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    IEnumerator Start()
    {
        // 1) คัดลอก DB จาก StreamingAssets -> persistentDataPath (รองรับ Android/iOS)
        string dst = Path.Combine(Application.persistentDataPath, databaseFileName);
        if (!File.Exists(dst))
        {
            string src = Path.Combine(Application.streamingAssetsPath, databaseFileName);
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var uwr = UnityWebRequest.Get(src))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[WordChecker] Copy DB failed: " + uwr.error);
                    yield break;
                }
                File.WriteAllBytes(dst, uwr.downloadHandler.data);
            }
#else
            try { File.Copy(src, dst, true); }
            catch (Exception ex) { Debug.LogError("[WordChecker] Copy DB failed: " + ex.Message); yield break; }
#endif
        }

        // 2) เปิดการเชื่อมต่อ
        try
        {
            conn = new SQLiteConnection(dst, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex);
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] Open DB failed: " + ex.Message);
            yield break;
        }

        // 3) ตรวจหาชื่อตารางจริง (word / Dictionary)
        try
        {
            string detected = conn.ExecuteScalar<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND lower(name) IN ('word','dictionary') LIMIT 1;");
            if (!string.IsNullOrEmpty(detected)) tableName = detected;
            else { Debug.LogError("[WordChecker] ไม่พบตาราง 'word' หรือ 'Dictionary' ใน DB"); yield break; }
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] Detect table failed: " + ex.Message);
            yield break;
        }

        // 4) (ทางเลือก) Preload ทั้งหมด
        if (preloadAllOnStart)
        {
            try
            {
                var rows = conn.Query<Entry>($"SELECT Word, Type, Translation FROM {tableName};");
                entries.Clear(); entries.AddRange(rows);
                dict.Clear();
                foreach (var e in rows)
                {
                    if (!string.IsNullOrWhiteSpace(e.Word))
                        dict.Add(e.Word.Trim().ToUpperInvariant());
                }
                Debug.Log($"[WordChecker] ✅ Preloaded {entries.Count} entries. Unique={dict.Count}. Table={tableName}");
            }
            catch (Exception ex)
            {
                Debug.LogError("[WordChecker] Preload failed: " + ex.Message);
            }
        }
        else
        {
            Debug.Log($"[WordChecker] ✅ DB ready (no preload). Table={tableName} Path={dst}");
        }
    }

    void OnDestroy() { conn?.Close(); }

    // ===== API =====

    public bool IsWordValid(string w)
    {
        if (string.IsNullOrWhiteSpace(w)) return false;
        string key = w.Trim().ToUpperInvariant();

        // ถ้า preload แล้ว เช็คจาก HashSet เร็วสุด
        if (dict.Count > 0) return dict.Contains(key);

        // เช็คตรงจาก DB (อาศัย COLLATE NOCASE)
        try
        {
            string sql = $"SELECT EXISTS(SELECT 1 FROM {tableName} WHERE Word=? COLLATE NOCASE LIMIT 1);";
            return conn.ExecuteScalar<int>(sql, w.Trim()) == 1;
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] IsWordValid error: " + ex.Message);
            return false;
        }
    }

    // ใช้อินเด็กซ์ที่คุณสร้าง: CREATE INDEX idx_word_len_expr ON word(length(Word));
    public List<string> GetWordsByLength(int len, int limit = 10000)
    {
        try
        {
            if (dict.Count > 0)
            {
                // ถ้า preload แล้ว ตัดจากหน่วยความจำ
                return dict.Where(s => s.Length == len).Take(limit).ToList();
            }
            string sql = $"SELECT Word AS w FROM {tableName} WHERE length(Word)=? ORDER BY Word LIMIT ?;";
            return conn.Query<Row>(sql, len, limit).Select(r => r.w).ToList();
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] GetWordsByLength error: " + ex.Message);
            return new List<string>();
        }
    }

    public List<string> GetAllWordsSorted()
    {
        try
        {
            if (entries.Count > 0) return entries.Select(e => e.Word).OrderBy(w => w).ToList();
            string sql = $"SELECT Word AS w FROM {tableName} ORDER BY Word;";
            return conn.Query<Row>(sql).Select(r => r.w).ToList();
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] GetAllWordsSorted error: " + ex.Message);
            return new List<string>();
        }
    }

    public bool TryGetInfo(string word, out string pos, out string translation)
    {
        pos = translation = null;
        try
        {
            if (entries.Count > 0)
            {
                var e = entries.FirstOrDefault(x => string.Equals(x.Word, word, StringComparison.OrdinalIgnoreCase));
                if (e != null) { pos = e.Type; translation = e.Translation; return true; }
                return false;
            }
            string sql = $"SELECT Type, Translation FROM {tableName} WHERE Word=? COLLATE NOCASE LIMIT 1;";
            var r = conn.Query<Info>(sql, word.Trim()).FirstOrDefault();
            if (r == null) return false;
            pos = r.Type; translation = r.Translation; return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] TryGetInfo error: " + ex.Message);
            return false;
        }
    }

    // ===== DTOs ภายใน =====
    private class Row { public string w { get; set; } }
    private class Info { public string Type { get; set; } public string Translation { get; set; } }
}