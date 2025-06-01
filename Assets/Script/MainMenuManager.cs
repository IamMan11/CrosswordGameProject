using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Options Panel")]
    public GameObject optionsPanel;  // ชี้ไปที่ Panel ตั้งค่า

    // เรียกตอนกดปุ่ม Play
    public void OnPlayButtonClicked()
    {
        // สมมติว่าชื่อ Scene เกมหลักคือ "GameScene"
        SceneManager.LoadScene("Shop");
    }

    // เรียกตอนกดปุ่ม Options
    public void OnOptionsButtonClicked()
    {
        optionsPanel.SetActive(true);
    }

    // เรียกตอนคลิกปุ่มปิดใน Options Panel
    public void OnCloseOptionsClicked()
    {
        optionsPanel.SetActive(false);
    }

    // ถ้าต้องการ อาจเพิ่มเมธอดสำหรับ Reset ค่าต่างๆ ใน Options
}
