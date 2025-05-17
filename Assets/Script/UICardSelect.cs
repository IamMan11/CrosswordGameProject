using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// แสดง Popup ให้ผู้เล่น “เลือก 1 ใน 3 การ์ด”
/// - ใส่ลง Canvas แล้วลาก Panel / Buttons ให้ครบใน Inspector
/// - ปุ่ม 3 ปุ่มต้องมี <TMP_Text> สำหรับชื่อ หรือจะใส่ Icon/Image เพิ่มเองก็ได้
/// </summary>
public class UICardSelect : MonoBehaviour
{
    [Header("Root Panel (SetActive)")]
    [SerializeField] GameObject panel;

    [Header("Card Buttons (3)")]
    [SerializeField] List<Button> cardButtons;   // ลาก 3 ปุ่มมาเรียงตามตำแหน่ง

    [Header("Text (Optional)")]
    [SerializeField] List<TMP_Text> cardNames;   // ถ้าอยากโชว์ชื่อการ์ดบนปุ่ม

    Action<CardData> onPicked;                   // callback กลับไปหา CardManager

    void Awake() => panel.SetActive(false);

    /// <summary>เปิด popup พร้อมตัวเลือก</summary>
    public void Open(List<CardData> options, Action<CardData> _onPicked)
    {
        Debug.Log("[UICardSelect] เปิด CardPanel – ผู้เล่นกำลังเลือกการ์ด");
        onPicked = _onPicked;

        // เซ็ตข้อมูลปุ่ม
        for (int i = 0; i < cardButtons.Count; i++)
        {
            bool active = i < options.Count;
            cardButtons[i].gameObject.SetActive(active);

            if (!active) continue;

            CardData data = options[i];
            if (i < cardNames.Count && cardNames[i] != null)
                cardNames[i].text = data.displayName;

            // ป้องกัน Event ซ้ำ
            cardButtons[i].onClick.RemoveAllListeners();
            cardButtons[i].onClick.AddListener(() => Pick(data));
        }

        panel.SetActive(true);
    }

    /* ---------- INTERNAL ---------- */
    void Pick(CardData picked)
    {
        panel.SetActive(false);
        onPicked?.Invoke(picked);
    }
}
