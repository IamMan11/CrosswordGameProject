using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Configs")]
    public LevelConfig[] levels;

    [Header("UI (ผูกใน Inspector)")]
    public TMP_Text levelText;
    public TMP_Text timerText;         // จับเวลาสำหรับ Auto-Remove
    public TMP_Text levelTimerText;    // จับเวลารวมของด่าน (เวลาหมด = แพ้)

    public int CurrentLevel => currentLevel;

    int currentLevel;
    bool timerStarted;
    bool timing;
    bool isGameOver = false;

    float levelTimeLimit;
    float levelTimeElapsed;
    bool levelTimerRunning;

    Coroutine autoRemoveCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("❌ No level configuration provided!");
            return;
        }

        SetupLevel(0);
    }

    public bool IsGameOver()
    {
        return isGameOver;
    }

    void Update()
    {
        if (isGameOver) return;

        var cfg = levels[currentLevel];

        // 🕒 จับเวลาเลเวลหลัก
        if (levelTimerRunning)
        {
            levelTimeElapsed += Time.deltaTime;
            float remaining = Mathf.Max(0, levelTimeLimit - levelTimeElapsed);
            UpdateLevelTimerText(remaining);

            if (remaining <= 0f)
            {
                levelTimerRunning = false;
                GameOver(false); // ❌ แพ้เพราะหมดเวลา
            }
        }

        // ✅ ผ่านด่าน
        if (!isGameOver && TurnManager.Instance.Score >= cfg.requiredScore &&
            TurnManager.Instance.CheckedWordCount >= cfg.requiredWords)
        {
            AnnounceLevelComplete();
            NextLevel();
        }
    }

    void SetupLevel(int idx)
    {
        currentLevel = idx;
        timerStarted = false;
        timing = levels[idx].enableAutoRemove;

        levelText.text = $"Level {levels[idx].levelIndex}";
        timerText.gameObject.SetActive(false);

        // 🔁 จับเวลาหลักของด่าน
        levelTimeElapsed = 0f;
        levelTimeLimit = levels[idx].timeLimit;
        levelTimerRunning = false;
        UpdateLevelTimerText(levelTimeLimit);

        // ล้างกระดาน + เติมตัวอักษรใหม่
        BoardManager.Instance.GenerateBoard();
        TurnManager.Instance.ResetForNewLevel();
        // a) รีเซ็ต TileBag ตาม Progress (190/200 หรือค่าจริง)
        TileBag.Instance.RefillTileBag();
        // b) อัปเดต UI ถุงก่อนจั่ว
        TurnManager.Instance.UpdateBagUI();
        // c) จั่วลง Bench (เหลือ 190)
        BenchManager.Instance.RefillEmptySlots();
        // d) อัปเดต UI อีกครั้งเพื่อความชัวร์
        TurnManager.Instance.UpdateBagUI();

        Debug.Log($"▶ เริ่มด่าน {levels[idx].levelIndex} | เวลา: {levels[idx].timeLimit}s | Score: {levels[idx].requiredScore}");
    }

    public void OnFirstConfirm()
    {
        if (!timerStarted && timing)
        {
            timerStarted = true;
            timerText.gameObject.SetActive(true);
            StartAutoRemoveLoop(levels[currentLevel].autoRemoveInterval);
            Debug.Log("⏱️ Auto-remove Timer started");
        }

        if (!levelTimerRunning && levelTimeLimit > 0)
        {
            levelTimerRunning = true;
            levelTimeElapsed = 0f;
            Debug.Log("🕒 Level timer started");
        }
    }

    public void ResetTimer()
    {
        if (autoRemoveCoroutine != null)
        {
            StopCoroutine(autoRemoveCoroutine);
        }
        timerText.text = levels[currentLevel].autoRemoveInterval.ToString("0.0") + "s";
        StartAutoRemoveLoop(levels[currentLevel].autoRemoveInterval);
    }

    void StartAutoRemoveLoop(float interval)
    {
        if (autoRemoveCoroutine != null)
            StopCoroutine(autoRemoveCoroutine);

        autoRemoveCoroutine = StartCoroutine(AutoRemoveRoutine(interval));
    }

    IEnumerator AutoRemoveRoutine(float interval)
    {
        while (!isGameOver)
        {
            float countdown = interval;
            while (countdown > 0f && !isGameOver)
            {
                countdown -= Time.deltaTime;
                timerText.text = countdown.ToString("0.0") + "s";
                yield return null;
            }

            if (!isGameOver)
            {
                TurnManager.Instance.AutoRemoveNow();
                Debug.Log("🔁 Auto-remove triggered");
            }
        }
    }

    void NextLevel()
    {
        Debug.Log($"[LevelManager] NextLevel: currentLevel={currentLevel}, levels.Length={levels.Length}");

        if (currentLevel + 1 < levels.Length)
        {
            SetupLevel(currentLevel + 1);
        }
        else
        {
            GameOver(true); // ✅ ชนะเกม
        }
    }

    void GameOver(bool win)
    {
        if (isGameOver) return;

        isGameOver = true;
        timerStarted = false;
        levelTimerRunning = false;

        if (autoRemoveCoroutine != null)
            StopCoroutine(autoRemoveCoroutine);

        timerText.gameObject.SetActive(false);
        levelTimerText.color = win ? Color.green : Color.red;

        if (win)
            Debug.Log("🎉 ชนะทุกด่าน");
        else
            Debug.Log("💀 แพ้เพราะหมดเวลา");

        // TODO: แสดง GameOverPanel
    }

    void UpdateLevelTimerText(float remaining)
    {
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        levelTimerText.text = $"🕒 {minutes:00}:{seconds:00}";
    }

    void AnnounceLevelComplete()
    {
        Debug.Log($"✅ ผ่านด่าน {levels[currentLevel].levelIndex}!");
    }
}