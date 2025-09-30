using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] string playSceneName = "Play"; // ชื่อซีนเล่น
    [Header("Options Panel")]
    public GameObject optionsPanel;

    [Header("Scene Names")]
    [SerializeField] private string startSceneName = "Shop"; // ซีนเริ่มเกม
    // ถ้าไม่มี last scene จะ fallback มาซีนนี้
    // ===== NEW: NewPlay Tutorial Popup =====
    [Header("NewPlay Tutorial Popup")]
    [SerializeField] private GameObject newPlayPopup;    // ใส่ Popup Root ที่คุณตั้งไว้
    [SerializeField] private Button btnYes;              // ปุ่ม Yes
    [SerializeField] private Button btnNo;               // ปุ่ม No
    const string TUT_SESSION_KEY = "TUT_ENABLE_SESSION";
    [SerializeField] private string tutorialSeenPrefKey = "SIMPLE_TUTORIAL_SEEN_V1";
    // ===== New Play: รีเซ็ตทุกอย่าง แล้วเริ่มที่ซีนเริ่มเกม =====
    void Start()
    {
        // ซ่อน popup ตอนเข้าเมนู
        if (newPlayPopup) newPlayPopup.SetActive(false);

        // ผูกปุ่ม ถ้าลืมผูกใน Inspector จะผูกให้อัตโนมัติ
        if (btnYes)
        {
            btnYes.onClick.RemoveAllListeners();
            btnYes.onClick.AddListener(() => OnConfirmNewPlay(true));
        }
        if (btnNo)
        {
            btnNo.onClick.RemoveAllListeners();
            btnNo.onClick.AddListener(() => OnConfirmNewPlay(false));
        }
    }
    public void OnNewPlayClicked()
    {
        Time.timeScale = 1f;
        if (newPlayPopup)
        {
            newPlayPopup.SetActive(true);
        }
        else
        {
            // ถ้าไม่ได้ตั้ง popup ไว้ ให้ fallback เป็นเริ่มแบบไม่มี tutorial
            OnConfirmNewPlay(false);
        }
    }

    // ===== Continue: ไปซีนล่าสุดที่เคยอยู่ พร้อม progress เดิม =====
    public void OnContinueButtonClicked()
    {
        PlayerProgressSO.Instance?.LoadFromPrefs();
        string last = PlayerProgressSO.Instance?.GetLastSceneOrDefault(startSceneName) ?? startSceneName;
        if (SceneTransitioner.I != null) SceneTransitioner.LoadScene(last);
        else SceneManager.LoadScene(last);
    }

    public void OnHistoryButtonClicked()
    {
        SceneManager.LoadScene("UIHistoryScene");
    }
    public void Btn_QuitGame()
    {
        SfxPlayer.Play(SfxId.UI_Click);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnOptionsButtonClicked() => optionsPanel.SetActive(true);
    public void OnCloseOptionsClicked() => optionsPanel.SetActive(false);

    private void DestroyIfExists(GameObject obj) { if (obj) Destroy(obj); }
        // เรียกจากปุ่ม Yes/No ใน Popup
    void OnConfirmNewPlay(bool withTutorial)
    {
        if (newPlayPopup) newPlayPopup.SetActive(false);

        // เซ็ต “โหมดทิวทอเรียลสำหรับเซสชันนี้”
        PlayerPrefs.SetInt(TUT_SESSION_KEY, withTutorial ? 1 : 0);

        // กัน one-time tutorial โผล่เองเมื่อเลือก No
        if (withTutorial)
            PlayerPrefs.DeleteKey(tutorialSeenPrefKey);  // ให้รันใหม่ได้
        else
            PlayerPrefs.SetInt(tutorialSeenPrefKey, 1);  // ถือว่าเคยดูแล้ว

        PlayerPrefs.Save();

        // --- เคลียร์ของค้าง + รีเซ็ตโปรเกรส/เหรียญ เหมือนเดิม ---
        DestroyIfExists(CardManager.Instance?.gameObject);
#if UNITY_2023_1_OR_NEWER
        DestroyIfExists(UnityEngine.Object.FindFirstObjectByType<ShopManager>()?.gameObject);
#else
        DestroyIfExists(FindObjectOfType<ShopManager>()?.gameObject);
#endif
        if (PlayerProgressSO.Instance != null)
        {
            PlayerProgressSO.Instance.ResetProgress();
            PlayerProgressSO.Instance.ClearLastScene();
        }
        CurrencyManager.Instance?.ResetToStart();

        // เข้า “ซีนเริ่มเกม” (Shop)
        if (SceneTransitioner.I != null) SceneTransitioner.LoadScene(startSceneName);
        else SceneManager.LoadScene(startSceneName);
    }
}
