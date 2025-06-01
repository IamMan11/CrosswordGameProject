using UnityEngine;

[CreateAssetMenu(menuName = "CrossClash/Player Progress")]
public class PlayerProgress : ScriptableObject
{
    [Header("Coins / Currency")]
    public int coins = 0;               // ใช้ร่วมกับ CurrencyManager ก็ได้

    [Header("Upgrades")]
    public int maxMana      = 10;
    public int maxCardSlots = 2;
    public int extraTiles   = 0;        // จำนวน tile เพิ่มเติมในถุง
}
