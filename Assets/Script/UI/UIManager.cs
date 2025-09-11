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
    [SerializeField] private List<Image> cardSlotIcons;   // ไอคอนการ์ดในช่อง

    [Header("Replace Mode")]
    [SerializeField] private Button cancelReplacementButton;
    [SerializeField] private TMP_Text replaceModePromptText;
    // === Defer apply เพื่อกันไอคอนโผล่ก่อนแอนิเมชันบินจบ ===
    private UICardSelect _uiSelectCached;   // <-- ไม่มี UI.
    private Coroutine _applyPendingCo;
    private List<CardData> _pendingCards;
    private bool _pendingReplaceMode;

    private UICardSelect GetSelect()
    {
        if (_uiSelectCached == null)
            _uiSelectCached = FindObjectOfType<UICardSelect>(true); // หาแม้ inactive
        return _uiSelectCached;
    }

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
    public void ShowGameWin() { if (gameWinPanel != null) gameWinPanel.SetActive(true); }
    /// <summary>แสดงหน้าล้มเหลวในด่าน</summary>
    public void ShowLevelFail() { if (levelFailPanel != null) levelFailPanel.SetActive(true); }

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

        // ⭐ บังคับเปิดสายพาเรนต์/CanvasGroup ให้แน่ใจว่า UI โหมดแทนที่มองเห็น
        if (replaceMode) ForceShowReplaceUI();

        // ปุ่มยกเลิก Replace + prompt
        if (cancelReplacementButton != null)
            cancelReplacementButton.gameObject.SetActive(replaceMode);

        if (replaceModePromptText != null)
        {
            replaceModePromptText.gameObject.SetActive(replaceMode);
            if (replaceMode) replaceModePromptText.text = "Chose card";
        }

        var sel = GetSelect();
        bool busy = sel != null && (sel.IsOpen || sel.HasActiveClone || sel.IsAnimating);

        // ⭐ แก้หลัก: "หน่วงเฉพาะตอนที่ไม่ใช่โหมดแทนที่"
        if (busy && !replaceMode)
        {
            _pendingCards = new List<CardData>(cards);
            _pendingReplaceMode = false;
            if (_applyPendingCo != null) StopCoroutine(_applyPendingCo);
            _applyPendingCo = StartCoroutine(ApplySlotsWhenSafe());
            return;
        }

        // โหมดแทนที่ให้ "อัปเดตทันที" เพื่อผูกปุ่ม ReplaceSlot(index)
        ApplySlotsImmediate(cards, replaceMode);
    }
    private IEnumerator ApplySlotsWhenSafe()
    {
        var sel = GetSelect();
        while (sel != null && (sel.IsOpen || sel.HasActiveClone || sel.IsAnimating))
            yield return null;                 // รอจน UI เลือกการ์ดปิด/จบอนิเมชัน

        yield return new WaitForEndOfFrame();  // เผื่อ layout รีเฟรช

        if (_pendingCards != null)
            ApplySlotsImmediate(_pendingCards, _pendingReplaceMode);

        _pendingCards = null;
        _applyPendingCo = null;
    }

    void ApplySlotsImmediate(List<CardData> cards, bool replaceMode)
    {
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
            var btn = cardSlotButtons[i];
            var icon = (i < cardSlotIcons.Count) ? cardSlotIcons[i] : null;
            var hover = btn ? btn.GetComponent<CardSlotUI>() : null;
            if (btn == null || icon == null) continue;

            int index = i;
            if (i < cards.Count && cards[i] != null)
            {
                var data = cards[i];

                // กราฟิก
                icon.sprite = data.icon;
                icon.enabled = true;
                btn.gameObject.SetActive(true);

                // Hover / meta
                if (hover != null)
                {
                    hover.cardInSlot = data;
                    hover.slotIndex = index;
                }

                // Drag helper (มีอยู่เดิม)
                var drag = icon.GetComponent<CardDraggable>();
                if (drag == null) drag = icon.gameObject.AddComponent<CardDraggable>();
                drag.SetData(index, data);

                // Click
                btn.onClick.RemoveAllListeners();
                if (replaceMode) btn.onClick.AddListener(() => CardManager.Instance?.ReplaceSlot(index));
                else btn.onClick.AddListener(() => CardManager.Instance?.UseCard(index));
            }
            else
            {
                // ช่องว่าง
                btn.gameObject.SetActive(false);
                if (hover != null)
                {
                    hover.cardInSlot = null;
                    hover.slotIndex = index;
                }
                var drag = icon.GetComponent<CardDraggable>();
                if (drag != null) drag.SetData(index, null);
            }
        }
    }
    // ===== Force show helpers =====
    void ForceShowTransform(Transform t)
    {
        // เปิดขึ้นมาจนถึง Canvas เพื่อกันกรณีพาเรนต์ถูกซ่อน/ปิดไว้
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

            var cg = t.GetComponent<CanvasGroup>();
            if (cg)
            {
                if (cg.alpha < 1f) cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable   = true;
            }

            if (t.GetComponent<Canvas>()) break;
            t = t.parent;
        }
    }

    void ForceShowReplaceUI()
    {
        if (cancelReplacementButton)
            ForceShowTransform(cancelReplacementButton.transform);

        if (replaceModePromptText)
            ForceShowTransform(replaceModePromptText.transform);

        // เผื่อกลุ่ม Card Slots ถูกปิดไว้ ตั้งแต่ container ขึ้นไปถึง Canvas
        if (cardSlotButtons != null)
        {
            foreach (var btn in cardSlotButtons)
                if (btn) { ForceShowTransform(btn.transform); break; } // เอาต้นหนึ่งต้นก็พอ
        }
    }
    }
