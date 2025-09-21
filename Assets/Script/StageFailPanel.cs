using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageFailPanel : MonoBehaviour
{
    public static StageFailPanel Instance { get; private set; }

    [Header("Wiring")]
    [SerializeField] CanvasGroup cg;      // CanvasGroup ของแผง StageFail (อยู่ใต้ Canvas หลัก)
    [SerializeField] TMP_Text titleText;  // ข้อความหัวเรื่อง เช่น "Stage Fail"
    [SerializeField] TMP_Text detailText; // ข้อความย่อย (ออปชัน)
    [SerializeField] Button btnReturn;    // ปุ่ม Return to MainMenu

    [Header("Options")]
    [SerializeField] float fadeIn  = 0.18f;

    [SerializeField] float fadeOut = 0.12f;
    [SerializeField] string mainMenuSceneName = "Mainmenu"; // <- ตั้งชื่อซีนให้ถูกต้อง

    void Awake()
    {
        // ซิงเกิลตันเฉพาะซีนนี้ (ไม่ DontDestroy)
        Instance = this;

        if (btnReturn) btnReturn.onClick.AddListener(OnClickReturn);

        gameObject.SetActive(false);
        if (cg) cg.alpha = 0f;
    }

    // StageFailPanel.cs
    void OnClickReturn()
    {
        Hide();

        // เคลียร์สถานะเสียง/พักเกมให้ปลอดภัย
        PauseManager.I?.ClosePause();
        BgmPlayer.I?.DuckAndStop(0.12f);
        SfxPlayer.I?.StopAllAndClearBank();

        // ใช้ชื่อจาก StageFailPanel ก่อน ถ้าเว้นว่างค่อยไปใช้ของ PauseManager
        string scene = !string.IsNullOrEmpty(mainMenuSceneName)
            ? mainMenuSceneName
            : (PauseManager.I != null ? PauseManager.I.mainMenuSceneName : null);

        if (!string.IsNullOrEmpty(scene))
        {
            if (SceneTransitioner.I != null)
                SceneTransitioner.LoadScene(scene);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene); // fallback ถ้าไม่มี SceneTransitioner
        }
        else
        {
            Debug.LogError("[StageFailPanel] MainMenu scene name is empty.");
        }
    }


    public void Show(string title = "Stage Fail", string reason = "")
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (titleText)  titleText.text = title;
        if (detailText) detailText.text = reason;

        if (cg) { cg.blocksRaycasts = true; cg.interactable = true; } // รับคลิก
        StopAllCoroutines();
        StartCoroutine(FadeTo(1f, fadeIn));
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        if (cg) { cg.blocksRaycasts = false; cg.interactable = false; } // ปิดคลิกตอนซ่อน
        StopAllCoroutines();
        StartCoroutine(FadeOutThenDisable());
    }

    System.Collections.IEnumerator FadeTo(float target, float dur)
    {
        if (!cg) yield break;
        float t = 0f, start = cg.alpha;
        dur = Mathf.Max(0.0001f, dur);
        while (t < 1f)
        {
            if (!PauseManager.IsPaused) t += Time.unscaledDeltaTime / dur;
            cg.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }
        cg.alpha = target;
    }

    System.Collections.IEnumerator FadeOutThenDisable()
    {
        yield return FadeTo(0f, fadeOut);
        gameObject.SetActive(false);
    }
}
