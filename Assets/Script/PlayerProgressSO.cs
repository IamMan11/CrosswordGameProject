// PlayerProgressSO.cs
using UnityEngine;

public class PlayerProgressSO : MonoBehaviour
{
    public static PlayerProgressSO Instance { get; private set; }
    public PlayerProgress data;               // อ้างถึง asset

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (data == null)
            data = Resources.Load<PlayerProgress>("PlayerProgress");
    }
    public bool HasCard(string id) => data.ownedCardIds.Contains(id);
    public void AddCard(string id)
    {
        if (!data.ownedCardIds.Contains(id))
            data.ownedCardIds.Add(id);
    }
    
    public void ResetProgress()
    {
        // รีเซ็ตค่าพื้นฐานใน ScriptableObject
        data.coins = 0;
        data.maxMana = 10;
        data.maxCardSlots = 2;
        data.extraTiles = 0;
        data.ownedCardIds.Clear();
        data.manaUpCount = 0;
        data.slotUpCount = 0;
        data.tileUpCount = 0;

        // รีเซ็ตคีย์อัปเกรดใน Shop (PlayerPrefs)
        PlayerPrefs.DeleteKey("CC_UPG_MANA");
        PlayerPrefs.DeleteKey("CC_UPG_SLOT");
        PlayerPrefs.DeleteKey("CC_UPG_TILE");
        PlayerPrefs.Save();

        Debug.Log("[PlayerProgressSO] Progress ถูกรีเซ็ตทั้งหมด");
    }
    
}
