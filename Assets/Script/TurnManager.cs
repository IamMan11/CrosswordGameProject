// ========================== TurnManager.cs ==========================
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("UI")]
    public Button   confirmBtn;
    public TMP_Text scoreText;
    public TMP_Text bagCounterText;
    public TMP_Text messageText;

    public int Score { get; private set; }
    public int TotalScore { get; private set; }
    public int CheckedWordCount { get; private set; }

    bool usedDictionaryThisTurn = false;
    bool freePassActiveThisTurn = false;

    Coroutine fadeCo;

    readonly HashSet<string> boardWords = new(System.StringComparer.OrdinalIgnoreCase);

    int nextWordMul = 1;

    [Header("Mana System")]
    public int maxMana = 10;
    public int currentMana;
    [SerializeField] private TMP_Text manaText;
    private bool infiniteManaMode = false;
    private Coroutine manaInfiniteCoroutine = null;

    [Header("Fire Stack (per turn)")]
    public LineOfFireAnimatorDriver fireFx;   // ลากตัวที่มี Animator Driver มาใส่ Inspector
    public int fireStack = 0;                 // สะสมตามเทิร์น

    private readonly Dictionary<string, int> usageCountThisTurn = new Dictionary<string, int>();

    public string LastConfirmedWord { get; private set; } = string.Empty;
    bool inConfirmProcess = false;

    [Header("Scoring Overlay / FX")]
    public GameObject inputBlocker;
    public Animator scoreOverlayAnimator;
    public TMP_Text phaseLabel;
    public float letterStepDelay = 0.08f;
    public float setDelay = 0.20f;
    public float phaseDelay = 0.25f;
    public bool pauseTimeDuringScoring = true;

    [Header("Score Pop (Anchors & Prefab)")]
    public RectTransform anchorLetters;
    public RectTransform anchorMults;
    public RectTransform anchorTotal;
    public RectTransform scoreHud;
    public ScorePopUI scorePopPrefab;
    public bool IsConfirmInProgress => inConfirmProcess;
    public bool IsConfirmLockedNow  => inConfirmProcess || IsScoringAnimation;

    [Header("Score Pop Settings")]
    public int tier2Min = 3;
    public int tier3Min = 6;
    public float stepDelay = 0.08f;
    public float sectionDelay = 0.20f;
    public float flyDur = 0.6f;
    [Header("Total Wave FX")]
    public bool  totalWaveEnabled   = true;
    public int   totalWaveThreshold = 120; // เริ่มเล่นคลื่นเมื่อ total >= ค่านี้ (ก่อนหักโทษ)
    public float waveAmplitude      = 24f;
    public float waveCharPhase      = 0.55f;
    public float waveSpeed          = 7f;
    public float waveHoldTail       = 0.15f;
    public float waveSettleDur      = 0.20f;
    [Header("Scoring SFX Pitch")]
    public float letterPitchStart = 1.00f;
    public float letterPitchMax   = 1.60f;
    public float multPitchStart   = 1.00f;
    public float multPitchMax     = 1.70f;
    [Header("Join SFX Scale (by total before penalty)")]
    public int   joinMidThreshold  = 60;   // คะแนนรวมก่อนหักโทษ ≥ 60 → Mid
    public int   joinHighThreshold = 120;  // ≥ 120 → High (เพดาน)
    [Range(0.5f, 2f)] public float joinVolBase = 1.00f;
    [Range(0.5f, 2f)] public float joinVolMid  = 1.25f;
    [Range(0.5f, 2f)] public float joinVolHigh = 1.50f;
    [Range(0.5f, 2.5f)] public float joinPitchBase = 1.00f;
    [Range(0.5f, 2.5f)] public float joinPitchMid  = 1.15f;
    [Range(0.5f, 2.5f)] public float joinPitchHigh = 1.30f;
    [Header("BGM Streak")]
    public int bgmStreak = 0;
    public int bgmTier2At = 3;  // 3 ครั้งติด = Mid
    public int bgmTier3At = 5;  // 5 ครั้งติด = High

    [Header("Dictionary Penalty")]
    [Range(0,100)] public int dictionaryPenaltyPercent = 50;

    Coroutine bagTweenCo = null;
    int lastBagShown = -1;

    static readonly WaitForSeconds WFS_06 = new WaitForSeconds(0.6f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (confirmBtn != null) confirmBtn.onClick.AddListener(OnConfirm);
        else Debug.LogWarning("[TurnManager] confirmBtn not assigned.");
    }

    void OnDisable()
    {
        if (confirmBtn != null) confirmBtn.onClick.RemoveListener(OnConfirm);

        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }
        if (manaInfiniteCoroutine != null) { StopCoroutine(manaInfiniteCoroutine); manaInfiniteCoroutine = null; }

        // กันหลงเหลือสถานะชั่วคราว (รองรับ Bench Issue)
        ScoreManager.ClearZeroScoreTiles();
    }

    void Start()
    {
        var prog = PlayerProgressSO.Instance?.data;
        if (prog != null)
        {
            maxMana = prog.maxMana;
            currentMana = maxMana;
        }
        else
        {
            currentMana = maxMana;
        }

        usageCountThisTurn.Clear();

        UpdateScoreUI();
        UpdateManaUI();
        UpdateBagUI();
    }

    public void ResetTotalScore() => TotalScore = 0;

    void Update()
    {
        // กันปุ่มเด้งกลับมา active ระหว่างกำลังคิดคะแนน/มี blocker
        if (confirmBtn != null)
        {
            if ((inputBlocker && inputBlocker.activeSelf) ||
                (scoreOverlayAnimator && scoreOverlayAnimator.GetBool("Scoring")))
            {
                confirmBtn.interactable = false;
                return;
            }
        }

        if (inConfirmProcess) return;
        if (confirmBtn == null) return;

        // เดิม: bool busy = inConfirmProcess || IsScoringAnimation;
        bool busy = inConfirmProcess || IsScoringAnimation || UiGuard.IsBusy; // ✅ เพิ่ม UiGuard

        if (!busy)
        {
            bool can = BoardHasAnyTile();
            confirmBtn.interactable = can;
            SetButtonVisual(confirmBtn, can);
        }
        else
        {
            confirmBtn.interactable = false;
            SetButtonVisual(confirmBtn, false);
        }
    }

    void ClearAllSlotFx()
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return;

        int R = bm.rows, C = bm.cols;
        for (int r = 0; r < R; r++)
            for (int c = 0; c < C; c++)
            {
                var s = bm.grid[r, c];
                if (s == null) continue;
                s.CancelFlash();
                s.HidePreview();
            }
    }

    void BeginScoreSequence()
    {
        ClearAllSlotFx();
        IsScoringAnimation = true;
        Level2Controller.SetZoneTimerFreeze(true);    // NEW: freeze โซน

        if (inputBlocker) inputBlocker.SetActive(true);
        if (scoreOverlayAnimator)
        {
            if (pauseTimeDuringScoring)
                scoreOverlayAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            scoreOverlayAnimator.SetBool("Scoring", true);
        }
    }

    void EndScoreSequence()
    {
        if (inputBlocker != null) inputBlocker.SetActive(false);
        if (scoreOverlayAnimator != null)
            scoreOverlayAnimator.SetBool("Scoring", false);

        CardManager.Instance?.HoldUI(false);
        ScoreManager.ClearZeroScoreTiles();

        Level2Controller.SetZoneTimerFreeze(false);   // NEW: คลาย freeze โซน
        IsScoringAnimation = false;
    }

    public bool IsScoringAnimation { get; private set; } = false;

    public int ConfirmsThisLevel { get; private set; } = 0;
    public int UniqueWordsThisLevel => boardWords.Count;
    void SetButtonVisual(Button b, bool on)
    {
        if (!b) return;
        var cg = b.GetComponent<CanvasGroup>();
        if (!cg) cg = b.gameObject.AddComponent<CanvasGroup>();

        b.interactable      = on;
        cg.interactable     = on;
        cg.blocksRaycasts   = on;
        cg.alpha            = on ? 1f : 0.45f;   // ปรับความจางตามชอบ
    }

    bool BoardHasAnyTile()
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return false;

        int R = bm.grid.GetLength(0), C = bm.grid.GetLength(1);
        for (int r = 0; r < R; r++)
            for (int c = 0; c < C; c++)
            {
                var s = bm.grid[r, c];
                if (s == null) continue;
                if (!BoardAnalyzer.IsRealLetter(s)) continue; // ไม่นับ Garbled
                var t = s.GetLetterTile();
                if (t != null && !t.isLocked)                  // นับเฉพาะตัวที่ยังไม่ล็อก (วางใหม่เทิร์นนี้)
                    return true;
            }
        return false;
    }
    
    // ================== Boss Fight (Level 3) ==================
    
    // คำนวณดาเมจให้บอสจากคำที่วาง
    private void HandleBossDamage(int totalWordScore)
    {
        if (LevelManager.Instance.CurrentLevel == 3)
        {
            // คำนวณดาเมจบอสจากคะแนนของคำที่วาง
            bossDamageThisTurn += totalWordScore;
            LevelManager.Instance.Level3_OnPlayerDealtWord(bossDamageThisTurn);
        }
    }

    public void OnWordChecked(bool isCorrect, string word)
    {
        if (isCorrect)
        {
            CheckedWordCount++;
            boardWords.Add(word.ToLower());

            // Handle IT word detection for Level 1
            if (LevelManager.Instance.CurrentLevel == 1 && itKeywordsLevel1.Contains(word.ToLower()))
            {
                itWordsFound.Add(word.ToLower());
                UpdateITWordProgress();
            }
        }
        LevelManager.Instance?.OnScoreOrWordProgressChanged();
    }
    
    private void UpdateITWordProgress()
    {
        if (itProgressText != null)
        {
            itProgressText.text = $"IT words: {itWordsFound.Count}/{itWordsTargetLevel1}";
        }
    }

    // ================== Fire Stack (for bonuses) ==================

    // ใช้ Fire Stack ในการคำนวณคะแนนโบนัส
    void HandleFireStackBonus()
    {
        if (LevelManager.Instance.CurrentLevel == 3 && fireStack > 0)
        {
            // ใช้ Fire Stack ในการคำนวณดาเมจให้บอส
            int fireStackBonus = fireStack * bossDamagePerWord;
            bossDamageThisTurn += fireStackBonus;
            fireStack = 0; // รีเซ็ตหลังใช้งาน
            LevelManager.Instance.Level3_OnPlayerDealtWord(bossDamageThisTurn);
        }
    }

    // ================== Score Finalizing ==================

    // คำนวณและแสดงคะแนน
    public void FinalizeScoreCalculation(int totalWordScore, int comboMultiplier)
    {
        // คำนวณดาเมจให้กับบอส
        HandleBossDamage(totalWordScore);
        
        // เพิ่มคะแนนจาก Combo
        ApplyComboBonus(comboMultiplier);

        // เพิ่มคะแนนจาก Fire Stack
        HandleFireStackBonus();
        
        // ปรับปรุง UI ของคะแนน
        UpdateScoreUI();
    }

    // ================== Combo ==================
    
    // ใช้ Combo เพิ่มคะแนน
    private void ApplyComboBonus(int comboMultiplier)
    {
        if (comboMultiplier > 1)
        {
            int comboBonus = Mathf.CeilToInt(Score * comboMultiplier);
            Score += comboBonus;
            UpdateScoreUI();
            ShowMessage($"Combo! +{comboBonus} points!", Color.green);
        }
    }

    // ================== Handle Turn End ==================

    // จัดการเมื่อจบเทิร์น
    public void HandleTurnEnd()
    {
        // คำนวณคะแนนหลังจากหัก penalty หรือโบนัส
        FinalizeScoreCalculation(Score, fireStack);

        // แสดงข้อความจบเทิร์น
        ShowMessage("End of Turn!", Color.yellow);

        // รีเซ็ตข้อมูลเทิร์น
        ResetManaAndFireStack();
    }

    // ================== Message ==================

    // แสดงข้อความใน HUD
    void ShowMessage(string message, Color color)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = color;
        }
    }
}
