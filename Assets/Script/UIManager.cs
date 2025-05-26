using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// จัดการ UI ทั่วไป: GameWin, LevelFail, ข้อความสถานะ และการจัดการ Card Slots + Replace Mode
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject gameWinPanel;
    public GameObject levelFailPanel;

    [Header("Message Popup")]
    [SerializeField] private GameObject popupPanel;   // MessagePopup Panel
    [SerializeField] private TMP_Text messageText;    // MessageText
    [SerializeField] private float displayTime = 2f;  // เวลาแสดง popup (วินาที)
    private Coroutine hideRoutine;

    [Header("Card Slots")]
    [SerializeField] private List<Button> cardSlotButtons;  // ปุ่มคลิกใช้การ์ด/แทนการ์ด
    [SerializeField] private List<Image>  cardSlotIcons;    // ไอคอนการ์ดในช่อง

    [Header("Replace Mode")]
    [SerializeField] private Button cancelReplacementButton; // ปุ่มยกเลิกโหมดแทนการ์ด

    [Header("Replace Mode Prompt")]
    [SerializeField] private TMP_Text replaceModePromptText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        popupPanel.SetActive(false);
        // ซ่อน card slots เริ่มต้น
        foreach (var btn in cardSlotButtons) btn.gameObject.SetActive(false);
        // ตั้ง callback ปุ่มยกเลิก Replace Mode
        if (cancelReplacementButton != null)
        {
            cancelReplacementButton.onClick.RemoveAllListeners();
            cancelReplacementButton.onClick.AddListener(() =>
            {
                CardManager.Instance.CancelReplacement();
            });
            cancelReplacementButton.gameObject.SetActive(false);
        }
        replaceModePromptText.gameObject.SetActive(false);
    }

    /// <summary>แสดงหน้าชนะเกม</summary>
    public void ShowGameWin()
    {
        if (gameWinPanel != null)
            gameWinPanel.SetActive(true);
    }

    /// <summary>แสดงหน้าล้มเหลวในด่าน</summary>
    public void ShowLevelFail()
    {
        if (levelFailPanel != null)
            levelFailPanel.SetActive(true);
    }

    /// <summary>แสดงข้อความเป็น Popup</summary>
    public void ShowMessageDictionary(string message)
    {
        ShowMessage(message, displayTime);
    }

    /// <summary>แสดงข้อความนานตาม seconds ที่กำหนด (seconds<=0 จะแสดงต่อไปจนกว่าจะ HideMessage)</summary>
    public void ShowMessage(string message, float seconds)
    {
        if (hideRoutine != null) StopCoroutine(hideRoutine);

        messageText.text = message;
        popupPanel.SetActive(true);
        if (seconds > 0f)
            hideRoutine = StartCoroutine(HideAfterDelay(seconds));
    }

    /// <summary>ปิดข้อความ popup ทันที</summary>
    public void HideMessage()
    {
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        popupPanel.SetActive(false);
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        popupPanel.SetActive(false);
    }

    /// <summary>อัพเดต Card Slot UI; ถ้า replaceMode=true จะเซ็ตให้เรียก ReplaceSlot()</summary>
    public void UpdateCardSlots(List<CardData> cards, bool replaceMode = false)
    {
        // ควบคุมปุ่มยกเลิก Replace Mode
        if (cancelReplacementButton != null)
            cancelReplacementButton.gameObject.SetActive(replaceMode);

        replaceModePromptText.gameObject.SetActive(replaceMode);
        if (replaceMode)
            replaceModePromptText.text = "Chose card";
            
        for (int i = 0; i < cardSlotButtons.Count; i++)
        {
            var btn = cardSlotButtons[i];
            var icon = cardSlotIcons[i];

            if (i < cards.Count)
            {
                var data = cards[i];
                icon.sprite = data.icon;
                icon.enabled = true;
                btn.gameObject.SetActive(true);

                btn.onClick.RemoveAllListeners();
                int index = i;
                if (replaceMode)
                    btn.onClick.AddListener(() => CardManager.Instance.ReplaceSlot(index));
                else
                    btn.onClick.AddListener(() => CardManager.Instance.UseCard(index));
            }
            else
            {
                btn.gameObject.SetActive(false);
            }
        }
    }
}
