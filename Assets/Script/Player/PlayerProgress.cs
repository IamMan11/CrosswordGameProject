using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PlayerProgress (ScriptableObject)
/// - เก็บสเตทความคืบหน้าของผู้เล่นที่แก้ไขได้ใน Inspector หรือโหลดผ่าน Resources
/// - ใช้ร่วมกับ PlayerProgressSO (ตัวกลางซิงเกิลตันที่อ้างอิง asset นี้)
/// 
/// หมายเหตุ: คงฟิลด์ public เดิมทุกตัวเพื่อไม่ให้โค้ดอื่นที่อ้างถึงพัง
/// </summary>
[CreateAssetMenu(menuName = "CrossClash/Player Progress")]
public class PlayerProgress : ScriptableObject
{
    [Header("Coins / Currency")]
    [Tooltip("จำนวนเหรียญสะสมของผู้เล่น")]
    public int coins = 0;

    [Header("Upgrades")]
    [Tooltip("มานาสูงสุดเริ่มต้น/ปัจจุบัน")]
    public int maxMana = 10;

    [Tooltip("จำนวนช่องการ์ดสูงสุดในมือ")]
    public int maxCardSlots = 2;

    [Tooltip("จำนวน tile เพิ่มเติมในถุง (นอกเหนือจากดีฟอลต์)")]
    public int extraTiles = 0;

    [Header("Card Ownership")]
    [Tooltip("รายการ id การ์ดที่ผู้เล่นเป็นเจ้าของแล้ว")]
    public List<string> ownedCardIds = new();

    [Header("Upgrade Count")]
    [Tooltip("จำนวนครั้งที่อัปเกรดมานาแล้ว (ใช้คำนวณราคาเพิ่มขั้น)")]
    public int manaUpCount  = 0;

    [Tooltip("จำนวนครั้งที่อัปเกรดช่องการ์ดแล้ว")]
    public int slotUpCount  = 0;

    [Tooltip("จำนวนครั้งที่อัปเกรดจำนวนไทล์ในถุงแล้ว")]
    public int tileUpCount  = 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // กันใส่ค่าติดลบจาก Inspector
        coins        = Mathf.Max(0, coins);
        maxMana      = Mathf.Max(1, maxMana);
        maxCardSlots = Mathf.Max(1, maxCardSlots);
        extraTiles   = Mathf.Max(0, extraTiles);
        manaUpCount  = Mathf.Max(0, manaUpCount);
        slotUpCount  = Mathf.Max(0, slotUpCount);
        tileUpCount  = Mathf.Max(0, tileUpCount);

        // กันลิสต์เป็น null หากแก้ asset ตอนปิด playmode
        if (ownedCardIds == null) ownedCardIds = new List<string>();
    }
#endif
}
