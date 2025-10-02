using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using SQLite4Unity3d;

/// <summary>
/// WordChecker
/// - โหลดฐานข้อมูลคำ (SQLite) จาก StreamingAssets → คัดลอกไป persistentDataPath แล้วเปิดแบบ ReadOnly
/// - ตรวจคำแบบ 2 ชั้น: in-memory cache (HashSet) → DB (พร้อม memoize ลง cache)
/// - รองรับ 2 โหมด:
///     1) preloadAllOnStart = true  : ดึงทั้งตารางเข้าหน่วยความจำ (เร็วมาก RAM มาก)
///     2) preloadAllOnStart = false : อุ่น cache อัตโนมัติหลังเชื่อมต่อ (โหลดเฉพาะคอลัมน์ Word)
/// - ยืดหยุ่นกับ schema: ตรวจหาตารางที่มีคอลัมน์ Word และเช็กว่ามี Type/Translation/len หรือไม่
/// - เวลาตรวจคำ จะเคารพกฎ minWordLength (เช่น ตัดคำ 1 ตัวอักษรทิ้งก่อน)
/// 
/// หมายเหตุ: คงเมธอด/ฟิลด์เดิมครบถ้วนเพื่อไม่กระทบสคริปต์อื่น
/// </summary>
public class WordChecker : MonoBehaviour
{
    public static WordChecker Instance { get; private set; }

    #region Inspector

    [Header("Database")]
    [Tooltip("ชื่อไฟล์ฐานข้อมูล .db ใน Assets/StreamingAssets")]
    public string databaseFileName = "data.db";

    [Tooltip("Preload คำทั้งหมดเข้าหน่วยความจำ (ไวมากแต่ใช้ RAM)")]
    public bool preloadAllOnStart = false;

    [Tooltip("คัดลอกไฟล์ DB จาก StreamingAssets มาทับ persistentDataPath ทุกครั้ง (เปิดแค่ตอนอัปเดต DB)")]
    public bool alwaysOverwriteDbAtLaunch = true;

    [Header("Rules")]
    [Tooltip("ความยาวคำขั้นต่ำที่จะนับว่าถูกต้อง")]
    public int minWordLength = 2;   // กันคำ 1 ตัวอักษร

    #endregion

    #region Runtime fields

    private SQLiteConnection conn;
    private string tableName = null;              // จะถูกตั้งหลังตรวจเจอ schema
    private readonly HashSet<string> dict = new();  // เก็บเป็น UPPER (ใช้เมื่อ preload หรือ cache runtime)
    private readonly List<Entry> entries = new();   // ใช้เฉพาะถ้า preload
    private bool tablesReady = false;               // true เมื่อเจอตารางพร้อมใช้งาน
    private string dbPathRuntime = "";

    // คอลัมน์ที่พบจริงในตาราง (กันเคสไม่มี Type/Translation/len)
    private bool hasColType = false;
    private bool hasColTranslation = false;
    private bool hasColLen = false;

    // อุ่นแคชครั้งเดียว
    private bool cacheWarmedUp = false;

    // (ออปชัน) นับสถิติเล็ก ๆ (โชว์ผ่าน Context Menu)
    private int cacheHit = 0, dbHit = 0;

    #endregion

    #region DTOs

    [Serializable]
    public class Entry
    {
        public string Word;
        public string Type;
        public string Translation;
    }

    // ใช้ map ผล query แบบ lightweight
    private class Row { public string w { get; set; } }
    private class Info { public string Type { get; set; } public string Translation { get; set; } }
    private class TableRow { public string name { get; set; } public string type { get; set; } }
    private class ColInfo { public int cid { get; set; } public string name { get; set; } public string type { get; set; } }

    #endregion

    #region Unity lifecycle

