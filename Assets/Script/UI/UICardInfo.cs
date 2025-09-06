using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// หน้าต่างข้อมูลการ์ดที่โชว์ทางขวา (ภาพ/ชื่อ/คำอธิบาย/ค่าสถานะการใช้ต่อเทิร์น)
/// </summary>
[DisallowMultipleComponent]
public class UICardInfo : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;     // Panel หลัก
    [SerializeField] private Image      iconImg;
    [SerializeField] private TMP_Text   nameText;
    [SerializeField] private TMP_Text   descText;
    [SerializeField] private TMP_Text   ManaText;
    [SerializeField] private TMP_Text   UseText;

    public static UICardInfo Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }
        if (root != null) root.SetActive(false);
    }

    /// <summary>เปิดหน้าต่างข้อมูลของการ์ด</summary>
    public void Show(CardData data)
    {
        if (data == null || root == null) return;

        if (iconImg)  iconImg.sprite  = data.icon;
        if (nameText) nameText.text   = data.displayName;
        if (descText) descText.text   = data.description;
        if (ManaText) ManaText.text   = $"Cost : {data.Mana} Mana";

        int used = 0;
        int max  = data.maxUsagePerTurn;

        // กัน NRE ถ้า TurnManager ยังไม่พร้อม
        if (TurnManager.Instance != null)
            used = TurnManager.Instance.GetUsageCount(data);

        if (UseText)
        {
            UseText.text = (max <= 0)
                ? $"Use : {used}/∞"
                : $"Use : {used}/{max}";
        }

        root.SetActive(true);
    }

    /// <summary>ซ่อนหน้าต่างข้อมูล</summary>
    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }
}
