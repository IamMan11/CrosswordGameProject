using UnityEngine;

/// <summary>
/// CurrencyManager
/// - ตัวจัดการ “เหรียญ” ฝั่ง UI/Shop อย่างง่าย (ซิงเกิลตัน + ค่าบนหน่วยความจำ)
/// - มี API พื้นฐาน: Has / Spend / Add และ Save ลง PlayerPrefs (ยังไม่โหลดกลับมาที่ Awake)
///
/// หมายเหตุ:
/// - คงพฤติกรรมเดิม: เซ็ตเหรียญเริ่มจาก startCoins ทุกครั้งที่เปิดแอป
/// - มีการ Save() หลัง Spend/Add (พร้อม PREF_KEY) — ถ้าต้องการให้ “โหลดกลับ” จริง ๆ
///   สามารถเติมโค้ดอ่าน PlayerPrefs ใน Awake ได้ภายหลัง (จะเปลี่ยนพฤติกรรมเริ่มเกม)
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("Initial Coins (สำหรับผู้เล่นใหม่)")]
    [Tooltip("เหรียญตั้งต้นเมื่อเริ่มแอป (ปัจจุบันยังไม่โหลดคืนจาก PlayerPrefs)")]
    [SerializeField] int startCoins = 1000;

    const string PREF_KEY = "CC_COINS";

    int coins;
    /// <summary>อ่านจำนวนเหรียญปัจจุบัน</summary>
    public int Coins => coins;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ถ้าเคยมีเซฟ ให้ใช้ค่าที่เซฟไว้; ถ้ายังไม่เคย ให้ใช้ startCoins เป็นเงินเริ่มต้น
        coins = PlayerPrefs.HasKey(PREF_KEY) ? PlayerPrefs.GetInt(PREF_KEY) : startCoins;
        Save(); // เขียนกลับกันพลาด + sync ไป SO ถ้ามี
    }

    void Save()
    {
        coins = Mathf.Max(0, coins);
        PlayerPrefs.SetInt(PREF_KEY, coins);
        if (PlayerProgressSO.Instance?.data != null)
            PlayerProgressSO.Instance.data.coins = coins;
    }
    // <<< ใช้อันนี้ตอนกด NewPlay
    public void ResetToStart()
    {
        coins = Mathf.Max(0, startCoins);
        Save();
    }

    // เผื่ออยากตั้งค่าเหรียญตรงๆ ตอน NewPlay
    public void SetCoins(int amount)
    {
        coins = Mathf.Max(0, amount);
        Save();
    }

    /// <summary>มีเงินพอหรือไม่</summary>
    public bool Has(int amount) => amount <= 0 || coins >= amount;

    /// <summary>หักเหรียญ (ไม่พอ → false)</summary>
    public bool Spend(int amount)
    {
        if (!Has(amount)) return false;
        coins -= amount;
        Save();
        return true;
    }

    /// <summary>เพิ่มเหรียญ</summary>
    public void Add(int amount)
    {
        coins += Mathf.Max(0, amount);
        Save();
    }
}
