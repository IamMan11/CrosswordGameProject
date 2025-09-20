using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Options Panel")]
    public GameObject optionsPanel;  // ชี้ไปที่ Panel ตั้งค่า
    [Header("Scene Names")]
    [SerializeField] private string playSceneName = "Shop";   // ตั้งชื่อซีนที่ต้องการ


    // เรียกตอนกดปุ่ม Play (เริ่มเกมใหม่): รีเซ็ตข้อมูลทั้งหมดแล้วโหลด Scene เกมหลัก
    public void OnPlayButtonClicked()
    {
        // ทำลาย GameObject ของ Managers ที่ไม่ถูกทำลายข้าม Scene
        DestroyIfExists(CardManager.Instance?.gameObject);
#if UNITY_2023_1_OR_NEWER
        DestroyIfExists(UnityEngine.Object.FindFirstObjectByType<ShopManager>()?.gameObject);
#else
        DestroyIfExists(FindObjectOfType<ShopManager>()?.gameObject);
#endif
        DestroyIfExists(CurrencyManager.Instance?.gameObject);
        DestroyIfExists(TurnManager.Instance?.gameObject);
        DestroyIfExists(TileBag.Instance?.gameObject);

        // รีเซ็ตค่าใน ScriptableObject หรือ Manager ที่เก็บข้อมูลหลัก
        if (PlayerProgressSO.Instance != null)
            PlayerProgressSO.Instance.ResetProgress();

        // โหลด Scene เกมหลัก (เปลี่ยนชื่อ Scene ตามจริง)
        SceneManager.LoadScene("Shop");
    }
    public void OnNewPlayClicked()
    {
        // ---- ทำความสะอาดของค้างข้ามซีน (ถ้ามี) ----
        DestroyIfExists(CardManager.Instance?.gameObject);

        #if UNITY_2023_1_OR_NEWER
        DestroyIfExists(UnityEngine.Object.FindFirstObjectByType<ShopManager>()?.gameObject);
        #else
        var shopMgr = FindObjectOfType<ShopManager>();
        if (shopMgr) Destroy(shopMgr.gameObject);
        #endif

        // ---- โหลดซีนผ่านทรานซิชัน (ถ้ามี) ----
        if (SceneTransitioner.I != null)
            SceneTransitioner.LoadScene(playSceneName); // fade ดำ → โหลด → เผยจากกลางจอ
        else
            SceneManager.LoadScene(playSceneName);       // fallback ถ้ายังไม่ได้วาง _SceneTransitioner
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
    public void OnCloseOptionsClicked() => optionsPanel.SetActive(false);

    private void DestroyIfExists(GameObject obj) { if (obj) Destroy(obj); }

}
