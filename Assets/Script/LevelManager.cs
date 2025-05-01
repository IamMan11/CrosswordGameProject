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
    public TMP_Text timerText;

    int   currentLevel;
    float elapsedTime;
    bool  timerStarted;
    bool  timing;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SetupLevel(0);
    }

    void Update()
    {
        var cfg = levels[currentLevel];

        // นับถอยหลังเมื่อเริ่มแล้ว และมี timeLimit > 0
        if (timing && timerStarted)
        {
            elapsedTime += Time.deltaTime;
            float remaining = Mathf.Max(0, cfg.timeLimit - elapsedTime);
            timerText.text = remaining.ToString("0.0") + "s";

            if (remaining <= 0f)
            {
                // หมดเวลา → สุ่มลบตัวอักษร + รีเซ็ตเวลา
                TurnManager.Instance.AutoRemoveNow();
                ResetTimer();
            }
        }

        // เช็กผ่านเงื่อนไขด่าน
        if (TurnManager.Instance.Score >= cfg.requiredScore
            && TurnManager.Instance.CheckedWordCount >= cfg.requiredWords)
        {
            NextLevel();
        }
    }

    void SetupLevel(int idx)
    {
        currentLevel = idx;
        elapsedTime  = 0f;
        timerStarted = false;
        timing       = levels[idx].timeLimit > 0;

        levelText.text = $"Level {levels[idx].levelIndex}";
        timerText.gameObject.SetActive(false);

        // รีเซ็ต TurnManager และเปิด/ปิด auto-remove
        TurnManager.Instance.ResetForNewLevel();
        if (levels[idx].enableAutoRemove)
            TurnManager.Instance.StartAutoRemove(levels[idx].autoRemoveInterval);
    }

    // เรียกครั้งเดียวเมื่อผู้เล่นยืนยันคำแรก
    public void OnFirstConfirm()
    {
        if (!timerStarted && timing)
        {
            timerStarted = true;
            elapsedTime  = 0f;
            timerText.gameObject.SetActive(true);
            Debug.Log("Timer started");  // ลองดูใน Console ว่าถูกเรียกหรือไม่
        }
    }

    // รีเซ็ตเฉพาะตัวนับเวลา (แต่ไม่ปิด UI)
    public void ResetTimer()
    {
        elapsedTime = 0f;
        timerText.text = levels[currentLevel].timeLimit.ToString("0.0") + "s";
    }

    void NextLevel()
    {
        if (currentLevel + 1 < levels.Length)
        {
            SetupLevel(currentLevel + 1);
        }
        else
        {
            Debug.Log("เกมจบ!");
            // แสดง UI ชนะที่นี่ถ้ามี
        }
    }
}
