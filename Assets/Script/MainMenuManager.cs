using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Options Panel")]
    public GameObject optionsPanel;

    [Header("Scene Names")]
    [SerializeField] private string startSceneName = "Shop"; // ซีนเริ่มเกม
    // ถ้าไม่มี last scene จะ fallback มาซีนนี้

    // ===== New Play: รีเซ็ตทุกอย่าง แล้วเริ่มที่ซีนเริ่มเกม =====
    public void OnNewPlayClicked()
    {
        // ทำลายตัวที่ค้างข้ามซีน (ถ้ามี)
        DestroyIfExists(CardManager.Instance?.gameObject);
#if UNITY_2023_1_OR_NEWER
        DestroyIfExists(UnityEngine.Object.FindFirstObjectByType<ShopManager>()?.gameObject);
#else
        DestroyIfExists(FindObjectOfType<ShopManager>()?.gameObject);
#endif
        DestroyIfExists(CurrencyManager.Instance?.gameObject);
        DestroyIfExists(TurnManager.Instance?.gameObject);
        DestroyIfExists(TileBag.Instance?.gameObject);

        // รีเซ็ต progress + last scene
        if (PlayerProgressSO.Instance != null)
        {
            PlayerProgressSO.Instance.ResetProgress();
            PlayerProgressSO.Instance.ClearLastScene();
        }
        CurrencyManager.Instance?.ResetToStart();

        // โหลดซีนเริ่มเกม
        if (SceneTransitioner.I != null) SceneTransitioner.LoadScene(startSceneName);
        else SceneManager.LoadScene(startSceneName);
    }

    // ===== Continue: ไปซีนล่าสุดที่เคยอยู่ พร้อม progress เดิม =====
    public void OnContinueButtonClicked()
    {
        // ให้แน่ใจว่าโหลด progress มาก่อน (เผื่อเปิดเกมใหม่)
        PlayerProgressSO.Instance?.LoadFromPrefs();

        string last = PlayerProgressSO.Instance?.GetLastSceneOrDefault(startSceneName) ?? startSceneName;

        if (SceneTransitioner.I != null) SceneTransitioner.LoadScene(last);
        else SceneManager.LoadScene(last);
    }

    public void OnHistoryButtonClicked()
    {
        SceneManager.LoadScene("UIHistoryScene");
    }

    public void OnOptionsButtonClicked() => optionsPanel.SetActive(true);
    public void OnCloseOptionsClicked()  => optionsPanel.SetActive(false);

    private void DestroyIfExists(GameObject obj) { if (obj) Destroy(obj); }
}
