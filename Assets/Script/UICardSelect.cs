using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// แสดง Popup ให้ผู้เล่น “เลือก 1 ใน 3 การ์ด” และปุ่มยกเลิก (Discard)
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

    [Header("Discard Button (Cancel)")]
    [SerializeField] Button discardButton;       // ปุ่มยกเลิกการเลือกการ์ด

    Action<CardData> onPicked;                   // callback กลับไปหา CardManager

    void Awake()
    {
        panel.SetActive(false);
        if (discardButton != null)
        {
            discardButton.onClick.RemoveAllListeners();
            discardButton.onClick.AddListener(() => {
                panel.SetActive(false);                       // ปิด Popup
                CardManager.Instance.CancelReplacement();     // ยกเลิกโหมดแทนการ์ด
            });
        }
    }
    public bool IsOpen => panel.activeSelf;

    /// <summary>เปิด popup พร้อมตัวเลือก</summary>
    public void Open(List<CardData> options, Action<CardData> _onPicked)
    {
        Debug.Log("[UICardSelect] เปิด CardPanel – ผู้เล่นกำลังเลือกการ์ด");
        onPicked = _onPicked;

        // เซ็ตข้อมูลปุ่มการ์ด
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

        // เปิดปุ่มยกเลิก
        if (discardButton != null)
            discardButton.gameObject.SetActive(true);

        panel.SetActive(true);
    }

    /* ---------- INTERNAL ---------- */
    void Pick(CardData picked)
    {
        panel.SetActive(false);
        onPicked?.Invoke(picked);
    }

    /// <summary>ยกเลิกการเลือกการ์ด</summary>
    void Cancel()
    {
        Debug.Log("[UICardSelect] ยกเลิกการเลือกการ์ด");
        panel.SetActive(false);
        // ไม่เรียก callback onPicked เพื่อยกเลิก
    }
}
