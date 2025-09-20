// PlayerProgressSO.cs
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Singletons สำหรับเข้าถึง/จัดการ PlayerProgress + บันทึก/โหลดผ่าน PlayerPrefs
/// </summary>
public class PlayerProgressSO : MonoBehaviour
{
    public static PlayerProgressSO Instance { get; private set; }

    [Tooltip("อ้างถึง PlayerProgress asset; ถ้าเว้นว่าง ระบบจะโหลดจาก Resources/PlayerProgress")]
    public PlayerProgress data;

    // ---- PlayerPrefs Keys ----
    const string KEY_COINS      = "CC_COINS";
    const string KEY_MAXMANA    = "CC_MAXMANA";
    const string KEY_MAXSLOTS   = "CC_MAXSLOTS";
    const string KEY_EXTRATILES = "CC_EXTRATILES";
    const string KEY_CARDIDS    = "CC_CARDIDS";    // เก็บเป็น string คั่นด้วย |
    const string KEY_UPG_MANA   = "CC_UPG_MANA";
    const string KEY_UPG_SLOT   = "CC_UPG_SLOT";
    const string KEY_UPG_TILE   = "CC_UPG_TILE";
    const string KEY_LAST_SCENE = "CC_LAST_SCENE";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureDataLoaded();
        LoadFromPrefs(); // โหลด progress ที่เคยเซฟไว้
    }

    // ====== PUBLIC API ======
    public bool HasCard(string id)
    {
        EnsureDataLoaded();
        if (string.IsNullOrEmpty(id) || data == null || data.ownedCardIds == null) return false;
        return data.ownedCardIds.Contains(id);
    }

    public void AddCard(string id)
    {
        EnsureDataLoaded();
        if (string.IsNullOrEmpty(id) || data == null) return;

        if (data.ownedCardIds == null) data.ownedCardIds = new List<string>();
        if (!data.ownedCardIds.Contains(id))
            data.ownedCardIds.Add(id);

        SaveToPrefs();
    }

    /// <summary>เซฟความคืบหน้าทั้งหมดลง PlayerPrefs</summary>
    public void SaveToPrefs()
    {
        if (data == null) return;
        PlayerPrefs.SetInt(KEY_COINS,      data.coins);
        PlayerPrefs.SetInt(KEY_MAXMANA,    data.maxMana);
        PlayerPrefs.SetInt(KEY_MAXSLOTS,   data.maxCardSlots);
        PlayerPrefs.SetInt(KEY_EXTRATILES, data.extraTiles);

        PlayerPrefs.SetInt(KEY_UPG_MANA, data.manaUpCount);
        PlayerPrefs.SetInt(KEY_UPG_SLOT, data.slotUpCount);
        PlayerPrefs.SetInt(KEY_UPG_TILE, data.tileUpCount);

        // การ์ดที่มี แปลงเป็นสตริง
        var ids = (data.ownedCardIds == null || data.ownedCardIds.Count == 0)
            ? "" : string.Join("|", data.ownedCardIds);
        PlayerPrefs.SetString(KEY_CARDIDS, ids);

        PlayerPrefs.Save();
    }

    /// <summary>โหลดความคืบหน้าจาก PlayerPrefs (ถ้าไม่มีคีย์จะคงค่าจาก asset)</summary>
    public void LoadFromPrefs()
    {
        if (data == null) return;

        if (PlayerPrefs.HasKey(KEY_COINS))      data.coins        = PlayerPrefs.GetInt(KEY_COINS);
        if (PlayerPrefs.HasKey(KEY_MAXMANA))    data.maxMana      = PlayerPrefs.GetInt(KEY_MAXMANA);
        if (PlayerPrefs.HasKey(KEY_MAXSLOTS))   data.maxCardSlots = PlayerPrefs.GetInt(KEY_MAXSLOTS);
        if (PlayerPrefs.HasKey(KEY_EXTRATILES)) data.extraTiles   = PlayerPrefs.GetInt(KEY_EXTRATILES);

        if (PlayerPrefs.HasKey(KEY_UPG_MANA)) data.manaUpCount = PlayerPrefs.GetInt(KEY_UPG_MANA);
        if (PlayerPrefs.HasKey(KEY_UPG_SLOT)) data.slotUpCount = PlayerPrefs.GetInt(KEY_UPG_SLOT);
        if (PlayerPrefs.HasKey(KEY_UPG_TILE)) data.tileUpCount = PlayerPrefs.GetInt(KEY_UPG_TILE);

        var ids = PlayerPrefs.GetString(KEY_CARDIDS, "");
        if (data.ownedCardIds == null) data.ownedCardIds = new List<string>();
        data.ownedCardIds.Clear();
        if (!string.IsNullOrEmpty(ids))
            data.ownedCardIds.AddRange(ids.Split('|').Where(s => !string.IsNullOrEmpty(s)));
    }

    /// <summary>รีเซ็ต progress ทั้งหมดกลับค่าเริ่มต้น + ลบคีย์ใน PlayerPrefs</summary>
    public void ResetProgress()
    {
        EnsureDataLoaded();
        if (data == null) return;

        data.coins = 0;
        data.maxMana = 10;
        data.maxCardSlots = 2;
        data.extraTiles = 0;

        if (data.ownedCardIds == null) data.ownedCardIds = new List<string>();
        data.ownedCardIds.Clear();

        data.manaUpCount = 0;
        data.slotUpCount = 0;
        data.tileUpCount = 0;

        // ลบคีย์ทั้งหมดที่เกี่ยวข้อง
        PlayerPrefs.DeleteKey(KEY_COINS);
        PlayerPrefs.DeleteKey(KEY_MAXMANA);
        PlayerPrefs.DeleteKey(KEY_MAXSLOTS);
        PlayerPrefs.DeleteKey(KEY_EXTRATILES);
        PlayerPrefs.DeleteKey(KEY_CARDIDS);
        PlayerPrefs.DeleteKey(KEY_UPG_MANA);
        PlayerPrefs.DeleteKey(KEY_UPG_SLOT);
        PlayerPrefs.DeleteKey(KEY_UPG_TILE);
        PlayerPrefs.DeleteKey(KEY_LAST_SCENE);
        PlayerPrefs.Save();

        Debug.Log("[PlayerProgressSO] Reset all progress done.");
    }

    // ====== Last Scene ======
    public void SetLastScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        PlayerPrefs.SetString(KEY_LAST_SCENE, sceneName);
        PlayerPrefs.Save();
    }

    public string GetLastSceneOrDefault(string defaultScene)
        => PlayerPrefs.GetString(KEY_LAST_SCENE, string.IsNullOrEmpty(defaultScene) ? "Shop" : defaultScene);

    public void ClearLastScene()
    {
        PlayerPrefs.DeleteKey(KEY_LAST_SCENE);
        PlayerPrefs.Save();
    }

    // ====== Helpers ======
    private void EnsureDataLoaded()
    {
        if (data == null)
        {
            data = Resources.Load<PlayerProgress>("PlayerProgress");
            if (data == null)
                Debug.LogWarning("[PlayerProgressSO] ไม่พบ Resources/PlayerProgress");
        }
        if (data != null && data.ownedCardIds == null)
            data.ownedCardIds = new List<string>();
    }
}
