using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }
    /* === UI อ้างอิง (ลากใน Inspector) ========================= */
    [Header("UI")]
    [SerializeField] TMP_Text coinText;
    [SerializeField] Button manaBtn;
    [SerializeField] Button slotBtn;
    [SerializeField] Button tileBtn;
    [SerializeField] Button RerollBtn;

    /* === ราคาต่อครั้ง  ======================================== */
    [Header("Upgrade Cost / ครั้ง")]
    [SerializeField] int manaUpgradeCost = 50;
    [SerializeField] int slotUpgradeCost = 75;
    [SerializeField] int tilepackCost = 40;
    [SerializeField] int rerollCost = 50;

    /* === จำนวนครั้งซื้อสูงสุด  ================================ */
    [Header("Upgrade Limits (ครั้ง)")]
    [SerializeField] int manaUpgradeMaxTimes = 5;   // +2 Mana / ครั้ง
    [SerializeField] int slotUpgradeMaxTimes = 2;   // +1 Card Slot / ครั้ง
    [SerializeField] int tileUpgradeMaxTimes = 10;  // +10 Tile / ครั้ง

    [Header("Shop Slots")]
    [SerializeField] ShopCardSlot[] shopSlots;

    /* === คีย์เก็บข้อมูลลง PlayerPrefs ========================= */
    const string PREF_MANA = "CC_UPG_MANA";
    const string PREF_SLOT = "CC_UPG_SLOT";
    const string PREF_TILE = "CC_UPG_TILE";

    /* === ตัวนับในหน่วยความจำ  ================================ */
    int manaBought, slotBought, tileBought;

    /* ----------------------------------------------------------- */
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }  // 🆕
        Instance = this;                                                            // 🆕

        // ของเดิม
        manaBought = PlayerPrefs.GetInt(PREF_MANA, 0);
        slotBought = PlayerPrefs.GetInt(PREF_SLOT, 0);
        tileBought = PlayerPrefs.GetInt(PREF_TILE, 0);
    }

    void Start()
    {
        RefreshUI();        // ของเดิม
        TryRerollAtStart();
    }
    List<CardData> BuildPurchasablePool()
    {
        // โหลดการ์ดทั้งหมดจาก Resources/Cards
        var all = Resources.LoadAll<CardData>("Cards");

        // กรอง: requirePurchase = true และยังไม่เป็นเจ้าของ
        var pool = all.Where(c => c != null
                            && c.requirePurchase
                            && !PlayerProgressSO.Instance.HasCard(c.id))
                    .ToList();
        return pool;
    }
    void TryRerollAtStart() {
        if (CardManager.Instance == null) {
            Debug.LogError("[Shop] CardManager.Instance = null (ยังไม่ได้วาง CardManager ในซีน)");
            return;
        }
        if (shopSlots == null || shopSlots.Length == 0) {
            Debug.LogError("[Shop] shopSlots ยังไม่ถูกตั้งค่าใน Inspector");
            return;
        }
        for (int i = 0; i < shopSlots.Length; i++) {
            if (shopSlots[i] == null) {
                Debug.LogError($"[Shop] shopSlots[{i}] = null (ยังไม่ได้ลากสลอตช่องที่ {i})");
                return;
            }
        }
        RerollShop();
    }

    /* ---------- ปุ่ม Back ไปเมนูหลัก ---------- */
    public void OnPlayPressed()
    {
        // รีเซ็ตพูลให้ตรงกับค่า extraTiles ปัจจุบัน
        if (TileBag.Instance != null)
            TileBag.Instance.ResetPool();    // เรียก RebuildPool() ใหม่ :contentReference[oaicite:0]{index=0}

        // จากนั้นค่อยโหลด Scene เกมหลัก
        SceneManager.LoadScene("Try");
    }

    /* ---------- ปุ่มอัปเกรดแต่ละอย่าง ---------- */
    public void OnBuyMana()
    {
        if (manaBought >= manaUpgradeMaxTimes)
        {
            ShowMsg("อัปเกรด Mana เต็มแล้ว!");
            return;
        }

        if (!Spend(manaUpgradeCost)) return;

        PlayerProgressSO.Instance.data.maxMana += 2;
        manaBought++;
        PlayerPrefs.SetInt(PREF_MANA, manaBought);
        RefreshUI();
        ShowMsg("+2 Mana สำเร็จ");
    }

    public void OnBuyCardSlot()
    {
        if (slotBought >= slotUpgradeMaxTimes)
        {
            ShowMsg("อัปเกรด Slot เต็มแล้ว!");
            return;
        }

        if (!Spend(slotUpgradeCost)) return;

        // อัปเดต Progress
        PlayerProgressSO.Instance.data.maxCardSlots += 1;

        // อัปเดตจำนวนช่องการ์ดที่ถือได้ใน CardManager ให้ตรงกับ Progress ใหม่
        if (CardManager.Instance != null)
            CardManager.Instance.UpgradeMaxHeldCards(PlayerProgressSO.Instance.data.maxCardSlots);

        // บันทึกจำนวนครั้งซื้อ และอัปเดต UI
        slotBought++;
        PlayerPrefs.SetInt(PREF_SLOT, slotBought);
        RefreshUI();
        ShowMsg("+1 Card Slot สำเร็จ");
    }

    public void OnBuyTilePack()
    {
        if (tileBought >= tileUpgradeMaxTimes)
        {
            ShowMsg("อัปเกรด TilePack เต็มแล้ว!");
            return;
        }
        if (!Spend(tilepackCost)) return;

        // 1) อัปเดต ProgressSO ก่อน
        PlayerProgressSO.Instance.data.extraTiles += 10;
        tileBought++;
        PlayerPrefs.SetInt(PREF_TILE, tileBought);


        RefreshUI();
        ShowMsg("+10 Tiles สำเร็จ");
    }
    public void OnRerollPressed() {
        if (!CurrencyManager.Instance.Spend(rerollCost)) {
            Debug.Log("[Shop] เหรียญไม่พอสำหรับ reroll");
            return;
        }
        RerollShop();
        RefreshUI(); // อัปเดตตัวเลข coin
    }
    void RerollShop() {
    // เซฟการ์ดพูล
        var pool = BuildPurchasablePool();

        for (int i = 0; i < shopSlots.Length; i++)
        {
            CardData pick = null;

            if (pool.Count > 0)
            {
                // ดึงแบบสุ่ม (และไม่ซ้ำ)
                int idx = Random.Range(0, pool.Count);
                pick = pool[idx];
                pool.RemoveAt(idx);
            }

            // ใส่การ์ดลงช่อง (หรือ null ถ้าพูลหมด)
            shopSlots[i].SetCard(pick);

            // ถ้าอยากซ่อนช่องว่าง ให้ใช้:
            // shopSlots[i].gameObject.SetActive(pick != null);
        }
    }

    /* ---------- Helper: หักเหรียญ ---------- */
    bool Spend(int cost)
    {
        if (!CurrencyManager.Instance.Spend(cost))
        {
            ShowMsg("เหรียญไม่พอ");
            return false;
        }
        return true;
    }

    /* ---------- อัปเดต UI / ล็อกปุ่ม ---------- */
    public void RefreshUI()
    {
        coinText.text = $"Coins : {CurrencyManager.Instance.Coins}";

        manaBtn.interactable = manaBought < manaUpgradeMaxTimes;
        slotBtn.interactable = slotBought < slotUpgradeMaxTimes;
        tileBtn.interactable = tileBought < tileUpgradeMaxTimes;
    }

    /* ---------- แสดงข้อความแบบง่าย ---------- */
    void ShowMsg(string msg) => Debug.Log($"[Shop] {msg}");
    public void ShowToast(string msg) => ShowMsg(msg); 
}
