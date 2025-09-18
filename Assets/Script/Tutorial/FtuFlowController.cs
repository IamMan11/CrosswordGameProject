using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FtuFlowController : MonoBehaviour
{
    public static FtuFlowController Instance { get; private set; }

    [Header("เปิด/ปิดโหมดสอน")]
    public bool forceFtu = false;   // เปิดเพื่อทดสอบเสมอ

    [Header("ลำดับฉาก (กำหนดตาม Project)")]
    public string[] sceneOrder = { "Start", "Shop", "Game", "Card", "End" };

    private int phaseIndex = 0;
    private bool isActive = false;

    const string KEY_DONE = "FTU_DONE";
    const string KEY_PHASE = "FTU_PHASE_INDEX";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        bool done = PlayerPrefs.GetInt(KEY_DONE, 0) == 1;
        if (!done || forceFtu)
        {
            isActive = true;
            phaseIndex = PlayerPrefs.GetInt(KEY_PHASE, 0);
            SceneManager.sceneLoaded += OnSceneLoaded;
            TutorialManager.OnTutorialFinished += OnPhaseFinished; // ← อาศัยอีเวนต์จาก TutorialManager (ดูข้อ 2)
        }
    }

    // Expose whether FTU flow is currently active
    public static bool IsFTUActive()
    {
        return Instance != null && Instance.isActive;
    }

    // Dev helper: reset both FTU and any TutorialManager per-scene keys
    public static void ResetFTUAndTutorials()
    {
        ResetFTU();
        // ล้าง keys ที่ TutorialManager ลงทะเบียนไว้
        try
        {
            var keys = TutorialManager.GetRegisteredKeys();
            foreach (var k in keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.DeleteKey("TUT_KEYS_REGISTRY");
            PlayerPrefs.Save();
            Debug.Log("[FTU] ResetFTUAndTutorials: cleared registered tutorial keys.");
        }
        catch { }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            TutorialManager.OnTutorialFinished -= OnPhaseFinished;
        }
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        if (!isActive) return;

        // ถ้าฉากที่โหลดตรงกับลำดับที่คาด
        if (phaseIndex < sceneOrder.Length && s.name == sceneOrder[phaseIndex])
        {
            var tm = FindFirstObjectByType<TutorialManager>();
            if (tm != null) tm.StartTutorialNow();  // ให้ฉากนี้เริ่มสอนทันที (Steps ตั้งใน Inspector ของฉากนั้น)
            else Debug.LogWarning($"[FTU] Scene '{s.name}' ไม่มี TutorialManager");
        }
    }

    void OnPhaseFinished()
    {
        if (!isActive) return;

        // จบสอนฉากนี้ → ไปฉากถัดไป
        phaseIndex++;
        PlayerPrefs.SetInt(KEY_PHASE, phaseIndex);
        PlayerPrefs.Save();

        if (phaseIndex >= sceneOrder.Length)
        {
            // ครบลูป FTU แล้ว
            isActive = false;
            PlayerPrefs.SetInt(KEY_DONE, 1);
            PlayerPrefs.Save();
            Debug.Log("[FTU] Completed first-time tutorial.");
            return;
        }

        // โหลดฉากถัดไป
        string nextScene = sceneOrder[phaseIndex];
        SceneManager.LoadScene(nextScene);
    }

    // ใช้ในกรณีอยากรีเซ็ต (เช่นปุ่ม Dev)
    public static void ResetFTU()
    {
        PlayerPrefs.DeleteKey(KEY_DONE);
        PlayerPrefs.DeleteKey(KEY_PHASE);
        PlayerPrefs.Save();
    }
}
