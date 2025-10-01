using UnityEngine;

public class ShopTutorialBinder : MonoBehaviour
{
    public static ShopTutorialBinder Instance { get; private set; }

    [Header("Shop UI Roots")]
    public RectTransform panelRoot;
    public RectTransform coinText;

    [Header("Buttons")]
    public RectTransform buyMana;
    public RectTransform buyTilePack;
    public RectTransform buyMaxCard;
    public RectTransform rerollButton;
    public RectTransform nextButton;

    [Header("Items Row")]
    public RectTransform itemsRow;
    public RectTransform item1;
    public RectTransform item2;
    public RectTransform item3;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // ไม่ DontDestroyOnLoad; ให้วางไว้เฉพาะในซีน Shop
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
