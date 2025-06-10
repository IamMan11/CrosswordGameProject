using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("Initial Coins (สำหรับผู้เล่นใหม่)")]
    [SerializeField] int startCoins = 9999;           // กำหนดใน Inspector ได้
    const string PREF_KEY = "CC_COINS";

    int coins;
    public int Coins => coins;                       // อ่านค่าเหรียญปัจจุบัน

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        coins = startCoins;
    }

    /* ---------- API ---------- */
    public bool Has(int amount) => coins >= amount;

    public bool Spend(int amount)
    {
        if (!Has(amount)) return false;
        coins -= amount;
        Save();
        return true;
    }

    public void Add(int amount)
    {
        coins += amount;
        Save();
    }

    /* ---------- Helper ---------- */
    void Save() => PlayerPrefs.SetInt(PREF_KEY, coins);
}
