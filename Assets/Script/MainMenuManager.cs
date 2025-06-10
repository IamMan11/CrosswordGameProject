using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Options Panel")]
    public GameObject optionsPanel;  // ชี้ไปที่ Panel ตั้งค่า

    // เรียกตอนกดปุ่ม Play (เริ่มเกมใหม่): รีเซ็ตข้อมูลทั้งหมดแล้วโหลด Scene เกมหลัก
    public void OnPlayButtonClicked()
    {
        // ทำลาย GameObject ของ Managers ที่ไม่ถูกทำลายข้าม Scene
        DestroyIfExists(CardManager.Instance?.gameObject);
        DestroyIfExists(FindObjectOfType<ShopManager>()?.gameObject);
        DestroyIfExists(CurrencyManager.Instance?.gameObject);
        DestroyIfExists(TurnManager.Instance?.gameObject);
        DestroyIfExists(TileBag.Instance?.gameObject);

        // รีเซ็ตค่าใน ScriptableObject หรือ Manager ที่เก็บข้อมูลหลัก
        if (PlayerProgressSO.Instance != null)
            PlayerProgressSO.Instance.ResetProgress();

        // โหลด Scene เกมหลัก (เปลี่ยนชื่อ Scene ตามจริง)
        SceneManager.LoadScene("Shop");
    }

    // เรียกตอนกดปุ่ม Continue (เล่นต่อ): ไม่รีเซ็ตข้อมูล, โหลด Scene เกมหลัก
    public void OnContinueButtonClicked()
    {
        SceneManager.LoadScene("Try");
    }

    public void OnHistoryButtonClicked()
    {
        SceneManager.LoadScene("UIHistoryScene");
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

    // ช่วยเมธอด: ถ้ามี GameObject อยู่ ก็ทำการทำลาย
    private void DestroyIfExists(GameObject obj)
    {
        if (obj != null)
            Destroy(obj);
    }
}
