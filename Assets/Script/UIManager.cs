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

    [Header("Message UI")]
    public TMP_Text messageText;           // ลาก TextMeshProUGUI สำหรับโชว์ข้อความ
    public float messageDuration = 2f;     // เวลาคงข้อความก่อนหาย

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
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
    /// แสดงข้อความสถานะบนจอชั่วคราว
    /// </summary>
    /// <param name="msg">ข้อความที่จะโชว์</param>
    /// <param name="col">สีข้อความ (ถ้าไม่กำหนดเป็นขาว)</param>
    public void ShowMessage(string msg, Color? col = null)
    {
        if (messageText == null) return;
        messageText.text  = msg;
        messageText.color = col ?? Color.white;

        StopAllCoroutines();
        StartCoroutine(HideMessageAfterDelay());
    }

    IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDuration);
        if (messageText != null)
            messageText.text = string.Empty;
    }
}