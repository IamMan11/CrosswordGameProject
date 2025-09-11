using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// จัดการ UI ทั่วไป: GameWin, LevelFail, ข้อความสถานะ
/// การจัดการ Card Slots + Replace Mode
/// และ (ใหม่) Garbled IT UI ของด่าน 1 / ตัวช่วยแสดงสถานะสามเหลี่ยมของด่าน 2
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

    // ===== NEW: Level 1 – Garbled IT UI =====
    [Header("Level 1 – Garbled IT UI")]
    [SerializeField] private GameObject garbledPanel;         // แผงกรอกคำ IT
    [SerializeField] private TMP_InputField garbledInput;     // ช่องกรอกคำ
    [SerializeField] private Button garbledSubmitButton;      // ปุ่มยืนยันเดา

    // ===== NEW: Level 2 – Triangle hint (optional label) =====
    [Header("Level 2 – Triangle Objective (optional)")]
    [SerializeField] private TMP_Text triangleHintText;       // ถ้ามี: โชว์ Connected/Not connected

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (popupPanel != null) popupPanel.SetActive(false);

        // ซ่อน card slots เริ่มต้น (กัน NPE ถ้า list ไม่ได้เซ็ต)
        if (cardSlotButtons != null)
            foreach (var btn in cardSlotButtons) if (btn) btn.gameObject.SetActive(false);

        // ตั้ง callback ปุ่มยกเลิก Replace Mode
        if (cancelReplacementButton != null)
        {
            cancelReplacementButton.onClick.RemoveAllListeners();
            cancelReplacementButton.onClick.AddListener(() =>
            {
                if (CardManager.Instance != null)
                    CardManager.Instance.CancelReplacement();
            });
            cancelReplacementButton.gameObject.SetActive(false);
        }
        if (replaceModePromptText != null)
            replaceModePromptText.gameObject.SetActive(false);

        // ===== wire Garbled IT UI =====
        if (garbledPanel != null) garbledPanel.SetActive(false);
        if (garbledSubmitButton != null)
        {
            garbledSubmitButton.onClick.RemoveAllListeners();
            garbledSubmitButton.onClick.AddListener(SubmitGarbledGuess);
        }

        // triangle hint label ซ่อนก่อน (ไม่จำเป็นต้องมี)
        if (triangleHintText != null) triangleHintText.gameObject.SetActive(false);
    }

    /// <summary>แสดงหน้าชนะเกม</summary>
    public void ShowGameWin()
    {
        if (gameWinPanel != null) gameWinPanel.SetActive(true);
    }

    /// <summary>แสดงหน้าล้มเหลวในด่าน</summary>
    public void ShowLevelFail()
    {
        if (levelFailPanel != null) levelFailPanel.SetActive(true);
    }

    /// <summary>แสดงข้อความเป็น Popup ระยะสั้น</summary>
    public void ShowMessageDictionary(string message)
    {
        ShowMessage(message, displayTime);
    }

    /// <summary>แสดงข้อความนานตาม seconds ที่กำหนด (seconds<=0 จะแสดงต่อไปจนกว่าจะ HideMessage)</summary>
    public void ShowMessage(string message, float seconds)
    {
        if (messageText == null || popupPanel == null) return;

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
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    /// <summary>อัพเดต Card Slot UI; ถ้า replaceMode=true จะเซ็ตให้เรียก ReplaceSlot()</summary>
    public void UpdateCardSlots(List<CardData> cards, bool replaceMode = false)
    {
        if (cardSlotButtons == null || cardSlotIcons == null) return;

        // ควบคุมปุ่มยกเลิก Replace Mode
        if (cancelReplacementButton != null)
            cancelReplacementButton.gameObject.SetActive(replaceMode);

        if (replaceModePromptText != null)
        {
            replaceModePromptText.gameObject.SetActive(replaceMode);
            if (replaceMode) replaceModePromptText.text = "Chose card";
        }

        for (int i = 0; i < cardSlotButtons.Count; i++)
        {
            var btn   = cardSlotButtons[i];
            var icon  = (i < cardSlotIcons.Count) ? cardSlotIcons[i] : null;
            if (btn == null || icon == null) continue;

            var hover = btn.GetComponent<CardSlotUI>();
            int index = i; // ✅ ประกาศก่อนใช้

            if (cards != null && i < cards.Count && cards[i] != null)
            {
                var data = cards[i];

                // กราฟิก
                icon.sprite  = data.icon;
                icon.enabled = true;
                btn.gameObject.SetActive(true);

                // ใส่ข้อมูลให้ Slot (สำหรับ hover และ drop)
                if (hover != null)
                {
                    hover.cardInSlot = data;
                    hover.slotIndex  = index;
                }

                // 🆕 ผูกตัวลาก
                var drag = icon.GetComponent<CardDraggable>();
                if (drag == null) drag = icon.gameObject.AddComponent<CardDraggable>();
                drag.SetData(index, data); // ให้รู้ว่าอยู่ช่องไหนและเป็นการ์ดอะไร

                // คลิก (ยังใช้ได้ตามเดิม)
                btn.onClick.RemoveAllListeners();
                if (replaceMode)
                    btn.onClick.AddListener(() => CardManager.Instance.ReplaceSlot(index));
                else
                    btn.onClick.AddListener(() => CardManager.Instance.UseCard(index));
            }
            else
            {
                btn.gameObject.SetActive(false);
                if (hover != null)
                {
                    hover.cardInSlot = null;
                    hover.slotIndex  = index; // เผื่อกรณี drop ใส่ช่องว่าง
                }
                var drag = icon.GetComponent<CardDraggable>();
                if (drag != null) drag.SetData(index, null);
            }
        }
    }

    // ======== NEW: Garbled IT UI controls (Lv1) ========

    /// <summary>ให้ LevelManager เรียกเพื่อเปิด/ปิดแผงเดาคำ IT ในด่าน 1</summary>
    public void ShowGarbledUI(bool show)
    {
        if (garbledPanel != null) garbledPanel.SetActive(show);
        if (show && garbledInput != null) garbledInput.text = "";
    }

    /// <summary>กดปุ่มยืนยันเดาคำ IT (เรียกจากปุ่มในแผง)</summary>
    public void SubmitGarbledGuess()
    {
        if (LevelManager.Instance == null) return;

        string guess = garbledInput != null ? garbledInput.text : "";
        if (string.IsNullOrWhiteSpace(guess))
        {
            ShowMessage("พิมพ์คำ IT ที่คิดว่าเจอแล้วกด Confirm", 1.2f);
            return;
        }

        bool ok = LevelManager.Instance.Level1_SubmitFixGuess(guess);
        if (ok)
        {
            ShowMessage($"✔ \"{guess}\" ถูกต้อง! หยุดสลับชั่วคราว", 2f);
            if (garbledInput) garbledInput.text = "";
        }
        else
        {
            ShowMessage($"✖ \"{guess}\" ไม่ใช่คำ IT", 1.5f);
        }
    }

    // ======== NEW: Triangle hint (Lv2) – optional ========

    /// <summary>เปิด/ปิด label แสดงสถานะ Triangle objective (ถ้าผูกไว้)</summary>
    public void SetTriangleHintVisible(bool show)
    {
        if (triangleHintText == null) return;
        triangleHintText.gameObject.SetActive(show);
    }

    /// <summary>อัพเดตข้อความ/สีของ Triangle objective (ถ้าอยากใช้เป็น indicator คงที่)</summary>
    public void UpdateTriangleHint(bool connected)
    {
        if (triangleHintText == null) return;
        triangleHintText.gameObject.SetActive(true);
        triangleHintText.text  = connected ? "Triangle: Connected" : "Triangle: Not connected";
        triangleHintText.color = connected ? new Color32(0,180,60,255) : new Color32(220,60,40,255);
    }
}