    /// <summary>พร้อมใช้งานแบบซิงเกิลตันข้ามซีน</summary>
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>คัดลอก DB → เปิด ReadOnly → ตรวจ schema → (ออปชัน) preload/อุ่น cache</summary>
    IEnumerator Start()
    {
        dbPathRuntime = Path.Combine(Application.persistentDataPath, databaseFileName);
        Debug.Log($"[WordChecker] persistentDataPath: {Application.persistentDataPath}");
        Debug.Log($"[WordChecker] streamingAssetsPath: {Application.streamingAssetsPath}");
        Debug.Log($"[WordChecker] target DB path: {dbPathRuntime}");

        // 1) คัดลอก DB (ทับเมื่อเปิด alwaysOverwriteDbAtLaunch หรือยังไม่มีไฟล์)
        bool needCopy = alwaysOverwriteDbAtLaunch || !File.Exists(dbPathRuntime);
        if (needCopy)
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
                File.WriteAllBytes(dbPathRuntime, uwr.downloadHandler.data);
            }
#else
            try { File.Copy(src, dbPathRuntime, true); }
            catch (Exception ex) { Debug.LogError("[WordChecker] Copy DB failed: " + ex.Message); yield break; }
#endif
            if (alwaysOverwriteDbAtLaunch)
                Debug.Log("[WordChecker] DB overwritten from StreamingAssets (alwaysOverwriteDbAtLaunch=true).");
        }

        // 2) เปิดการเชื่อมต่อ
        try
        {
            conn = new SQLiteConnection(dbPathRuntime, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex);
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] Open DB failed: " + ex.Message);
            yield break;
        }

        // 3) ตรวจหา “ตารางไหนก็ได้ที่มีคอลัมน์ Word”
        if (!DetectDictionaryTable(conn, out tableName, out hasColType, out hasColTranslation, out hasColLen))
        {
            Debug.LogError("[WordChecker] ❌ ไม่พบตารางที่มีคอลัมน์ 'Word' ใน DB ที่ใช้งานจริง");
            yield break;
        }
        tablesReady = true;

        // 4) (ทางเลือก) Preload ทั้งหมด
        if (preloadAllOnStart)
        {
            try
            {
                string q = $"SELECT Word{(hasColType ? ", Type" : "")}{(hasColTranslation ? ", Translation" : "")} FROM {Q(tableName)};";
                var rows = conn.Query<Entry>(q);
                entries.Clear(); entries.AddRange(rows);
                dict.Clear();
                foreach (var e in rows)
                {
                    if (!string.IsNullOrWhiteSpace(e.Word))
                        dict.Add(e.Word.Trim().ToUpperInvariant());
                }
                cacheWarmedUp = true; // ถือว่าอุ่นแล้วเพราะ preload มาเต็ม
                Debug.Log($"[WordChecker] ✅ Preloaded {entries.Count} rows | Unique={dict.Count} | Table={tableName} | Cols: Word{(hasColType ? ",Type" : "")}{(hasColTranslation ? ",Translation" : "")}");
            }
            catch (Exception ex)
            {
                Debug.LogError("[WordChecker] Preload failed: " + ex.Message);
            }
        }
        else
        {
            Debug.Log($"[WordChecker] ✅ DB ready (no preload). Using table={tableName} | Cols: Word{(hasColType ? ",Type" : "")}{(hasColTranslation ? ",Translation" : "")}{(hasColLen ? ",len" : "")} | Path={dbPathRuntime}");

            // อุ่น cache อัตโนมัติแบบเบา ๆ (จะโหลดเฉพาะ Word ทั้งหมดครั้งเดียว)
            WarmUpCacheIfNeeded();
        }
    }

    void OnDestroy() { conn?.Close(); }

    #endregion

    #region Public API

    /// <summary>
    /// ตัวชี้วัดความพร้อมของระบบตรวจคำ
    /// - true เมื่อมี cache/entries แล้ว หรือมี connection และเจอตารางที่ถูกต้อง
    /// </summary>
    public bool IsReady()
    {
        if (entries.Count > 0 || dict.Count > 0) return true; // preload แล้ว
        return conn != null && tablesReady && !string.IsNullOrEmpty(tableName);
    }

    /// <summary>
    /// ตรวจคำแบบ 2 ชั้น: cache (dict) → DB; ถ้า DB เจอ จะ cache ลง dict ด้วย
    /// - เคารพ minWordLength ก่อน query
    /// </summary>
    public bool IsWordValid(string w)
    {
        if (string.IsNullOrWhiteSpace(w)) return false;

        string trimmed = w.Trim();

        // ⛔ กันคำที่สั้นกว่า minWordLength (เช่น 1 ตัวอักษร) ไม่ต้องเช็คต่อ
        if (trimmed.Length < minWordLength)
        {
            Debug.Log($"[WordChecker] ปัดทิ้งเพราะสั้นกว่า {minWordLength}: '{w}'");
            return false;
        }

        string key = trimmed.ToUpperInvariant();

        // 1) cache ก่อน
        if (dict.Contains(key))
        {
            cacheHit++;
            Debug.Log($"[WordChecker] ตรวจคำ: '{w}' → '{key}' → ✅ (cache)");
            return true;
        }

        // 2) อุ่น cache ถ้ายังไม่อุ่น (กันเคส dict ว่างหลังโหลดใหม่)
        WarmUpCacheIfNeeded();

        // 3) เช็คอีกทีหลังอุ่น
        if (dict.Contains(key))
        {
            cacheHit++;
            Debug.Log($"[WordChecker] ตรวจคำ: '{w}' → '{key}' → ✅ (cache after warm)");
            return true;
        }

        // 4) DB fallback
        if (!IsReady())
        {
            Debug.LogWarning("[WordChecker] ตรวจคำไม่สำเร็จ: DB ยังไม่พร้อม");
            return false;
        }

        try
        {
            string sql = $"SELECT EXISTS(SELECT 1 FROM {Q(tableName)} WHERE Word=? COLLATE NOCASE LIMIT 1);";
            bool found = conn.ExecuteScalar<int>(sql, trimmed) == 1;
            if (found)
            {
                dbHit++;
                dict.Add(key); // memoize
                Debug.Log($"[WordChecker] ตรวจคำ: '{w}' → '{key}' → ✅ (db)");
                return true;
            }
            else
            {
                Debug.Log($"[WordChecker] ตรวจคำ: '{w}' → '{key}' → ❌");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] IsWordValid DB error: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// คืนลิสต์คำตามความยาว (ใช้ WHERE len=? ถ้าตารางมีคอลัมน์ len)
    /// </summary>
    public List<string> GetWordsByLength(int len, int limit = 10000)
    {
        try
        {
            if (dict.Count > 0) return dict.Where(s => s.Length == len).Take(limit).ToList();
            if (!IsReady()) return new List<string>();

            string sql = hasColLen
                ? $"SELECT Word AS w FROM {Q(tableName)} WHERE len=? ORDER BY Word LIMIT ?;"
                : $"SELECT Word AS w FROM {Q(tableName)} WHERE length(Word)=? ORDER BY Word LIMIT ?;";

            return conn.Query<Row>(sql, len, limit).Select(r => r.w).ToList();
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] GetWordsByLength error: " + ex.Message);
            return new List<string>();
        }
    }

    /// <summary>
    /// คืนลิสต์คำทั้งหมดเรียงตามตัวอักษร (ถ้า preload ไว้จะดึงจากหน่วยความจำ)
    /// </summary>
    public List<string> GetAllWordsSorted()
    {
        try
        {
            if (entries.Count > 0) return entries.Select(e => e.Word).OrderBy(w => w).ToList();
            if (!IsReady()) return new List<string>();

            string sql = $"SELECT Word AS w FROM {Q(tableName)} ORDER BY Word;";
            return conn.Query<Row>(sql).Select(r => r.w).ToList();
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] GetAllWordsSorted error: " + ex.Message);
            return new List<string>();
        }
    }

    /// <summary>
    /// คืนข้อมูลชนิดคำ/คำแปล ถ้ามีคอลัมน์รองรับ; ถ้าไม่มี ให้ fallback เป็นตรวจว่ามีคำไหม
    /// </summary>
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
            if (!IsReady()) return false;

            if (!hasColType && !hasColTranslation)
            {
                // มีแต่ Word อย่างเดียว
                return IsWordValid(word);
            }

            string cols = $"{(hasColType ? "Type" : "NULL AS Type")}, {(hasColTranslation ? "Translation" : "NULL AS Translation")}";
            string sql = $"SELECT {cols} FROM {Q(tableName)} WHERE Word=? COLLATE NOCASE LIMIT 1;";
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

    /// <summary>
    /// ใช้กับ DictionaryUI: ดึงทุกแถวกลับมา (ถ้าไม่ได้ preload ก็ query ตาม schema ที่มี)
    /// </summary>
    public List<Entry> GetAllEntries()
    {
        if (entries.Count > 0) return new List<Entry>(entries);
        if (!IsReady()) return new List<Entry>();

        string cols = "Word"
            + (hasColType ? ", Type" : ", NULL AS Type")
            + (hasColTranslation ? ", Translation" : ", NULL AS Translation");

        string sql = $"SELECT {cols} FROM {Q(tableName)} ORDER BY Word;";
        try
        {
            return conn.Query<Entry>(sql);
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] GetAllEntries error: " + ex.Message);
            return new List<Entry>();
        }
    }

    #endregion

    #region Internal helpers

    /// <summary>ใส่ Double-quote ปลอดภัยสำหรับชื่อ object (table/column)</summary>
    private static string Q(string ident)
    {
        if (string.IsNullOrEmpty(ident)) return "\"\"";
        return "\"" + ident.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// ตรวจ/ค้นหาตารางที่ใช้งานเป็นพจนานุกรมจาก connection ที่ให้มา
    /// เลือกตารางตัวแรกที่พบคอลัมน์ "Word" และรายงานว่ามีคอลัมน์ประกอบอื่น ๆ ไหม
    /// </summary>
    private bool DetectDictionaryTable(SQLiteConnection c,
                                       out string foundTable,
                                       out bool hasType,
                                       out bool hasTrans,
                                       out bool hasLen)
    {
        foundTable = null; hasType = false; hasTrans = false; hasLen = false;

        // โชว์รายการตาราง/วิวทั้งหมดเพื่อดีบัก
        var all = c.Query<TableRow>("SELECT name, type FROM sqlite_master WHERE type IN ('table','view') ORDER BY name;");
        if (all == null || all.Count == 0)
        {
            Debug.LogError("[WordChecker] ❌ DB ไม่มี table/view ใด ๆ");
            return false;
        }
        Debug.Log("[WordChecker] Objects found: " + string.Join(", ", all.Select(t => $"{t.name}({t.type})")));

        // 1) พยายามเลือกชื่อที่น่าจะใช่ก่อน
        var candidates = all
            .OrderBy(t =>
            {
                string n = t.name.ToLowerInvariant().Trim();
                if (n == "word" || n == "words" || n == "dictionary") return 0;
                if (n.Contains("word") || n.Contains("dict")) return 1;
                return 2;
            })
            .ToList();

        // 2) ตรวจคอลัมน์ของแต่ละ candidate — เลือกตัวแรกที่มีคอลัมน์ "Word"
        foreach (var t in candidates)
        {
            try
            {
                var cols = c.Query<ColInfo>($"PRAGMA table_info({Q(t.name)});");
                var set = new HashSet<string>(cols.Select(x => x.name.ToLowerInvariant()));
                bool hasWord = set.Contains("word");
                if (!hasWord) continue;

                foundTable = t.name;
                hasType = set.Contains("type");
                hasTrans = set.Contains("translation");
                hasLen = set.Contains("len");

                Debug.Log($"[WordChecker] ✅ Using table: {foundTable} | Columns: {string.Join(",", set)}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WordChecker] PRAGMA table_info({t.name}) failed: {ex.Message}");
            }
        }

        return false;
    }
    public string GetRandomValidWord(int minLen, int maxLen)
    {
        try
        {
            // 1) ใช้ in-memory cache ก่อน (อุ่น cache ถ้ายัง)
            WarmUpCacheIfNeeded();
            if (dict != null && dict.Count > 0)
            {
                // dict เก็บเป็น UPPER ทั้งหมด → คืนเป็น lower/ต้นฉบับก็ได้ตามที่ต้องการ
                var pool = dict.Where(w => w.Length >= minLen && w.Length <= maxLen).ToList();
                if (pool.Count > 0)
                {
                    var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                    return pick; // ถ้าต้องการ lower: return pick.ToLowerInvariant();
                }
            }

            // 2) ถ้า cache ไม่พอ ใช้ DB โดยตรง (SQLite)
            if (!IsReady()) return null;

            string sql = hasColLen
                ? $"SELECT Word AS w FROM {Q(tableName)} WHERE len BETWEEN ? AND ? ORDER BY RANDOM() LIMIT 1;"
                : $"SELECT Word AS w FROM {Q(tableName)} WHERE length(Word) BETWEEN ? AND ? ORDER BY RANDOM() LIMIT 1;";

            var row = conn.Query<Row>(sql, minLen, maxLen).FirstOrDefault();
            return row?.w; // อาจได้เคส null ถ้าไม่มีคำตามช่วงความยาว
        }
        catch (Exception ex)
        {
            Debug.LogError("[WordChecker] GetRandomValidWord error: " + ex.Message);
            return null;
        }
    }


    /// <summary>
    /// อุ่น cache ในหน่วยความจำ (โหลดคอลัมน์ Word ทั้งหมดครั้งเดียว)
    /// เรียกอัตโนมัติเมื่อยังไม่อุ่นและไม่มี preload
    /// </summary>
    private void WarmUpCacheIfNeeded()
    {
        if (cacheWarmedUp || dict.Count > 0 || entries.Count > 0) return;
        if (!IsReady()) return;

        try
        {
            var rows = conn.Query<Row>($"SELECT Word AS w FROM {Q(tableName)};");
            int added = 0;
            foreach (var r in rows)
            {
                if (string.IsNullOrWhiteSpace(r.w)) continue;
                if (dict.Add(r.w.Trim().ToUpperInvariant())) added++;
            }
            cacheWarmedUp = true;
            Debug.Log($"[WordChecker] 🔥 Warmed cache: +{added} words (total {dict.Count})");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WordChecker] WarmUpCacheIfNeeded failed: " + ex.Message);
        }
    }

    #endregion

    #region Editor Context Menu (ช่วยดีบัก)

#if UNITY_EDITOR
    [ContextMenu("WordChecker/Open persistentDataPath")]
    void _OpenPersistentPath() => UnityEditor.EditorUtility.RevealInFinder(Application.persistentDataPath);

    [ContextMenu("WordChecker/Stats")]
    void _Stats() => Debug.Log($"[WordChecker] cacheHit={cacheHit}, dbHit={dbHit}, dictSize={dict.Count}, warmed={cacheWarmedUp}");
#endif

    [ContextMenu("WordChecker/Delete cached DB (persistentDataPath)")]
    void _DeleteCachedDb()
    {
        var p = Path.Combine(Application.persistentDataPath, databaseFileName);
        if (File.Exists(p)) { File.Delete(p); Debug.Log("[WordChecker] Deleted cached DB: " + p); }
        else Debug.Log("[WordChecker] No cached DB at: " + p);
    }

    [ContextMenu("WordChecker/Clear in-memory cache")]
    void _ClearCache()
    {
        dict.Clear();
        entries.Clear();
        cacheWarmedUp = false;
        Debug.Log("[WordChecker] Cleared in-memory cache.");
    }

    #endregion
}
