using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// จัดการ UI ทั่วไป: GameWin, LevelFail และข้อความสถานะ
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject gameWinPanel;
    public GameObject levelFailPanel;

    [Header("References")]
    [SerializeField] private GameObject popupPanel;       // MessagePopup Panel
    [SerializeField] private TMP_Text messageText;        // MessageText

    [Header("Settings")]
    [SerializeField] private float displayTime = 2f;      // เวลาแสดง popup (วินาที)
    private Coroutine hideRoutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        popupPanel.SetActive(false);
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

    /// <summary>
    /// แสดงข้อความเป็น Popup
    /// </summary>
    public void ShowMessage(string message)
    {
        // ถ้ามี Coroutine กำลังรอซ่อนอยู่ ให้หยุดก่อน
        if (hideRoutine != null) StopCoroutine(hideRoutine);

        // ตั้งข้อความ
        messageText.text = message;
        // แสดง Panel
        popupPanel.SetActive(true);
        // สั่งให้ซ่อนเมื่อครบเวลา
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayTime);
        popupPanel.SetActive(false);
    }
}