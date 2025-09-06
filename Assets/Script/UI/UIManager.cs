using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// จัดการ UI ทั่วไป: ชนะ/แพ้ด่าน, ข้อความสถานะ, และ Card Slots + โหมด Replace
/// </summary>
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject gameWinPanel;
    public GameObject levelFailPanel;

    [Header("Message Popup")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float displayTime = 2f;
    private Coroutine hideRoutine;

    [Header("Card Slots")]
    [SerializeField] private List<Button> cardSlotButtons; // ปุ่มกดใช้/แทนที่
    [SerializeField] private List<Image>  cardSlotIcons;   // ไอคอนการ์ดในช่อง

    [Header("Replace Mode")]
    [SerializeField] private Button   cancelReplacementButton;
    [SerializeField] private TMP_Text replaceModePromptText;

    void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }

        if (popupPanel) popupPanel.SetActive(false);

        // ซ่อนช่องการ์ดเริ่มต้น
        if (cardSlotButtons != null)
            foreach (var btn in cardSlotButtons) if (btn) btn.gameObject.SetActive(false);

        // ปุ่มยกเลิก Replace
        if (cancelReplacementButton != null)
        {
            cancelReplacementButton.onClick.RemoveAllListeners();
            cancelReplacementButton.onClick.AddListener(() => CardManager.Instance?.CancelReplacement());
            cancelReplacementButton.gameObject.SetActive(false);
        }

        if (replaceModePromptText != null)
            replaceModePromptText.gameObject.SetActive(false);
    }

    /// <summary>แสดงหน้าชนะเกม</summary>
    public void ShowGameWin()      { if (gameWinPanel  != null) gameWinPanel.SetActive(true); }
    /// <summary>แสดงหน้าล้มเหลวในด่าน</summary>
    public void ShowLevelFail()    { if (levelFailPanel != null) levelFailPanel.SetActive(true); }

    /// <summary>แสดงข้อความสั้นตามค่า default</summary>
    public void ShowMessageDictionary(string message) => ShowMessage(message, displayTime);

    /// <summary>แสดงข้อความนานตาม seconds (<=0 = แสดงค้าง)</summary>
    public void ShowMessage(string message, float seconds)
    {
        if (popupPanel == null || messageText == null) { Debug.Log(message); return; }

        if (hideRoutine != null) StopCoroutine(hideRoutine);

        messageText.text = message;
        popupPanel.SetActive(true);

        if (seconds > 0f)
            hideRoutine = StartCoroutine(HideAfterDelay(seconds));
    }

    /// <summary>ปิดข้อความทันที</summary>
    public void HideMessage()
    {
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        if (popupPanel) popupPanel.SetActive(false);
    }

    IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (popupPanel) popupPanel.SetActive(false);
    }

    /// <summary>
    /// อัปเดต UI ช่องการ์ดทั้งหมด
    /// - replaceMode=true: คลิก = ReplaceSlot(index)
    /// - replaceMode=false: คลิก = UseCard(index)
    /// </summary>
    public void UpdateCardSlots(List<CardData> cards, bool replaceMode = false)
    {
        if (cards == null || cardSlotButtons == null || cardSlotIcons == null) return;
        if (cardSlotButtons.Count != cardSlotIcons.Count)
            Debug.LogWarning("[UIManager] จำนวนปุ่มและไอคอนไม่เท่ากัน");

        // ปุ่มยกเลิก Replace + prompt
        if (cancelReplacementButton != null)
            cancelReplacementButton.gameObject.SetActive(replaceMode);

        if (replaceModePromptText != null)
        {
            replaceModePromptText.gameObject.SetActive(replaceMode);
            if (replaceMode) replaceModePromptText.text = "Chose card";
        }

        int n = cardSlotButtons.Count;
        for (int i = 0; i < n; i++)
        {
            var btn  = cardSlotButtons[i];
            var icon = (i < cardSlotIcons.Count) ? cardSlotIcons[i] : null;
            var hover = btn ? btn.GetComponent<CardSlotUI>() : null;

            if (btn == null || icon == null) continue;

            int index = i;

            if (i < cards.Count && cards[i] != null)
            {
                var data = cards[i];

                // กราฟิก
                icon.sprite  = data.icon;
                icon.enabled = true;
                btn.gameObject.SetActive(true);

                // Hover / Drop info
                if (hover != null)
                {
                    hover.cardInSlot = data;
                    hover.slotIndex  = index;
                }

                // Drag helper (ถ้ามีระบบลาก)
                var drag = icon.GetComponent<CardDraggable>();
                if (drag == null) drag = icon.gameObject.AddComponent<CardDraggable>();
                drag.SetData(index, data);

                // Click
                btn.onClick.RemoveAllListeners();
                if (replaceMode)
                    btn.onClick.AddListener(() => CardManager.Instance?.ReplaceSlot(index));
                else
                    btn.onClick.AddListener(() => CardManager.Instance?.UseCard(index));
            }
            else
            {
                // ช่องว่าง
                btn.gameObject.SetActive(false);
                if (hover != null)
                {
                    hover.cardInSlot = null;
                    hover.slotIndex  = index;
                }

                var drag = icon.GetComponent<CardDraggable>();
                if (drag != null) drag.SetData(index, null);
            }
        }
    }
}
