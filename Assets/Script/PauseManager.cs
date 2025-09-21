using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class PauseManager : MonoBehaviour
{
    public static PauseManager I { get; private set; }
    [Header("Panels")]
    [Tooltip("หน้าหลักของ Pause (มีปุ่ม Back/Reset/Setting/Return/Quit)")]
    public GameObject pauseRoot;
    [Tooltip("หน้าตั้งค่าเสียง (Master / Music / SFX)")]
    public GameObject settingsPanel;

    [Header("Options")]
    [Tooltip("ชื่อซีนเมนูหลัก (จะไม่ให้กด Esc เปิดพาเนลในซีนนี้)")]
    public string mainMenuSceneName = "MainMenu";
    [Tooltip("อนุญาตให้กด Esc ซ้อนเพื่อ Back/Close")]
    public bool allowEscToClose = true;
    [SerializeField] Canvas pauseCanvas;       // ลาก GlobalPauseCanvas เข้ามา
    [SerializeField] bool persistAcrossScenes = true;
    [SerializeField] int canvasSortOrder = 5000; // ค่าเดียวกับบนอินสเปกเตอร์
    readonly Dictionary<Animator, float> _pausedAnim = new();

    public static bool IsPaused { get; private set; }

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        if (persistAcrossScenes && pauseCanvas)
        {
            DontDestroyOnLoad(pauseCanvas.gameObject);
            pauseCanvas.overrideSorting = true;
            pauseCanvas.sortingOrder = canvasSortOrder;
        }

        // กันเคสค้างจากซีนก่อน
        ResumeGame(force: true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ไม่อนุญาตในหน้า MainMenu
            var now = SceneManager.GetActiveScene().name;
            if (string.Equals(now, mainMenuSceneName, System.StringComparison.OrdinalIgnoreCase))
                return;
            if (settingsPanel && settingsPanel.activeSelf)
            {
                CloseSettings();
                SfxPlayer.Play(SfxId.UI_Click);
                return;
            }

            // toggle pause root
            if (!IsPaused) OpenPause();
            else if (allowEscToClose) ClosePause();
        }
    }

    // ===== API เปิด/ปิด =====
    public void OpenPause()
    {
        if (IsPaused) return;

        // ดันพาเนลขึ้นบนสุด
        if (pauseRoot) pauseRoot.transform.SetAsLastSibling();
        if (settingsPanel) settingsPanel.transform.SetAsLastSibling();

        // === หยุดเวลาแบบ scale + หยุดระบบ unscaled ด้วย ===
        Time.timeScale = 0f;
        AudioListener.pause = true;
        LevelManager.Instance?.PauseLevelTimer();        // <— หยุดจับเวลาในเลเวล
        UiGuard.Push();                                  // <— บล็อกอินพุต (TurnManager เช็กตัวนี้)
        FreezeUnscaledAnimators(true);                   // <— แช่ Animator ที่เป็น UnscaledTime
        Level2Controller.SetZoneTimerFreeze(true);       // (ถ้ามี) แช่โซนด่าน 2

        if (pauseRoot) pauseRoot.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        

        IsPaused = true;
        SfxPlayer.Play(SfxId.UI_Click);
    }

    public void ClosePause()
    {
        if (!IsPaused) return;

        
        if (settingsPanel) settingsPanel.SetActive(false);
        if (pauseRoot) pauseRoot.SetActive(false);

        // === เดินต่อทั้งสองโลกเวลา ===
        Time.timeScale = 1f;
        AudioListener.pause = false;
        LevelManager.Instance?.ResumeLevelTimer();
        UiGuard.Pop();
        FreezeUnscaledAnimators(false);
        Level2Controller.SetZoneTimerFreeze(false);

        IsPaused = false;
        SfxPlayer.Play(SfxId.UI_Click);
    }

    void ResumeGame(bool force = false)
    {
        // เดินเวลา + ปลดหยุดเสียง
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
        if (force)
        {
            if (pauseRoot) pauseRoot.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            
        }
    }
    void FreezeUnscaledAnimators(bool freeze)
    {
        if (freeze)
        {
            _pausedAnim.Clear();
            // รวมทุก Animator ที่ active (ทั้งใน/นอกกล้อง)
            var anims = Resources.FindObjectsOfTypeAll<Animator>();
            foreach (var a in anims)
            {
                if (!a || !a.isActiveAndEnabled) continue;
                // แช่เฉพาะตัวที่อัพเดตด้วย UnscaledTime หรือกำลังวิ่งอยู่
                if (a.updateMode == AnimatorUpdateMode.UnscaledTime && a.speed > 0f)
                {
                    _pausedAnim[a] = a.speed; a.speed = 0f;
                }
            }
        }
        else
        {
            foreach (var kv in _pausedAnim)
                if (kv.Key) kv.Key.speed = kv.Value;
            _pausedAnim.Clear();
        }
    }

    // ===== ปุ่มบน Pause =====
    public void Btn_Back() => ClosePause();

    public void Btn_Settings()
    {
        if (!IsPaused) OpenPause();
        if (settingsPanel) settingsPanel.SetActive(true);
        // sync ค่าจากมิกเซอร์มายังสไลเดอร์
        var ui = settingsPanel ? settingsPanel.GetComponentInChildren<SettingsPanelUI>() : null;
        if (ui) ui.RefreshFromMixer();
        SfxPlayer.Play(SfxId.UI_Click);
    }

    public void CloseSettings()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    public void Btn_ReturnToMainMenu()
    {
        // 1) ปิด Pause Panel ให้สะอาด (และคืนเวลาปกติ)
        if (IsPaused) ClosePause();          // ซ่อนพาเนล + TimeScale=1 + ปลด freeze ต่างๆ
        else
        {
            // กันกรณีเรียกขณะไม่ paused
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (settingsPanel) settingsPanel.SetActive(false);
            if (pauseRoot)     pauseRoot.SetActive(false);
        }

        // 2) เคลียร์เสียงของซีนปัจจุบัน
        BgmPlayer.I?.StopImmediateAndClear();
        SfxPlayer.I?.StopAllAndClearBank();

        // (ออปชัน) ตอกค่าวอลลุ่มผู้ใช้ (กันค่าสะดุด)
        if (AudioMixerController.I)
            AudioMixerController.I.ApplyAll(
                AudioMixerController.I.GetMaster(),
                AudioMixerController.I.GetMusic(),
                AudioMixerController.I.GetSfx()
            ); // จะถูกตอกซ้ำอีกครั้งเมื่อโหลดซีนใหม่ด้วย sceneLoaded อยู่แล้ว :contentReference[oaicite:6]{index=6}

        // 3) โหลดซีนเมนูหลัก
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneTransitioner.LoadScene(mainMenuSceneName);
    }


    public void Btn_QuitToDesktop()
    {
        SfxPlayer.Play(SfxId.UI_Click);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
