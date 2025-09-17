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

    [Header("Score Pop Settings")]
    public int tier2Min = 3;
    public int tier3Min = 6;
    public float stepDelay = 0.08f;
    public float sectionDelay = 0.20f;
    public float flyDur = 0.6f;
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
    static readonly WaitForSeconds WFS_2s = new WaitForSeconds(2f);

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
        var prog = PlayerProgressSO.Instance != null ? PlayerProgressSO.Instance.data : null;
        if (prog != null) maxMana = prog.maxMana;

        currentMana = maxMana;
        usageCountThisTurn.Clear();

        UpdateScoreUI();
        UpdateManaUI();
        UpdateBagUI();
    }

    public void ResetTotalScore() => TotalScore = 0;

    void Update()
    {
        if (!inConfirmProcess && confirmBtn != null)
        {
            bool can = BoardHasAnyTile();
            confirmBtn.interactable = can;   // ของเดิม
            SetButtonVisual(confirmBtn, can); // เพิ่มบรรทัดนี้ให้จาง/สว่างตามสถานะ
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

        if (inputBlocker) inputBlocker.SetActive(true);

        if (scoreOverlayAnimator)
        {
            scoreOverlayAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            scoreOverlayAnimator.SetBool("Scoring", true);
        }

        if (pauseTimeDuringScoring) Time.timeScale = 0f;
    }

    void EndScoreSequence()
    {
        if (pauseTimeDuringScoring) Time.timeScale = 1f;
        if (inputBlocker != null) inputBlocker.SetActive(false);

        if (scoreOverlayAnimator != null)
            scoreOverlayAnimator.SetBool("Scoring", false);

        CardManager.Instance?.HoldUI(false);

        ScoreManager.ClearZeroScoreTiles();
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
    void ResetBgmStreak(bool playSfx)
    {
        bgmStreak = 0;
        BgmPlayer.I?.SetTier(BgmTier.Base);
        if (playSfx) SfxPlayer.Play(SfxId.StreakBreak);   // <-- ใช้เสียงสตรีคแตกใหม่
    }
    void ApplyBgmStreakAfterConfirm(bool dictPenaltyApplied)
    {
        bgmStreak = Mathf.Max(0, bgmStreak + 1);

        var newTier = (bgmStreak >= bgmTier3At) ? BgmTier.High
                    : (bgmStreak >= bgmTier2At) ? BgmTier.Mid
                    : BgmTier.Base;

        var oldTier = (BgmPlayer.I != null) ? BgmPlayer.I.CurrentTier : BgmTier.Base;

        if (newTier != oldTier)
            BgmPlayer.I?.SetTier(newTier);      // เปลี่ยนเพลงเฉพาะตอนข้าม tier
    }
    float PitchAtStep(int index, int total, float start, float max)
    {
        if (total <= 1) return start;
        float t = Mathf.Clamp01(index / Mathf.Max(1f, (float)(total - 1)));
        return Mathf.Lerp(start, max, t);
    }
    enum JoinTier { Base, Mid, High }
    JoinTier GetJoinTier(int totalBeforePenalty)
    {
        if (totalBeforePenalty >= joinHighThreshold) return JoinTier.High;
        if (totalBeforePenalty >= joinMidThreshold)  return JoinTier.Mid;
        return JoinTier.Base;
    }

    public void ResetForNewLevel()
    {
        Score = 0;
        CheckedWordCount = 0;
        boardWords.Clear();

        if (confirmBtn != null) confirmBtn.interactable = true;

        UpdateScoreUI();
        UpdateBagUI();

        usageCountThisTurn.Clear();
        usedDictionaryThisTurn = false;
        freePassActiveThisTurn = false;
        nextWordMul = 1;

        ConfirmsThisLevel = 0;

        LevelManager.Instance?.OnScoreOrWordProgressChanged();
    }

    public void AddScore(int delta)
    {
        Score      = Mathf.Max(0, Score + delta);
        TotalScore = Mathf.Max(0, TotalScore + delta);
        UpdateScoreUI();
        LevelManager.Instance?.OnScoreOrWordProgressChanged();
    }

    void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"Score : {Score}";
    }

    /* ===================== Mana ===================== */

    public void ActivateInfiniteMana(float duration)
    {
        if (manaInfiniteCoroutine != null) StopCoroutine(manaInfiniteCoroutine);
        infiniteManaMode = true;
        UpdateManaUI();
        ShowMessage("Mana Infinity – ใช้มานาไม่จำกัด!", Color.cyan);
        manaInfiniteCoroutine = StartCoroutine(DeactivateInfiniteManaAfter(duration));
    }

    IEnumerator DeactivateInfiniteManaAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        infiniteManaMode = false;
        manaInfiniteCoroutine = null;
        UpdateManaUI();
        ShowMessage("Mana Infinity หมดเวลาแล้ว", Color.cyan);
    }

    public void AddMana(int amount)
    {
        if (infiniteManaMode) return;
        currentMana = Mathf.Min(maxMana, currentMana + amount);
        UpdateManaUI();
        ShowMessage($"+{amount} Mana", Color.cyan);
    }

    public bool UseMana(int amount)
    {
        if (infiniteManaMode) return true;
        if (currentMana < amount) return false;
        currentMana -= amount;
        UpdateManaUI();
        return true;
    }

    public void UpgradeMaxMana(int newMax)
    {
        maxMana     = Mathf.Max(0, newMax);
        currentMana = Mathf.Clamp(currentMana, 0, maxMana);
        UpdateManaUI();
    }

    void UpdateManaUI()
    {
        if (manaText != null)
            manaText.text = infiniteManaMode ? "Mana: ∞" : $"Mana: {currentMana}/{maxMana}";
    }

    /* ===================== Turn flags / cards ===================== */

    public void SetDictionaryUsed() => usedDictionaryThisTurn = true;

    public void ApplyFreePass()
    {
        freePassActiveThisTurn = true;
        ShowMessage("Free Pass – ยกเลิกโทษการเปิดพจนานุกรมในเทิร์นนี้!", Color.cyan);
    }

    public void SetScoreMultiplier(int mul) => nextWordMul = Mathf.Max(1, mul);

    public void OnWordChecked(bool isCorrect)
    {
        if (isCorrect)
        {
            CheckedWordCount++;
            LevelManager.Instance?.OnScoreOrWordProgressChanged();
        }
    }

    public void ResetCardUsage()
    {
        usageCountThisTurn.Clear();
        ShowMessage("Reset Card Usage – รีเซ็ตการใช้การ์ดในเทิร์นนี้แล้ว", Color.cyan);
    }

    public bool CanUseCard(CardData card)
    {
        if (card == null) return false;
        if (!usageCountThisTurn.ContainsKey(card.id)) return true;
        return usageCountThisTurn[card.id] < card.maxUsagePerTurn;
    }

    public void OnCardUsed(CardData card)
    {
        if (card == null) return;
        if (!usageCountThisTurn.ContainsKey(card.id)) usageCountThisTurn[card.id] = 1;
        else usageCountThisTurn[card.id]++;
    }

    public int GetUsageCount(CardData card)
    {
        if (card == null) return 0;
        if (!usageCountThisTurn.ContainsKey(card.id)) return 0;
        return usageCountThisTurn[card.id];
    }

    public void UpdateBagUI()
    {
        if (bagCounterText == null) return;

        var bag = TileBag.Instance;
        if (bag == null) { bagCounterText.text = "—"; return; }

        int remain = bag.Remaining;
        int total  = bag.TotalInitial;

        if (lastBagShown < 0)
        {
            lastBagShown = remain;
            bagCounterText.text = $"{remain}/{total}";
            return;
        }

        if (bagTweenCo != null) StopCoroutine(bagTweenCo);
        bagTweenCo = StartCoroutine(AnimateBagRemaining(lastBagShown, remain, total, 0.20f));
        lastBagShown = remain;
    }

    IEnumerator AnimateBagRemaining(int from, int to, int total, float dur)
    {
        if (from == to) { bagCounterText.text = $"{to}/{total}"; yield break; }

        float t = 0f;
        int last = from;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, dur);
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, 1 - Mathf.Pow(1 - t, 3)));
            if (v != last)
            {
                bagCounterText.text = $"{v}/{total}";
                last = v;
            }
            yield return null;
        }
        bagCounterText.text = $"{to}/{total}";
    }

    /* ===================== Message (HUD) ===================== */

    void ShowMessage(string msg, Color? col = null)
    {
        if (messageText == null) return;

        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }

        messageText.text  = msg;
        messageText.color = col ?? Color.white;

        if (!string.IsNullOrEmpty(msg))
            fadeCo = StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        yield return WFS_2s;
        float t = 0f;
        Color start = messageText.color;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            messageText.color = new Color(start.r, start.g, start.b, 1 - t);
            yield return null;
        }
        messageText.text  = string.Empty;
        messageText.color = new Color(start.r, start.g, start.b, 1f);
    }

    /* ===================== Confirm ===================== */

    public void EnableConfirm()
    {
        inConfirmProcess = false;
        if (confirmBtn == null) return;
        bool can = BoardHasAnyTile();
        confirmBtn.interactable = can;
        SetButtonVisual(confirmBtn, can);
    }


    public void OnClickDictionaryButton()
    {
        UIConfirmPopup.Show(
            "การเปิดพจนานุกรมจะลดคะแนนคำในเทิร์นนี้ 50%\nยังต้องการเปิดหรือไม่?",
            () => DictionaryUI.Instance.Open(),
            null
        );
    }

    List<int> BuildMultiplierFactors(List<MoveValidator.WordInfo> correct)
    {
        var factors = new List<int>();

        foreach (var w in correct)
        {
            int wordMul = 1;
            foreach (var s in SlotsInWord(w))
            {
                int wm = ScoreManager.EffectiveWordMulFor(s.type);
                if (wm > 1) wordMul *= wm;
            }
            if (wordMul > 1) factors.Add(wordMul);
        }

        if (ScoreManager.GetWordOverride() > 1)
            factors.Add(ScoreManager.GetWordOverride());

        return factors;
    }

    List<int> BuildLetterAdds(List<MoveValidator.WordInfo> correct)
    {
        var adds = new List<int>();
        foreach (var w in correct)
        {
            foreach (var s in SlotsInWord(w))
            {
                var t = s.GetLetterTile(); if (!t) continue;
                int baseSc = Mathf.Max(0, t.GetData().score);

                if (ScoreManager.IsZeroScoreTile(t)) baseSc = 0;

                int lm = ScoreManager.EffectiveLetterMulFor(s.type);
                adds.Add(baseSc * Mathf.Max(1, lm));
            }
        }
        return adds;
    }

    List<(LetterTile t, BoardSlot s, int add)> BuildLetterSteps(List<MoveValidator.WordInfo> correct)
    {
        var steps = new List<(LetterTile, BoardSlot, int)>();
        foreach (var w in correct)
        {
            foreach (var s in SlotsInWord(w))
            {
                var t = s.GetLetterTile(); if (!t) continue;
                int baseSc = Mathf.Max(0, t.GetData().score);

                if (ScoreManager.IsZeroScoreTile(t)) baseSc = 0;

                int lm = ScoreManager.EffectiveLetterMulFor(s.type);
                steps.Add((t, s, baseSc * Mathf.Max(1, lm)));
            }
        }
        return steps;
    }

    ScorePopUI SpawnPop(RectTransform anchor, int startValue = 0)
    {
        var ui = Instantiate(scorePopPrefab, anchor);
        ui.transform.localPosition = Vector3.zero;
        ui.transform.localScale    = Vector3.one;
        ui.SetValue(startValue);
        return ui;
    }

    List<BoardSlot> SlotsInWord(MoveValidator.WordInfo w)
    {
        var list = new List<BoardSlot>();
        int dr = w.r0 == w.r1 ? 0 : (w.r1 > w.r0 ? 1 : -1);
        int dc = w.c0 == w.c1 ? 0 : (w.c1 > w.c0 ? 1 : -1);
        int r = w.r0, c = w.c0;

        while (true)
        {
            var s = BoardManager.Instance?.GetSlot(r, c);
            if (s != null) list.Add(s);
            if (r == w.r1 && c == w.c1) break;
            r += dr; c += dc;
        }
        return list;
    }

    IEnumerator AnimateAndFinalizeScoring(
        List<(LetterTile t, BoardSlot s)> placed,
        List<MoveValidator.WordInfo>      correct,
        int                               moveScore,
        int                               comboMul,
        HashSet<LetterTile>               bounced,
        int                               dictPenaltyPercent
    )
    {
        LevelManager.Instance?.PauseLevelTimer();
        float prevTimeScale = Time.timeScale;

        if (scoreOverlayAnimator)
            scoreOverlayAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        try
        {
            BeginScoreSequence();

            var letterAdds = BuildLetterAdds(correct);
            var mulFactors = BuildMultiplierFactors(correct);

            int lettersRunning = 0;
            int mulRunning     = 0;

            // A) รวมตัวอักษร
            var steps = BuildLetterSteps(correct);
            var uiA   = SpawnPop(anchorLetters, 0);

            int totalLetterSteps = steps.Count;
            int letterIdx = 0;

            foreach (var step in steps)
            {
                step.s.Flash(Color.white, 1, 0.08f);
                step.t.Pulse();

                lettersRunning += step.add;
                uiA.SetValue(lettersRunning);
                uiA.SetColor(uiA.colorLetters);
                uiA.PopByDelta(step.add, tier2Min, tier3Min);

                float p = PitchAtStep(letterIdx++, totalLetterSteps, letterPitchStart, letterPitchMax);
                SfxPlayer.PlayPitch(SfxId.ScoreLetterTick, p);    // ✅ ไล่โทนสูงขึ้น

                yield return new WaitForSecondsRealtime(stepDelay);
            }
            yield return new WaitForSecondsRealtime(sectionDelay);

            // B) รวมตัวคูณ
            var uiB = SpawnPop(anchorMults, 0);
            uiB.SetColor(uiB.colorMults);

            int totalMultSteps = mulFactors.Count;     // จะบวก comboSteps เพิ่มทีหลัง
            int multIdx = 0;

            foreach (var f in mulFactors)
            {
                mulRunning += f; // x2 + x3 = x5 (ดีไซน์เดิม)
                uiB.SetText("x" + mulRunning);
                uiB.PopByDelta(f, tier2Min, tier3Min);

                float p = PitchAtStep(multIdx++, /*temp*/ Mathf.Max(1, totalMultSteps), multPitchStart, multPitchMax);
                SfxPlayer.PlayPitch(SfxId.ScoreMultTick, p);

                yield return new WaitForSecondsRealtime(stepDelay);
            }

            // คอมโบจำนวน “คำใหม่” (สูงสุด +x4)
            int comboSteps = Mathf.Min(correct.Count, 4);
            totalMultSteps += comboSteps; // รวม step ของคอมโบเข้าไปด้วย
            for (int i = 0; i < comboSteps; i++)
            {
                var w = correct[i];
                foreach (var s in SlotsInWord(w))
                {
                    var t = s.GetLetterTile();
                    if (t) t.Pulse();
                    s.Flash(new Color(1f, 0.55f, 0.20f, 1f), 1, 0.08f);
                }

                mulRunning += 1;
                uiB.SetText("x" + mulRunning);
                uiB.PopByDelta(1, tier2Min, tier3Min);

                float p = PitchAtStep(multIdx++, totalMultSteps, multPitchStart, multPitchMax);
                SfxPlayer.PlayPitch(SfxId.ScoreMultTick, p);   // ✅ ไล่ต่อจากช่วง mulFactors

                yield return new WaitForSecondsRealtime(stepDelay);
            }

            yield return new WaitForSecondsRealtime(sectionDelay);
            if (mulRunning <= 0) mulRunning = 1;

            // Join to total
            float joinDur = 0.35f;
            var flyA = uiA.FlyTo(anchorTotal, joinDur);
            var flyB = uiB.FlyTo(anchorTotal, joinDur);

            // ✅ คำนวณคะแนนรวมก่อนหักโทษ เพื่อใช้ชั่งระดับเสียง/โทน
            int prePenaltyTotal = Mathf.Max(0, lettersRunning * Mathf.Max(1, mulRunning));
            var tier = GetJoinTier(prePenaltyTotal);

            // เลือกพารามิเตอร์ตามระดับ
            float jPitch = (tier == JoinTier.High) ? joinPitchHigh :
                        (tier == JoinTier.Mid)  ? joinPitchMid  : joinPitchBase;

            float jVol   = (tier == JoinTier.High) ? joinVolHigh :
                        (tier == JoinTier.Mid)  ? joinVolMid  : joinVolBase;

            // ✅ เล่นเสียง Join แบบปรับดัง/แหลมตามระดับ (เพดาน High)
            SfxPlayer.PlayVolPitch(SfxId.ScoreJoin, jVol, jPitch);

            StartCoroutine(flyA);
            yield return StartCoroutine(flyB);

            int displayedTotal = lettersRunning * mulRunning;
            var uiC = SpawnPop(anchorTotal, displayedTotal);
            uiC.transform.localScale = uiA.transform.localScale;
            uiC.SetColor(uiC.colorTotal);
            uiC.PopByDelta(displayedTotal, tier2Min, tier3Min);
            yield return new WaitForSecondsRealtime(0.8f);

            // โทษพจนานุกรม
            if (dictPenaltyPercent > 0)
            {
                var uiPenalty = SpawnPop(anchorTotal, 0);
                uiPenalty.SetText($"-{dictPenaltyPercent}%");
                if (uiPenalty.text != null) uiPenalty.text.color = Color.red;

                var pRT = (RectTransform)uiPenalty.transform;
                pRT.anchoredPosition += new Vector2(0f, -300f);

                uiPenalty.PopByDelta(1, tier2Min, tier3Min);
                SfxPlayer.Play(SfxId.ScorePenalty);
                yield return StartCoroutine(uiPenalty.FlyTo(anchorTotal, 0.8f));

                int penalized = Mathf.CeilToInt(displayedTotal * (100 - dictPenaltyPercent) / 100f);
                float t = 0f, dur = 0.8f;
                int last = displayedTotal;
                while (t < 1f)
                {
                    t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, dur);
                    int v = Mathf.RoundToInt(Mathf.Lerp(displayedTotal, penalized, 1 - Mathf.Pow(1 - t, 3)));
                    if (v != last) { uiC.SetValue(v); last = v; }
                    yield return null;
                }
                uiC.SetValue(penalized);
                uiC.PopByDelta(Mathf.Max(1, displayedTotal - penalized), tier2Min, tier3Min);

                displayedTotal = penalized;
                yield return new WaitForSecondsRealtime(0.8f);
            }

            // ลอยเข้าหา HUD + อัปเดต HUD ชั่วคราว
            int hudStart  = Score;
            int hudTarget = hudStart + displayedTotal;
            SfxPlayer.PlayForDuration(SfxId.ScoreCommit, flyDur, stretchPitch: true, volumeMul: 1f);
            var fly   = uiC.FlyTo(scoreHud, flyDur);
            var tween = TweenHudScoreTemp(hudStart, hudTarget, flyDur);
            StartCoroutine(tween);
            yield return StartCoroutine(fly);

            AddScore(displayedTotal);
            if (Level1GarbledIT.Instance != null)
                yield return Level1GarbledIT.Instance.ProcessAfterMainScoring();

            // sync กับคะแนนจริง (เผื่อ moveScore สุดท้ายต่าง)
            if (displayedTotal != moveScore)
            {
                yield return StartCoroutine(TweenHudScoreTemp(
                    hudStart + displayedTotal, hudStart + moveScore, 0.15f));
                AddScore(moveScore - displayedTotal);
            }

            // งานท้ายเทิร์น
            foreach (var (t, _) in placed)
                if (!bounced.Contains(t)) t.Lock();

            BenchManager.Instance.RefillEmptySlots();
            UpdateBagUI();
            EnableConfirm();
            if (Level1GarbledIT.Instance != null)
                yield return Level1GarbledIT.Instance.ProcessAfterMainScoring();

            EndScoreSequence();
        }
        finally
        {
            Time.timeScale = prevTimeScale;
            LevelManager.Instance?.ResumeLevelTimer();
        }
    }

    IEnumerator TweenHudScoreTemp(int start, int target, float dur)
    {
        float t = 0f; int last = -1;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.001f, dur);
            int v = Mathf.RoundToInt(Mathf.Lerp(start, target, 1 - Mathf.Pow(1 - t, 3)));
            if (v != last) { if (scoreText) scoreText.text = $"Score : {v}"; last = v; }
            yield return null;
        }
        if (scoreText) scoreText.text = $"Score : {target}";
    }

    void BounceWord(
        MoveValidator.WordInfo w,
        IEnumerable<(LetterTile t, BoardSlot s)> placed,
        Color? flashCol,
        HashSet<LetterTile> bouncedSet)
    {
        var slots = SlotsInWord(w);
        foreach (var (t, s) in placed)
        {
            if (!slots.Contains(s)) continue;

            if (flashCol.HasValue) s.Flash(flashCol.Value, 3, 0.17f);
            else s.Flash(Color.white, 1, 0.08f);

            var tile = s.RemoveLetter();
            if (tile == null) continue;

            BenchManager.Instance.ReturnTileToBench(tile);
            bouncedSet.Add(tile);
        }
    }

    IEnumerator SkipTurnAfterBounce()
    {
        yield return WFS_06;
        EnableConfirm();
    }

    void RejectMove(List<(LetterTile t, BoardSlot s)> tiles, string reason, bool applyPenalty)
    {
        int penalty = 0;
        if (applyPenalty)
        {
            int sum = tiles.Sum(p => p.t.GetData().score);
            penalty = Mathf.CeilToInt(sum * 0.5f);
            Score = Mathf.Max(0, Score - penalty);
            UpdateScoreUI();
            LevelManager.Instance?.OnScoreOrWordProgressChanged();
        }

        foreach (var (t, _) in tiles)
            SpaceManager.Instance.RemoveTile(t);

        LastConfirmedWord = string.Empty;

        string msg = applyPenalty ? $"{reason}  -{penalty}" : reason;
        ShowMessage(msg, Color.red);
        UpdateBagUI();
        EnableConfirm();
        ResetBgmStreak(playSfx:true);
    }

    void OnConfirm()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.IsGameOver())
        {
            EnableConfirm();
            return;
        }

        if (inConfirmProcess) return;
        inConfirmProcess = true;
        if (confirmBtn != null) confirmBtn.interactable = false;

        if (WordChecker.Instance == null || !WordChecker.Instance.IsReady())
        {
            ShowMessage("ระบบตรวจคำยังไม่พร้อม", Color.red);
            EnableConfirm();
            return;
        }

        try
        {
            var bm = BoardManager.Instance;
            if (bm == null || bm.grid == null) { EnableConfirm(); return; }

            var placed = new List<(LetterTile t, BoardSlot s)>();
            int R = bm.grid.GetLength(0), C = bm.grid.GetLength(1);
            for (int r = 0; r < R; r++)
            {
                for (int c = 0; c < C; c++)
                {
                    var sl = bm.grid[r, c];
                    if (sl == null || !sl.HasLetterTile()) continue;
                    var lt = sl.GetLetterTile();
                    if (!lt.isLocked) placed.Add((lt, sl));
                }
            }

            if (placed.Count == 0) { EnableConfirm(); return; }

            if (!MoveValidator.ValidateMove(placed, out var words, out string err))
            {
                RejectMove(placed, err, true);
                return;
            }

            int minLen = WordChecker.Instance?.minWordLength ?? 2;
            bool IsShort(MoveValidator.WordInfo wi)
                => string.IsNullOrWhiteSpace(wi.word) || wi.word.Trim().Length < minLen;

            var shortOnes   = words.Where(IsShort).ToList();
            var invalidDict = words.Except(shortOnes).Where(w => !WordChecker.Instance.IsWordValid(w.word)).ToList();
            var duplicate   = words.Where(w => boardWords.Contains(w.word)).ToList();
            var correct     = words.Except(shortOnes).Except(invalidDict).Except(duplicate).ToList();
            var bounced     = new HashSet<LetterTile>();

            var placedSet = placed.Select(p => (p.s.row, p.s.col)).ToHashSet();
            MoveValidator.WordInfo mainWord;
            bool hasMain;

            if (placed.Count == 1)
            {
                mainWord = words.OrderByDescending(w => (w.word ?? string.Empty).Length).FirstOrDefault();
                hasMain  = !string.IsNullOrEmpty(mainWord.word);
            }
            else
            {
                mainWord = words.FirstOrDefault(w => CountNewInWord(w, placedSet) >= 2);
                hasMain  = !string.IsNullOrEmpty(mainWord.word);
            }

            LastConfirmedWord = hasMain ? mainWord.word : string.Empty;

            int penalty = 0;
            var toBounceRed    = new List<MoveValidator.WordInfo>();
            var toBounceYellow = new List<MoveValidator.WordInfo>();
            var toBounceDup    = new List<MoveValidator.WordInfo>();

            bool mainShort     = hasMain && IsShort(mainWord);
            bool mainInvalid   = hasMain && !mainShort   && invalidDict.Any(w => w.word == mainWord.word);
            bool mainDuplicate = hasMain && duplicate.Any(w => w.word == mainWord.word);

            if (mainShort)
            {
                toBounceYellow.Add(mainWord);
                toBounceYellow.AddRange(shortOnes.Where(w => w.word != mainWord.word));
                toBounceDup.AddRange(duplicate);
                ShowMessage($"คำสั้นเกินไป (ขั้นต่ำ {minLen}) – เด้งกลับ", Color.yellow);
                ResetBgmStreak(playSfx: true);
            }
            else if (mainInvalid)
            {
                // รวม "คะแนนฐาน" ของตัวที่วางในเทิร์นนี้ (ไม่เอาตัวคูณกระดาน)
                int baseSumNewLetters = placed.Sum(p => Mathf.Max(0, p.t.GetData().score));
                int thisPenalty = Mathf.CeilToInt(baseSumNewLetters * 0.75f);

                penalty += thisPenalty;
                toBounceRed.Add(mainWord);  // เด้งคำหลักที่ผิด
                // เด้งคำผิดอื่น ๆ ด้วย แต่ไม่คิดโทษเพิ่มอีก
                foreach (var w in invalidDict.Where(w => w.word != mainWord.word))
                    toBounceRed.Add(w);

                toBounceDup.AddRange(duplicate);
                ShowMessage($"คำผิด -{thisPenalty}", Color.red);
                ResetBgmStreak(playSfx: true);
            }
            else if (mainDuplicate)
            {
                toBounceDup.Add(mainWord);
                toBounceDup.AddRange(duplicate.Where(w => w.word != mainWord.word));
                ShowMessage("คำซ้ำ – เด้งกลับ", Color.yellow);
                ResetBgmStreak(playSfx:true);
            }

            foreach (var w in toBounceRed)    BounceWord(w, placed, Color.red,    bounced);
            foreach (var w in toBounceYellow) BounceWord(w, placed, Color.yellow, bounced);
            foreach (var w in toBounceDup)    BounceWord(w, placed, Color.yellow, bounced);

            bool skipTurn = mainShort || mainInvalid || mainDuplicate;

            // เริ่มจับเวลาเมื่อคอนเฟิร์มครั้งแรก
            LevelManager.Instance?.OnFirstConfirm();

            // Bench Issue (Level 2): mark zero-score บางตัวแบบสุ่ม
            ScoreManager.ClearZeroScoreTiles();
            if (LevelManager.Instance != null &&
                LevelManager.Instance.GetCurrentLevelIndex() == 2 &&
                LevelManager.Instance.Level2_IsBenchIssueActive())
            {
                int k = LevelManager.Instance.Level2_SelectZeroCount(placed.Count);
                if (k > 0)
                {
                    var pool = new List<LetterTile>(placed.Select(p => p.t));
                    var chosen = new List<LetterTile>();
                    for (int i = 0; i < k && pool.Count > 0; i++)
                    {
                        int idx = Random.Range(0, pool.Count);
                        chosen.Add(pool[idx]);
                        pool.RemoveAt(idx);
                    }
                    ScoreManager.MarkZeroScoreTiles(chosen);
                    if (chosen.Count > 0)
                        ShowMessage($"Bench bug: {chosen.Count} letter(s) score 0", Color.yellow);
                }
            }

            int moveScore = 0;
            int newWordCountThisMove = 0;

            if (!skipTurn)
            {
                newWordCountThisMove = correct.Count;
                foreach (var w in correct)
                {
                    if (!boardWords.Contains(w.word))
                    {
                        moveScore += ScoreManager.CalcWord(w.r0, w.c0, w.r1, w.c1);
                        boardWords.Add(w.word);
                    }
                }
            }

            if (skipTurn)
            {
                if (penalty > 0)
                {
                    Score = Mathf.Max(0, Score - penalty);
                    UpdateScoreUI();
                    LevelManager.Instance?.OnScoreOrWordProgressChanged();
                }
                ShowMessage("คำหลักไม่ผ่าน – เสียเทิร์น", Color.red);
                StartCoroutine(SkipTurnAfterBounce());
                return;
            }

            moveScore = Mathf.Max(0, moveScore - penalty);

            int comboMul = Mathf.Clamp(newWordCountThisMove, 1, 4);
            if (comboMul > 1) moveScore = Mathf.CeilToInt(moveScore * comboMul);

            CardManager.Instance?.HoldUI(true);

            foreach (var (tile, slot) in placed)
            {
                if (tile.IsSpecial)
                {
                    Debug.Log($"[Placement] พบตัวพิเศษ {tile.GetData().letter} – เรียก GiveRandomCard()");
                    CardManager.Instance.GiveRandomCard();
                }
                if (slot.manaGain > 0) AddMana(slot.manaGain);
            }

            bool dictPenaltyApplied = false;
            if (usedDictionaryThisTurn)
            {
                if (!freePassActiveThisTurn)
                {
                    moveScore = Mathf.CeilToInt(moveScore * 0.5f);
                    ShowMessage("Penalty: ลดคะแนน 50% จากการเปิดพจนานุกรม", Color.red);
                    dictPenaltyApplied = true;
                }
                usedDictionaryThisTurn = false;
            }

            if (nextWordMul > 1)
            {
                moveScore = Mathf.CeilToInt(moveScore * nextWordMul);
                nextWordMul = 1;
            }

            LevelManager.Instance?.RegisterConfirmedWords(correct.Select(w => w.word));
            ApplyBgmStreakAfterConfirm(dictPenaltyApplied);

            StartCoroutine(AnimateAndFinalizeScoring(
                placed,
                correct,
                moveScore,
                comboMul,
                bounced,
                dictPenaltyApplied ? dictionaryPenaltyPercent : 0
            ));
        }
        finally
        {
            if (inConfirmProcess) inConfirmProcess = false;
        }
    }

    int CountNewInWord(MoveValidator.WordInfo w, HashSet<(int r, int c)> coords)
    {
        int cnt = 0;
        int dr = w.r0 == w.r1 ? 0 : (w.r1 > w.r0 ? 1 : -1);
        int dc = w.c0 == w.c1 ? 0 : (w.c1 > w.c0 ? 1 : -1);
        int r = w.r0, c = w.c0;
        while (true)
        {
            if (coords.Contains((r, c))) cnt++;
            if (r == w.r1 && c == w.c1) break;
            r += dr; c += dc;
        }
        return cnt;
    }

    // helper (ซ้ำของ LevelManager แต่เผื่อจุดอื่นเรียก)
    public int GetCurrentLevelIndex()
    {
        return (LevelManager.Instance != null)
            ? LevelManager.Instance.GetCurrentLevelIndex()
            : 0;
    }
}
