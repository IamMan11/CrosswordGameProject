using UnityEngine;

public class ShopTutorialBinder : MonoBehaviour
{
    public static ShopTutorialBinder Instance { get; private set; }

    [Header("Shop Root / Panel")]
    public RectTransform panelRoot;

    [Header("HUD")]
    public RectTransform coinText;

    [Header("Upgrade buttons")]
    public RectTransform buyMana;
    public RectTransform buyTilePack;
    public RectTransform buyMaxCard;

    [Header("Items row (cards)")]
    public RectTransform itemsRow;
    public RectTransform item1;
    public RectTransform item2;
    public RectTransform item3;

    [Header("Bottom")]
    public RectTransform rerollButton;
    public RectTransform nextButton;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
}
