using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>ควบคุมหน้าต่างแสดงข้อมูลการ์ดทางขวา</summary>
public class UICardInfo : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;       // ตัว Panel หลัก
    [SerializeField] private Image      iconImg;
    [SerializeField] private TMP_Text   nameText;
    [SerializeField] private TMP_Text   descText;
    [SerializeField] private TMP_Text   ManaText;

    public static UICardInfo Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        Hide();
    }

    public void Show(CardData data)
    {
        if (data == null) return;
        iconImg.sprite  = data.icon;
        nameText.text   = data.displayName;
        descText.text   = data.description;
        ManaText.text   = $"Cost : {data.Mana} Mana";
        root.SetActive(true);
    }

    public void Hide() => root.SetActive(false);
}
