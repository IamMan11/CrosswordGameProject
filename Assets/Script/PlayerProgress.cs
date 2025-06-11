using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "CrossClash/Player Progress")]
public class PlayerProgress : ScriptableObject
{
    [Header("Coins / Currency")]
    public int coins = 0;               // ใช้ร่วมกับ CurrencyManager ก็ได้

    [Header("Upgrades")]
    public int maxMana = 10;
    public int maxCardSlots = 2;
    public int extraTiles = 0;        // จำนวน tile เพิ่มเติมในถุง
    [Header("Card Ownership")]
    public List<string> ownedCardIds = new();   // เก็บ card.id ที่ซื้อไปแล้ว
    [Header("Upgrade Count")]
    public int manaUpCount  = 0;
    public int slotUpCount  = 0;
    public int tileUpCount  = 0;
}
