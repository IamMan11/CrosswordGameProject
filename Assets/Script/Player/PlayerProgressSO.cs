// PlayerProgressSO.cs
using UnityEngine;

/// <summary>
/// PlayerProgressSO
/// - ซิงเกิลตันตัวกลางสำหรับเข้าถึง/จัดการ PlayerProgress (ScriptableObject)
/// - โหลด asset อัตโนมัติจาก Resources/PlayerProgress ถ้าไม่ได้ผูกใน Inspector
/// - มี helper สำหรับเช็ก/เพิ่มการ์ด และรีเซ็ตความคืบหน้า
/// 
/// หมายเหตุ: คง public API เดิมทั้งหมด (Instance, data, HasCard, AddCard, ResetProgress)
/// </summary>
public class PlayerProgressSO : MonoBehaviour
{
    public static PlayerProgressSO Instance { get; private set; }

    [Tooltip("อ้างถึง PlayerProgress asset; ถ้าเว้นว่าง ระบบจะโหลดจาก Resources/PlayerProgress")]
    public PlayerProgress data;

    // คีย์ของ PlayerPrefs สำหรับรีเซ็ต (คงชื่อเดิมเพื่อความเข้ากันได้)
    const string KEY_UPG_MANA = "CC_UPG_MANA";
    const string KEY_UPG_SLOT = "CC_UPG_SLOT";
    const string KEY_UPG_TILE = "CC_UPG_TILE";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureDataLoaded();
    }

    /// <summary>ผู้เล่นมีการ์ด id นี้แล้วหรือยัง</summary>
    public bool HasCard(string id)
    {
        EnsureDataLoaded();
        if (string.IsNullOrEmpty(id) || data == null || data.ownedCardIds == null) return false;
        return data.ownedCardIds.Contains(id);
    }

    /// <summary>เพิ่มการ์ด id เข้าคลังผู้เล่น (ถ้ายังไม่มี)</summary>
    public void AddCard(string id)
    {
        EnsureDataLoaded();
        if (string.IsNullOrEmpty(id) || data == null) return;

        if (data.ownedCardIds == null) data.ownedCardIds = new System.Collections.Generic.List<string>();
        if (!data.ownedCardIds.Contains(id))
            data.ownedCardIds.Add(id);
    }

    /// <summary>
    /// รีเซ็ตความคืบหน้าทั้งหมดกลับค่าเริ่มต้น
    /// - รีเซ็ต ScriptableObject ในหน่วยความจำ
    /// - ลบ PlayerPrefs ที่เกี่ยวกับการอัปเกรด
    /// </summary>
    public void ResetProgress()
    {
        EnsureDataLoaded();
        if (data == null) { Debug.LogWarning("[PlayerProgressSO] ไม่มี data ให้รีเซ็ต"); return; }

        // รีเซ็ตค่าพื้นฐานใน ScriptableObject
        data.coins = 0;
        data.maxMana = 10;
        data.maxCardSlots = 2;
        data.extraTiles = 0;

        if (data.ownedCardIds == null) data.ownedCardIds = new System.Collections.Generic.List<string>();
        data.ownedCardIds.Clear();

        data.manaUpCount = 0;
        data.slotUpCount = 0;
        data.tileUpCount = 0;

        // รีเซ็ตคีย์อัปเกรดใน Shop (PlayerPrefs)
        PlayerPrefs.DeleteKey(KEY_UPG_MANA);
        PlayerPrefs.DeleteKey(KEY_UPG_SLOT);
        PlayerPrefs.DeleteKey(KEY_UPG_TILE);
        PlayerPrefs.Save();

        Debug.Log("[PlayerProgressSO] Progress ถูกรีเซ็ตทั้งหมด");
    }

    /// <summary>
    /// โหลด PlayerProgress asset ถ้ายังไม่ได้อ้างอิง และกันลิสต์ภายในเป็น null
    /// </summary>
    private void EnsureDataLoaded()
    {
        if (data == null)
        {
            data = Resources.Load<PlayerProgress>("PlayerProgress");
            if (data == null)
                Debug.LogWarning("[PlayerProgressSO] ไม่พบ Resources/PlayerProgress — โปรดสร้าง ScriptableObject นี้ในโฟลเดอร์ Resources");
        }

        if (data != null && data.ownedCardIds == null)
            data.ownedCardIds = new System.Collections.Generic.List<string>();
    }
}
