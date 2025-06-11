using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardShopSlotUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Image    icon;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Button   buyBtn;

    CardData data;

    public void Setup(CardData cd, bool owned, System.Action<CardData> onBuy)
    {
        data = cd;
        icon.sprite    = cd.icon;
        nameText.text  = cd.displayName;

        if (owned)
        {
            priceText.text   = "Owned";
            buyBtn.interactable = false;
        }
        else
        {
            priceText.text   = $"💰 {cd.price}";
            buyBtn.interactable = true;
            buyBtn.onClick.RemoveAllListeners();
            buyBtn.onClick.AddListener(() => onBuy(cd));
        }
    }
}
