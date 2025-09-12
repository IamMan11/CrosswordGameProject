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
    [SerializeField] int startCoins = 9999;

    const string PREF_KEY = "CC_COINS";

    int coins;
    /// <summary>อ่านจำนวนเหรียญปัจจุบัน</summary>
    public int Coins => coins;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // พฤติกรรมเดิม: ตั้งจาก startCoins ทุกครั้ง
        coins = Mathf.Max(0, startCoins);

        // NOTE:
        // ถ้าอยาก “โหลดจำนวนเหรียญที่บันทึกไว้” แทนที่จะรีเซ็ตทุกครั้ง
        // สามารถใช้โค้ดนี้ (จะทำให้พฤติกรรมเปลี่ยน):
        // if (PlayerPrefs.HasKey(PREF_KEY)) coins = Mathf.Max(0, PlayerPrefs.GetInt(PREF_KEY));
    }

    /* ---------- API ---------- */

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

    /* ---------- Helper ---------- */

    /// <summary>บันทึกลง PlayerPrefs (ปัจจุบันยังไม่อ่านคืนตอนเริ่ม)</summary>
    void Save() => PlayerPrefs.SetInt(PREF_KEY, Mathf.Max(0, coins));
}
