using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class ShopManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text coinText;          // ลากมาผูก
    public int manaUpgradeCost  = 50;
    public int slotUpgradeCost  = 75;
    public int tilepackCost     = 40;

    void Start() => RefreshUI();

    /* -------- ปุ่ม Back ไปเมนูหลัก -------- */
    public void OnPlayPressed() => SceneManager.LoadScene("Try");

    /* --------- ปุ่มอัปเกรดแต่ละอย่าง --------- */
    bool TryGetManagers()
    {
        if (TurnManager.Instance == null || CardManager.Instance == null || TileBag.Instance == null)
        {
            UIManager.Instance.ShowMessage("ยังไม่พบ GameManagers ในซีนนี้", 2);
            return false;
        }
        return true;
    }
    public void OnBuyMana()
    {
        if (Spend(manaUpgradeCost))
            PlayerProgressSO.Instance.data.maxMana += 2;
    }

    public void OnBuyCardSlot()
    {
        if (Spend(slotUpgradeCost))
            PlayerProgressSO.Instance.data.maxCardSlots += 1;
    }

    public void OnBuyTilePack()
    {
        if (Spend(tilepackCost))
            PlayerProgressSO.Instance.data.extraTiles += 10;
    }

    /* ------------- helper -------------- */
    bool Spend(int cost)
    {
        if (!CurrencyManager.Instance.Spend(cost))
        {
            UIManager.Instance.ShowMessage("เหรียญไม่พอ", 2);
            return false;
        }
        RefreshUI();
        return true;
    }

    void RefreshUI()
    {
        coinText.text = $"Coins : {CurrencyManager.Instance.Coins}";
    }
}
