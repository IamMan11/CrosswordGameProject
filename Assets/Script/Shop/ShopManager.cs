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

    // ⬇⬇⬇ เพิ่มช่องแสดงผลตามที่ต้องการ
    [Header("UI - Upgrade Progress (n/MAX or Max)")]
    [SerializeField] TMP_Text manaProgressText;
    [SerializeField] TMP_Text slotProgressText;
    [SerializeField] TMP_Text tileProgressText;

    [Header("UI - Current Stats")]
    [SerializeField] TMP_Text manaStatText;   // data.maxMana
    [SerializeField] TMP_Text slotStatText;   // data.maxCardSlots
    [SerializeField] TMP_Text tileStatText;  // ← ใช้ตัวนี้แสดง TileBack (ความจุถุง)
    [Header("TileBag Base")]
    [SerializeField] int baseTileBack = 100;

    /* === ราคาต่อครั้ง  ======================================== */
    [Header("Upgrade Cost / ครั้ง")]
    [SerializeField] int manaUpgradeCost = 50;
    [SerializeField] int slotUpgradeCost = 75;
    [SerializeField] int tilepackCost   = 40;
    [SerializeField] int rerollCost     = 50;

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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // โหลดจำนวนที่ซื้อไว้แล้ว
        manaBought = PlayerPrefs.GetInt(PREF_MANA, 0);
        slotBought = PlayerPrefs.GetInt(PREF_SLOT, 0);
        tileBought = PlayerPrefs.GetInt(PREF_TILE, 0);

        // ซิงก์ค่าตัวนับไปที่ ScriptableObject ด้วย (เผื่อไปแสดงที่อื่น)
        if (PlayerProgressSO.Instance != null && PlayerProgressSO.Instance.data != null)
        {
            var so = PlayerProgressSO.Instance.data;
            so.manaUpCount = manaBought;
            so.slotUpCount = slotBought;
            so.tileUpCount = tileBought;
        }
    }

    void OnEnable() => RefreshUI();

    void Start()
    {
        RefreshUI();
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
    int GetTileBackCapacity()
    {
        // ถ้ามี TileBag ในซีน ใช้ค่าจริงจากถุง (TotalInitial = base + extraTiles)
        if (TileBag.Instance != null)
            return TileBag.Instance.TotalInitial; // :contentReference[oaicite:2]{index=2}

        // ถ้าไม่มี TileBag (เช่นอยู่หน้า Shop) ให้คำนวณจาก Progress: base + extraTiles
        var so = PlayerProgressSO.Instance?.data;         // extraTiles เก็บใน Progress :contentReference[oaicite:3]{index=3}
        int extra = (so != null) ? so.extraTiles : 0;
        return baseTileBack + extra;
    }

    /* ---------- ปุ่มอัปเกรดแต่ละอย่าง ---------- */
    public void OnBuyMana()
    {
        if (manaBought >= manaUpgradeMaxTimes) { ShowMsg("อัปเกรด Mana เต็มแล้ว!"); return; }
        if (!Spend(manaUpgradeCost)) return;

        PlayerProgressSO.Instance.data.maxMana += 2;

        manaBought++;
        PlayerPrefs.SetInt(PREF_MANA, manaBought);
        PlayerProgressSO.Instance.data.manaUpCount = manaBought;   // ซิงก์ตัวนับ

        RefreshUI();
        ShowMsg("+2 Mana สำเร็จ");
    }

    public void OnBuyCardSlot()
    {
        if (slotBought >= slotUpgradeMaxTimes) { ShowMsg("อัปเกรด Slot เต็มแล้ว!"); return; }
        if (!Spend(slotUpgradeCost)) return;

        PlayerProgressSO.Instance.data.maxCardSlots += 1;

        if (CardManager.Instance != null)
            CardManager.Instance.UpgradeMaxHeldCards(PlayerProgressSO.Instance.data.maxCardSlots);

        slotBought++;
        PlayerPrefs.SetInt(PREF_SLOT, slotBought);
        PlayerProgressSO.Instance.data.slotUpCount = slotBought;   // ซิงก์ตัวนับ

        RefreshUI();
        ShowMsg("+1 Card Slot สำเร็จ");
    }

    public void OnBuyTilePack()
    {
        if (tileBought >= tileUpgradeMaxTimes) { ShowMsg("อัปเกรด TilePack เต็มแล้ว!"); return; }
        if (!Spend(tilepackCost)) return;

        PlayerProgressSO.Instance.data.extraTiles += 10;

        tileBought++;
        PlayerPrefs.SetInt(PREF_TILE, tileBought);
        PlayerProgressSO.Instance.data.tileUpCount = tileBought;   // ซิงก์ตัวนับ

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
    /* ---------- อัปเดต UI ---------- */
    string ProgressText(int bought, int max) => (bought >= max) ? "Max" : $"{bought}/{max}";

    public void RefreshUI()
    {
        if (coinText) coinText.text = $"Coins : {CurrencyManager.Instance.Coins}";

        // เปิด/ปิดปุ่มตามเพดานอัปเกรด
        if (manaBtn) manaBtn.interactable = manaBought < manaUpgradeMaxTimes;
        if (slotBtn) slotBtn.interactable = slotBought < slotUpgradeMaxTimes;
        if (tileBtn) tileBtn.interactable = tileBought < tileUpgradeMaxTimes;

        // แสดงความคืบหน้า n/MAX หรือ Max
        if (manaProgressText) manaProgressText.text = ProgressText(manaBought, manaUpgradeMaxTimes);
        if (slotProgressText) slotProgressText.text = ProgressText(slotBought, slotUpgradeMaxTimes);
        if (tileProgressText) tileProgressText.text = ProgressText(tileBought, tileUpgradeMaxTimes);

        // แสดงค่าสถานะปัจจุบัน
        var so = PlayerProgressSO.Instance?.data;
        if (so != null)
        {
            if (manaStatText) manaStatText.text = so.maxMana.ToString();         // MaxMana ปัจจุบัน
            if (slotStatText) slotStatText.text = so.maxCardSlots.ToString();    // Card Slot ปัจจุบัน
            if (tileStatText) tileStatText.text = GetTileBackCapacity().ToString();      // TileBack/Extra Tiles ปัจจุบัน
        }
    }

    /* ---------- แสดงข้อความแบบง่าย ---------- */
    void ShowMsg(string msg) => Debug.Log($"[Shop] {msg}");
    public void ShowToast(string msg) => ShowMsg(msg); 
}
