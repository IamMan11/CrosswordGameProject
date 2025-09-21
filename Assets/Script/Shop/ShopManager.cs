using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// ShopManager
/// - จัดการร้านค้า: อัปเกรด Mana / Card Slot / TilePack และ Reroll สินค้าที่ขายเป็นการ์ด
/// - ซิงก์สเตทกับ PlayerProgressSO / CurrencyManager / CardManager / TileBag
/// - อัปเดต UI แสดงเหรียญ, สถานะอัปเกรด (n/MAX) และค่าสถานะปัจจุบัน
/// 
/// หมายเหตุ:
/// - คงพฤติกรรมเดิม: ใช้เหรียญจาก CurrencyManager, เก็บตัวนับอัปเกรดลง PlayerPrefs
/// - เพิ่มเช็ก null ทุกซิงเกิลตัน/อ้างอิง UI เพื่อกัน NRE
/// - เอาของที่ไม่ใช้จริงออก: RerollBtn (ฟิลด์ไม่เคยถูกอ้างอิง)
/// </summary>
[DisallowMultipleComponent]
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    #region UI refs (ลากจาก Inspector)
    [Header("Upgrade UI (Texts)")]
    [SerializeField] TMP_Text manaProgressText;
    [SerializeField] TMP_Text tileProgressText;
    [SerializeField] TMP_Text slotProgressText;

    [SerializeField] TMP_Text manaPriceText;
    [SerializeField] TMP_Text tilePriceText;
    [SerializeField] TMP_Text slotPriceText;

    [Header("Upgrade Buttons")]
    [SerializeField] Button buyManaBtn;
    [SerializeField] Button buyTileBtn;
    [SerializeField] Button buySlotBtn;

    [Header("Upgrade Caps & Prices")]
    // จำนวนครั้งสูงสุดที่อัปเกรดได้ (กำหนดเองใน Inspector)
    [SerializeField] int manaMaxUpgrades = 5;
    [SerializeField] int tileMaxUpgrades = 10;
    [SerializeField] int slotMaxUpgrades = 2;

    // สูตรราคา: base + step * (จำนวนครั้งที่อัปเกรดไปแล้ว)
    [SerializeField] int manaBasePrice = 200, manaStepPrice = 25;
    [SerializeField] int tileBasePrice = 100, tileStepPrice = 30;
    [SerializeField] int slotBasePrice = 1000, slotStepPrice = 40;

    // ====== helper อ่านจำนวนครั้งที่อัปเกรด (ดึงจาก SO ถ้ามี) ======
    int ManaUpCount => PlayerProgressSO.Instance?.data?.manaUpCount ?? PlayerPrefs.GetInt("ManaUpCount", 0);
    int TileUpCount => PlayerProgressSO.Instance?.data?.tileUpCount ?? PlayerPrefs.GetInt("TileUpCount", 0);
    int SlotUpCount => PlayerProgressSO.Instance?.data?.slotUpCount ?? PlayerPrefs.GetInt("SlotUpCount", 0);
    int CalcPrice(int baseP, int step, int count) => baseP + step * count;

    [Header("UI")]
    [SerializeField] TMP_Text coinText;
    [SerializeField] Button manaBtn;
    [SerializeField] Button slotBtn;
    [SerializeField] Button tileBtn;

    [Header("UI - Current Stats")]
    [SerializeField] TMP_Text manaStatText;  // data.maxMana
    [SerializeField] TMP_Text slotStatText;  // data.maxCardSlots
    [SerializeField] TMP_Text tileStatText;  // ความจุถุงไทล์

    [Header("TileBag Base")]
    [SerializeField] int baseTileBack = 100;

    [Header("Shop Slots")]
    [SerializeField] ShopCardSlot[] shopSlots;
    [Header("Reroll")]
    [SerializeField] Button rerollButton;

    #endregion

    #region Costs / Limits

    [Header("Upgrade Cost / ครั้ง")]
    [SerializeField] int manaUpgradeCost = 200;
    [SerializeField] int slotUpgradeCost = 1000;
    [SerializeField] int tilepackCost   = 100;
    [SerializeField] int rerollCost     = 50;

    [Header("Upgrade Limits (ครั้ง)")]
    [SerializeField] int manaUpgradeMaxTimes = 5;   // +2 Mana / ครั้ง
    [SerializeField] int slotUpgradeMaxTimes = 2;   // +1 Card Slot / ครั้ง
    [SerializeField] int tileUpgradeMaxTimes = 10;  // +10 Tile / ครั้ง

    #endregion

    #region Pref Keys / Counters

    const string PREF_MANA = "CC_UPG_MANA";
    const string PREF_SLOT = "CC_UPG_SLOT";
    const string PREF_TILE = "CC_UPG_TILE";

    int manaBought, slotBought, tileBought;

    #endregion

    /* ----------------------------------------------------------- */
    #region Unity lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // โหลดจำนวนครั้งที่อัปเกรดจาก PlayerPrefs (ต่อ profile ทั้งเกม)
        manaBought = PlayerPrefs.GetInt(PREF_MANA, 0);
        slotBought = PlayerPrefs.GetInt(PREF_SLOT, 0);
        tileBought = PlayerPrefs.GetInt(PREF_TILE, 0);

        // ซิงก์ตัวเลขไปที่ ScriptableObject (เผื่อระบบอื่นอ่านไปแสดง)
        var prog = PlayerProgressSO.Instance?.data;
        if (prog != null)
        {
            prog.manaUpCount = manaBought;
            prog.slotUpCount = slotBought;
            prog.tileUpCount = tileBought;
        }
    }

    #endregion

    /* ----------------------------------------------------------- */
    #region Entry / Build Shop

    /// <summary>ตรวจความพร้อมของช่อง และสุ่มสินค้าลงสลอต (ครั้งแรก)</summary>
    void TryRerollAtStart()
    {
        if (shopSlots == null || shopSlots.Length == 0)
        {
            Debug.LogError("[Shop] shopSlots ยังไม่ถูกตั้งค่าใน Inspector");
            return;
        }
        for (int i = 0; i < shopSlots.Length; i++)
        {
            if (shopSlots[i] == null)
            {
                Debug.LogError($"[Shop] shopSlots[{i}] = null (ยังไม่ได้ลากสลอตที่ตำแหน่ง {i})");
                return;
            }
        }

        // CardManager ใช้ที่อื่น (UI ซื้อการ์ด), ถ้าไม่มีจะยัง reroll ได้ แต่แจ้งเตือน
        if (CardManager.Instance == null)
            Debug.LogWarning("[Shop] CardManager.Instance = null (ยังไม่ได้วาง CardManager ในซีน)");

        RerollShop();
    }

    /// <summary>สร้างพูลการ์ดที่ “ต้องซื้อก่อนใช้” และยังไม่เป็นเจ้าของ</summary>
    List<CardData> BuildPurchasablePool()
    {
        var all = Resources.LoadAll<CardData>("Cards");
        var pso = PlayerProgressSO.Instance;

        var pool = all.Where(c =>
                    c != null &&
                    c.requirePurchase &&
                    (pso == null || !pso.HasCard(c.id))   // ถ้า pso ว่าง ให้ถือว่ายังไม่เป็นเจ้าของ
                  ).ToList();

        return pool;
    }

    /// <summary>สุ่มการ์ดลงแต่ละสลอต (ไม่ซ้ำเท่าที่พูลเอื้อ)</summary>
    void RerollShop()
    {
        var pool = BuildPurchasablePool();

        for (int i = 0; i < shopSlots.Length; i++)
        {
            CardData pick = null;

            if (pool.Count > 0)
            {
                int idx = Random.Range(0, pool.Count);
                pick = pool[idx];
                pool.RemoveAt(idx); // กันซ้ำ
            }

            // ใส่การ์ดลงช่อง (หรือ null ถ้าพูลหมด → ช่องจะ Disable ปุ่มซื้อเอง)
            shopSlots[i].SetCard(pick);
            // ถ้าต้องการซ่อนช่องว่าง ใช้: shopSlots[i].gameObject.SetActive(pick != null);
        }
    }

    #endregion

    /* ----------------------------------------------------------- */
    #region Buttons (Scene / Reroll / Upgrades)

    /// <summary>กลับไปเล่นเกม: รีบิลด์ถุงไทล์ตามค่าอัปเกรด แล้วโหลดฉากเกมหลัก</summary>
    public void OnPlayPressed()
    {
        // รีเซ็ตพูลไทล์ให้ตรงกับค่า extraTiles ปัจจุบัน
        TileBag.Instance?.ResetPool();

        // เข้าฉากเกม
        if (SceneTransitioner.I != null) SceneTransitioner.LoadScene("Play");
    }

    /// <summary>กด Reroll — หักเหรียญ แล้วสุ่มของใหม่</summary>
    public void OnRerollPressed()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) { ShowMsg("CurrencyManager ไม่พร้อม"); return; }

        // ถ้าไม่มีอะไรเหลือให้สุ่มแล้ว → ปิดปุ่มและไม่ทำงาน
        if (BuildPurchasablePool().Count == 0)
        {
            RefreshRerollInteractable();
            ShowMsg("ไม่มีการ์ดเหลือในพูลที่ยังซื้อได้");
            return;
        }

        if (!cm.Spend(rerollCost)) { ShowMsg("เหรียญไม่พอสำหรับ reroll"); return; }
        StartCoroutine(RerollSequenceAnimated_FillWithOwned());
    }

    // เด้ง-เปลี่ยนไพ่ทีละช่อง (ซ้าย -> ขวา)
    IEnumerator RerollSequenceAnimated_FillWithOwned()
    {
        if (shopSlots == null || shopSlots.Length == 0) yield break;

        var buyable = BuildPurchasablePool();
        var owned = BuildOwnedPurchasablePool();

        for (int i = 0; i < shopSlots.Length; i++)
        {
            CardData pick = null;
            bool showAsPurchased = false;

            if (buyable.Count > 0)
            {
                int idx = Random.Range(0, buyable.Count);
                pick = buyable[idx];
                buyable.RemoveAt(idx);
                showAsPurchased = false;
            }
            else if (owned.Count > 0)
            {
                int idx = Random.Range(0, owned.Count);
                pick = owned[idx];
                // ไม่ต้องลบออกก็ได้ จะได้สุ่มซ้ำได้
                showAsPurchased = true; // ← โชว์แบบซื้อแล้ว
            }
            else
            {
                // ไม่มีอะไรให้แสดงเลย
                pick = null;
                showAsPurchased = true;
            }

            yield return shopSlots[i].AnimateSwap(pick, showAsPurchased, 0.12f, 0.14f);
            yield return new WaitForSecondsRealtime(0.05f);
        }

        RefreshUI();
        RefreshRerollInteractable();
        TutorialManager.Instance?.Fire(TutorialEvent.ShopReroll);
    }
    List<CardData> BuildOwnedPurchasablePool()
    {
        var all = Resources.LoadAll<CardData>("Cards");
        var pso = PlayerProgressSO.Instance;
        // เฉพาะใบที่ requirePurchase และ "ผู้เล่นมีแล้ว"
        var owned = all.Where(c =>
                    c != null &&
                    c.requirePurchase &&
                    (pso != null && pso.HasCard(c.id))
                ).ToList();
        return owned;
    }

    // เรียกทุกครั้งที่ต้องอัปเดตสถานะปุ่ม reroll
    void RefreshRerollInteractable()
    {
        var pool = BuildPurchasablePool();
        bool hasBuyable = pool.Count > 0;
        if (rerollButton) rerollButton.interactable = hasBuyable && (CurrencyManager.Instance?.Coins >= rerollCost);
    }

    // ใน Awake/Start/OnEnable (หลัง RefreshUI()) ให้เรียก:
    void OnEnable() { RefreshUI(); RefreshRerollInteractable(); }
    void Start() { RefreshUI(); TryRerollAtStart(); RefreshRerollInteractable(); TutorialManager.Instance?.Fire(TutorialEvent.ShopOpened); }

    // เมื่อซื้อการ์ดเสร็จจาก slot ให้เรียกเมธอดนี้ (จะเขียนไว้ด้านล่าง)
    public void OnCardPurchased(ShopCardSlot slot, CardData card)
    {
        RefreshUI();
        RefreshRerollInteractable();
        ShowToast($"ซื้อ {card.displayName} สำเร็จ");

        TutorialManager.Instance?.Fire(TutorialEvent.ShopBuy);   // NEW ✅
    }

    public void OnBuyMana()
    {
        if (manaBought >= manaUpgradeMaxTimes) { ShowMsg("อัปเกรด Mana เต็มแล้ว!"); return; }
        if (!Spend(manaUpgradeCost)) return;

        var so = PlayerProgressSO.Instance?.data;
        if (so == null) { ShowMsg("Progress ไม่พร้อม"); return; }

        so.maxMana += 2;

        manaBought++;
        PlayerPrefs.SetInt(PREF_MANA, manaBought);
        so.manaUpCount = manaBought;

        RefreshUI();

        TutorialManager.Instance?.Fire(TutorialEvent.ShopBuy);   // NEW ✅

        ShowMsg("+2 Mana สำเร็จ");
    }

    public void OnBuyCardSlot()
    {
        if (slotBought >= slotUpgradeMaxTimes) { ShowMsg("อัปเกรด Slot เต็มแล้ว!"); return; }
        if (!Spend(slotUpgradeCost)) return;

        var so = PlayerProgressSO.Instance?.data;
        if (so == null) { ShowMsg("Progress ไม่พร้อม"); return; }

        so.maxCardSlots += 1;

        // แจ้ง CardManager ให้รีเฟรชขนาดมือ
        CardManager.Instance?.UpgradeMaxHeldCards(so.maxCardSlots);

        slotBought++;
        PlayerPrefs.SetInt(PREF_SLOT, slotBought);
        so.slotUpCount = slotBought;

        RefreshUI();
        TutorialManager.Instance?.Fire(TutorialEvent.ShopBuy);
        ShowMsg("+1 Card Slot สำเร็จ");
    }

    public void OnBuyTilePack()
    {
        if (tileBought >= tileUpgradeMaxTimes) { ShowMsg("อัปเกรด TilePack เต็มแล้ว!"); return; }
        if (!Spend(tilepackCost)) return;

        var so = PlayerProgressSO.Instance?.data;
        if (so == null) { ShowMsg("Progress ไม่พร้อม"); return; }

        so.extraTiles += 10;

        tileBought++;
        PlayerPrefs.SetInt(PREF_TILE, tileBought);
        so.tileUpCount = tileBought;

        RefreshUI();
        TutorialManager.Instance?.Fire(TutorialEvent.ShopBuy);
        ShowMsg("+10 Tiles สำเร็จ");
    }

    #endregion

    /* ----------------------------------------------------------- */
    #region UI update / Helpers

    /// <summary>ข้อความความคืบหน้า (n/MAX หรือ Max)</summary>
    string ProgressText(int bought, int max) => (bought >= max) ? "Max" : $"{bought}/{max}";

    /// <summary>อัปเดตตัวเลขเหรียญ/ปุ่ม/สถานะ และค่าปัจจุบันของผู้เล่น</summary>
    public void RefreshUI()
    {
        var cm = CurrencyManager.Instance;
        if (coinText)
        {
            // ถ้าอยากมีศูนย์นำหน้า 5 หลัก ใช้ :D5 แทน
            // coinText.text = $"coin: {(cm ? cm.Coins : 0):D5}";
            coinText.text = $"coin: {(cm ? cm.Coins : 0):N0}";
        }
        int manaCount = ManaUpCount, tileCount = TileUpCount, slotCount = SlotUpCount;

        bool manaMax = manaCount >= manaMaxUpgrades;
        bool tileMax = tileCount >= tileMaxUpgrades;
        bool slotMax = slotCount >= slotMaxUpgrades;

        // ----- Progress -----
        if (manaProgressText) manaProgressText.text = manaMax ? "Max" : $"Progress : {manaCount}/{manaMaxUpgrades}";
        if (tileProgressText) tileProgressText.text = tileMax ? "Max" : $"Progress : {tileCount}/{tileMaxUpgrades}";
        if (slotProgressText) slotProgressText.text = slotMax ? "Max" : $"Progress : {slotCount}/{slotMaxUpgrades}";

        // ----- Price -----
        int manaPrice = manaMax ? 0 : CalcPrice(manaBasePrice, manaStepPrice, manaCount);
        int tilePrice = tileMax ? 0 : CalcPrice(tileBasePrice, tileStepPrice, tileCount);
        int slotPrice = slotMax ? 0 : CalcPrice(slotBasePrice, slotStepPrice, slotCount);

        if (manaPriceText) manaPriceText.text = manaMax ? "Price : —" : $"Price : {manaPrice}";
        if (tilePriceText) tilePriceText.text = tileMax ? "Price : —" : $"Price : {tilePrice}";
        if (slotPriceText) slotPriceText.text = slotMax ? "Price : —" : $"Price : {slotPrice}";

        // ----- ปุ่มซื้อ -----
        if (buyManaBtn) buyManaBtn.interactable = !manaMax && (cm == null || cm.Coins >= manaPrice);
        if (buyTileBtn) buyTileBtn.interactable = !tileMax && (cm == null || cm.Coins >= tilePrice);
        if (buySlotBtn) buySlotBtn.interactable = !slotMax && (cm == null || cm.Coins >= slotPrice);

        // สถานะปัจจุบัน
        var so = PlayerProgressSO.Instance?.data;
        if (so != null)
        {
            if (manaStatText) manaStatText.text = $"Max Mana : {so.maxMana}";
            if (slotStatText) slotStatText.text = $"Card Slot : {so.maxCardSlots}";
            if (tileStatText) tileStatText.text = $"TilePack : {GetTileBackCapacity()}";
        }
    }

    /// <summary>คำนวณความจุถุงไทล์ (ถ้ามี TileBag ใช้ค่าจริง, ไม่งั้น base + extra)</summary>
    int GetTileBackCapacity()
    {
        if (TileBag.Instance != null) return TileBag.Instance.TotalInitial;

        int extra = PlayerProgressSO.Instance?.data?.extraTiles ?? 0;
        return baseTileBack + extra;
    }

    /// <summary>พยายามหักเหรียญผ่าน CurrencyManager พร้อมข้อความแจ้งเตือนเมื่อไม่พอ</summary>
    bool Spend(int cost)
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) { ShowMsg("ระบบเหรียญไม่พร้อม"); return false; }

        if (!cm.Spend(cost))
        {
            ShowMsg("เหรียญไม่พอ");
            return false;
        }
        return true;
    }

    /* ---------- แสดงข้อความแบบง่าย ---------- */
    void ShowMsg(string msg) => Debug.Log($"[Shop] {msg}");
    public void ShowToast(string msg) => ShowMsg(msg);

    #endregion
}
