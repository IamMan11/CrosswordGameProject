using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Popup เลือกการ์ด “1 จาก 3”
/// - ปุ่ม Discard จะยกเลิกโหมดแทนที่ (เรียก CardManager.CancelReplacement)
/// </summary>
[DisallowMultipleComponent]
public class UICardSelect : MonoBehaviour
{
    [Header("Root Panel (SetActive)")]
    [SerializeField] GameObject panel;

    [Header("Card Buttons (3)")]
    [SerializeField] List<Button> cardButtons;   // ต้องมี 3 ปุ่ม

    [Header("Text (Optional)")]
    [SerializeField] List<TMP_Text> cardNames;   // ถ้ามี จะแสดงชื่อบนปุ่ม

    [Header("Discard Button (Cancel)")]
    [SerializeField] Button discardButton;

    private Action<CardData> onPicked; // callback ไป CardManager

    void Awake()
    {
        if (panel != null) panel.SetActive(false);

        if (discardButton != null)
        {
            discardButton.onClick.RemoveAllListeners();
            discardButton.onClick.AddListener(() =>
            {
                if (panel != null) panel.SetActive(false); // ปิด popup
                CardManager.Instance?.CancelReplacement(); // กลับจากโหมดแทนที่
            });
        }
    }

    /// <summary>สถานะเปิดอยู่หรือไม่</summary>
    public bool IsOpen => panel != null && panel.activeSelf;

    /// <summary>เปิด popup พร้อมตัวเลือกการ์ด</summary>
    public void Open(List<CardData> options, Action<CardData> _onPicked)
    {
        if (panel == null || cardButtons == null) return;

        onPicked = _onPicked;

        // เซ็ตปุ่มตามจำนวน options
        for (int i = 0; i < cardButtons.Count; i++)
        {
            var btn = cardButtons[i];
            if (btn == null) continue;

            bool active = (options != null && i < options.Count && options[i] != null);
            btn.gameObject.SetActive(active);

            if (!active) continue;

            var data = options[i];
            if (i < cardNames.Count && cardNames[i] != null)
                cardNames[i].text = data.displayName;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => Pick(data));
        }

        if (discardButton != null)
            discardButton.gameObject.SetActive(true);

        panel.SetActive(true);
    }

    /* ---------- INTERNAL ---------- */
    void Pick(CardData picked)
    {
        if (panel != null) panel.SetActive(false);
        onPicked?.Invoke(picked);
    }
}
