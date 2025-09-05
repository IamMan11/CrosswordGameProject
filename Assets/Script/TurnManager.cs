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
    public Button confirmBtn;
    public TMP_Text scoreText;
    public TMP_Text bagCounterText;
    public TMP_Text messageText;

    public int Score { get; private set; }             // คะแนนของด่านปัจจุบัน
    public int TotalScore { get; private set; }        // คะแนนสะสมข้ามด่าน
    public int CheckedWordCount { get; private set; }

    bool usedDictionaryThisTurn = false;
    bool isFirstWord = true;
    bool freePassActiveThisTurn = false;

    Coroutine fadeCo;

    // ✅ จำคำที่เคยยืนยันบนกระดานแบบไม่สนตัวพิมพ์
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

    // ===== Scoring FX / Blocking =====
    public GameObject inputBlocker;       // Image เต็มจอที่ Raycast Target = true
    public Animator scoreOverlayAnimator; // ถ้าทำอนิเมชันเฟด/ป้าย
    public TMP_Text phaseLabel;           // ไว้โชว์ “Card Multiplier…”, “Combo x3…”
    public float letterStepDelay = 0.08f;
    public float setDelay = 0.20f;
    public float phaseDelay = 0.25f;
    public bool pauseTimeDuringScoring = true;

    [Header("Score Pop (Anchors & Prefab)")]
    public RectTransform anchorLetters;   // จุด A
    public RectTransform anchorMults;     // จุด B
    public RectTransform anchorTotal;     // จุด C
    public RectTransform scoreHud;        // RectTransform ของข้อความ Score HUD
    public ScorePopUI scorePopPrefab;

    [Header("Score Pop Settings")]
    public int tier2Min = 3;   // เกณฑ์เด้งระดับกลาง
    public int tier3Min = 6;   // เกณฑ์เด้งระดับใหญ่
    public float stepDelay = 0.08f;
    public float sectionDelay = 0.20f;
    public float flyDur = 0.6f;

    [Header("Dictionary Penalty")]
    [Range(0,100)] public int dictionaryPenaltyPercent = 50;

    // cache yields
    static readonly WaitForSeconds WFS_02 = new WaitForSeconds(0.2f);
    static readonly WaitForSeconds WFS_06 = new WaitForSeconds(0.6f);
    static readonly WaitForSeconds WFS_2s = new WaitForSeconds(2f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (confirmBtn != null)
            confirmBtn.onClick.AddListener(OnConfirm);
        else
            Debug.LogWarning("[TurnManager] confirmBtn not assigned.");
    }

    void OnDisable()
    {
        if (confirmBtn != null)
            confirmBtn.onClick.RemoveListener(OnConfirm);

        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }
        if (manaInfiniteCoroutine != null) { StopCoroutine(manaInfiniteCoroutine); manaInfiniteCoroutine = null; }
    }

    void Start()
    {
        var prog = PlayerProgressSO.Instance.data;
        maxMana = prog.maxMana;
        currentMana = maxMana;
        usageCountThisTurn.Clear();
        UpdateScoreUI();
        UpdateManaUI();
        UpdateBagUI();
    }

    public void ResetTotalScore() => TotalScore = 0;

    void Update()
    {
        if (inConfirmProcess) return;
        if (confirmBtn == null) return;

        // เปิดปุ่มเฉพาะเมื่อมีไทล์บนบอร์ด
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) { confirmBtn.interactable = false; return; }

        bool hasTile = false;
        int rowCount = bm.grid.GetLength(0);
        int colCount = bm.grid.GetLength(1);
        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                var slot = bm.grid[r, c];
                if (slot != null && slot.HasLetterTile())
                {
                    hasTile = true;
                    break;
                }
            }
            if (hasTile) break;
        }
        confirmBtn.interactable = hasTile;
    }

    // ===== FX helpers =====
    void ClearAllSlotFx()
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return;

        var grid = bm.grid;
        int R = bm.rows, C = bm.cols;
        for (int r = 0; r < R; r++)
            for (int c = 0; c < C; c++)
            {
                var s = grid[r, c];
                if (s == null) continue;
                s.CancelFlash();
                s.HidePreview();
            }
    }

    void BeginScoreSequence()
    {
        ClearAllSlotFx();          // เคลียร์ของค้างก่อนทุกครั้ง
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

        // ปิด overlay ด้วยพารามิเตอร์เดียวกับตอนเปิด
        if (scoreOverlayAnimator != null) scoreOverlayAnimator.SetBool("Scoring", false);

        // <<< ปลด hold เพื่อให้ RandomCard UI เปิดหลังนับคะแนนเสร็จ
        CardManager.Instance?.HoldUI(false);
    }

    // ===== Level reset =====
    public void ResetForNewLevel()
    {
        Score = 0;
        CheckedWordCount = 0;
        boardWords.Clear();
        isFirstWord = true;
        if (confirmBtn != null) confirmBtn.interactable = true;
        UpdateScoreUI();
        UpdateBagUI();
        usageCountThisTurn.Clear();
        usedDictionaryThisTurn = false;
        freePassActiveThisTurn = false;
        nextWordMul = 1;

        // แจ้งให้ LevelManager เช็กเงื่อนไขผ่านด่านอีกครั้ง
        LevelManager.Instance?.OnScoreOrWordProgressChanged();
    }

    // ===== Score & UI =====
    public void AddScore(int delta)
    {
        Score = Mathf.Max(0, Score + delta);
        TotalScore = Mathf.Max(0, TotalScore + delta);
        UpdateScoreUI();
        LevelManager.Instance?.OnScoreOrWordProgressChanged();
    }

    void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"Score : {Score}";
    }

    // ===== Mana =====
    public void ActivateInfiniteMana(float duration)
    {
        if (manaInfiniteCoroutine != null) StopCoroutine(manaInfiniteCoroutine);
        infiniteManaMode = true;
        UpdateManaUI();
        ShowMessage("Mana Infinity – ใช้มานาไม่จำกัด!", Color.cyan);
        manaInfiniteCoroutine = StartCoroutine(DeactivateInfiniteManaAfter(duration));
    }

    // ระหว่างลอยเข้าหา HUD ให้เลขบน HUD “ไหล” ไปยังค่าเป้าหมาย (แต่ยังไม่ commit ตัวแปร Score จริง)
    System.Collections.IEnumerator TweenHudScoreTemp(int start, int target, float dur)
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

    private IEnumerator DeactivateInfiniteManaAfter(float duration)
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
        maxMana = Mathf.Max(0, newMax);
        currentMana = Mathf.Clamp(currentMana, 0, maxMana);
        UpdateManaUI();
    }

    void UpdateManaUI()
    {
        if (manaText != null)
            manaText.text = infiniteManaMode
                ? $"Mana: ∞"
                : $"Mana: {currentMana}/{maxMana}";
    }

    // ===== Turn flags / cards =====
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
        if (TileBag.Instance == null) { bagCounterText.text = "—"; return; }
        bagCounterText.text = $"{TileBag.Instance.Remaining}/{TileBag.Instance.TotalInitial}";
    }

    void ShowMessage(string msg, Color? col = null)
    {
        if (messageText == null) return;
        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }

        messageText.text = msg;
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
        messageText.text = string.Empty;
        messageText.color = new Color(start.r, start.g, start.b, 1f); // reset alpha
    }

    public void EnableConfirm()
    {
        inConfirmProcess = false;
        if (confirmBtn == null) return;

        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) { confirmBtn.interactable = false; return; }

        bool hasTile = false;
        int rowCount = bm.grid.GetLength(0);
        int colCount = bm.grid.GetLength(1);
        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                var slot = bm.grid[r, c];
                if (slot != null && slot.HasLetterTile())
                {
                    hasTile = true;
                    break;
                }
            }
            if (hasTile) break;
        }
        confirmBtn.interactable = hasTile;
    }

    public void OnClickDictionaryButton()
    {
        UIConfirmPopup.Show(
            "การเปิดพจนานุกรมจะลดคะแนนคำในเทิร์นนี้ 50%\nยังต้องการเปิดหรือไม่?",
            () => DictionaryUI.Instance.Open(),
            null
        );
    }

    // ===== Helpers (multiplier / letter steps / spawn pop) =====
    List<int> BuildMultiplierFactors(List<MoveValidator.WordInfo> correct)
    {
        var factors = new List<int>();

        // ช่องพิเศษคูณคำ (DW/TW) ต่อ "แต่ละคำ"
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

        // การ์ดคูณคำ (ถ้ามี)
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
                int lm = ScoreManager.EffectiveLetterMulFor(s.type); // DL/TL
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
                int lm = ScoreManager.EffectiveLetterMulFor(s.type); // DL/TL
                steps.Add((t, s, baseSc * Mathf.Max(1, lm)));
            }
        }
        return steps;
    }

    ScorePopUI SpawnPop(RectTransform anchor, int startValue = 0)
    {
        var ui = Instantiate(scorePopPrefab, anchor);
        ui.transform.localPosition = Vector3.zero;
        ui.transform.localScale = Vector3.one;
        ui.SetValue(startValue);
        return ui;
    }

    // ===== Slots helper =====
    List<BoardSlot> SlotsInWord(MoveValidator.WordInfo w)
    {
        var list = new List<BoardSlot>();
        int dr = w.r0 == w.r1 ? 0 : (w.r1 > w.r0 ? 1 : -1);
        int dc = w.c0 == w.c1 ? 0 : (w.c1 > w.c0 ? 1 : -1);
        int r = w.r0, c = w.c0;
        while (true)
        {
            var s = BoardManager.Instance.GetSlot(r, c);
            if (s != null) list.Add(s);
            if (r == w.r1 && c == w.c1) break;
            r += dr; c += dc;
        }
        return list;
    }

    // ===== Scoring animation =====
    IEnumerator AnimateAndFinalizeScoring(
        List<(LetterTile t, BoardSlot s)> placed,
        List<MoveValidator.WordInfo> correct,
        int moveScore,
        int comboMul,
        HashSet<LetterTile> bounced,
        int dictPenaltyPercent
    )
    {
        // ==== เข้าเฟสคิดคะแนน ====
        LevelManager.Instance?.PauseLevelTimer();  // พักนาฬิกาด่าน
        float prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;                       // หยุดเกมเพลย์/อินพุต/ฟิสิกส์

        // ให้แอนิเมชัน UI/เสียงยังเล่นได้ระหว่าง timeScale=0
        if (scoreOverlayAnimator)
            scoreOverlayAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        try
        {
            BeginScoreSequence();

            var letterAdds = BuildLetterAdds(correct);        // คะแนนฐานที่คิด L2/L3 แล้ว (เก็บไว้ถ้าจะโชว์)
            var mulFactors = BuildMultiplierFactors(correct); // ตัวคูณแบบบวก (x2+x3=x5)

            int lettersRunning = 0;
            int mulRunning = 0;

            // Part 1: ตัวอักษร (A)
            var steps = BuildLetterSteps(correct);
            var uiA = SpawnPop(anchorLetters, 0);

            foreach (var step in steps)
            {
                step.s.Flash(Color.white, 1, 0.08f);
                step.t.Pulse();

                lettersRunning += step.add;
                uiA.SetValue(lettersRunning);
                uiA.SetColor(uiA.colorLetters);
                uiA.PopByDelta(step.add, tier2Min, tier3Min);
                SfxPlayer.Play(SfxId.ScoreLetterTick);

                yield return new WaitForSecondsRealtime(stepDelay);
            }
            yield return new WaitForSecondsRealtime(sectionDelay);

            // Part 2: ตัวคูณ (B)
            var uiB = SpawnPop(anchorMults, 0);
            uiB.SetColor(uiB.colorMults);

            foreach (var f in mulFactors)
            {
                mulRunning += f;                   // x2+x3 = x5 (ดีไซน์รวมแบบบวก)
                uiB.SetText("x" + mulRunning);
                uiB.PopByDelta(f, tier2Min, tier3Min);
                SfxPlayer.Play(SfxId.ScoreMultTick);
                yield return new WaitForSecondsRealtime(stepDelay);
            }

            // คอมโบจำนวนคำใหม่ (สูงสุด x4)
            int comboSteps = Mathf.Min(correct.Count, 4);
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
                SfxPlayer.Play(SfxId.ScoreMultTick);
                yield return new WaitForSecondsRealtime(stepDelay);
            }

            yield return new WaitForSecondsRealtime(sectionDelay);
            if (mulRunning <= 0) mulRunning = 1;

            // รวมเข้ากลาง (C)
            float joinDur = 0.35f;
            var flyA = uiA.FlyTo(anchorTotal, joinDur);
            var flyB = uiB.FlyTo(anchorTotal, joinDur);
            SfxPlayer.Play(SfxId.ScoreJoin);
            StartCoroutine(flyA);
            yield return StartCoroutine(flyB);

            int displayedTotal = lettersRunning * mulRunning;
            var uiC = SpawnPop(anchorTotal, displayedTotal);
            uiC.transform.localScale = uiA.transform.localScale;
            uiC.SetColor(uiC.colorTotal);
            uiC.PopByDelta(displayedTotal, tier2Min, tier3Min);
            yield return new WaitForSecondsRealtime(0.8f);

            // ---------- แสดงหัก % ใต้ Total แล้วลอยเข้ามาหา Total ----------
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

            // ส่งเข้า Score HUD
            int hudStart = Score;
            int hudTarget = hudStart + displayedTotal;
            SfxPlayer.Play(SfxId.ScoreCommit);
            var fly = uiC.FlyTo(scoreHud, flyDur);
            var tweenHud = TweenHudScoreTemp(hudStart, hudTarget, flyDur);
            StartCoroutine(tweenHud);
            yield return StartCoroutine(fly);

            AddScore(displayedTotal);

            if (displayedTotal != moveScore)
            {
                yield return StartCoroutine(TweenHudScoreTemp(
                    hudStart + displayedTotal, hudStart + moveScore, 0.15f));
                AddScore(moveScore - displayedTotal);
            }

            // เก็บงานท้ายเทิร์น
            foreach (var (t, _) in placed) if (!bounced.Contains(t)) t.Lock();
            BenchManager.Instance.RefillEmptySlots();
            UpdateBagUI();
            EnableConfirm();

            EndScoreSequence();
        }
        finally
        {
            // ==== ออกจากเฟสคิดคะแนน ====
            Time.timeScale = prevTimeScale;
            LevelManager.Instance?.ResumeLevelTimer(); // เดินนาฬิกาต่อ (ไม่รีเซ็ต)
        }
    }

    // ===== Bounce helpers =====
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

    private IEnumerator SkipTurnAfterBounce()
    {
        yield return WFS_06;   // ให้เวลาแอนิเมชันเด้ง/แฟลช
        EnableConfirm();       // ปลดล็อกปุ่มและเคลียร์ inConfirmProcess
    }

    // ===== Core: Confirm =====
    void OnConfirm()
    {
        // กันกดยืนยันตอนจบเกม
        if (LevelManager.Instance != null && LevelManager.Instance.IsGameOver())
        {
            EnableConfirm();
            return;
        }

        if (inConfirmProcess) return;
        inConfirmProcess = true;
        if (confirmBtn != null) confirmBtn.interactable = false;

        // ✅ กันเคส WordChecker ยังไม่พร้อม
        if (WordChecker.Instance == null || !WordChecker.Instance.IsReady())
        {
            ShowMessage("ระบบตรวจคำยังไม่พร้อม", Color.red);
            EnableConfirm();
            return;
        }

        try
        {
            var bm = BoardManager.Instance;
            if (bm == null || bm.grid == null)
            {
                EnableConfirm();
                return;
            }

            // เก็บไทล์ที่ผู้เล่น “วางใหม่ในเทิร์นนี้” (ไม่รวมที่ล็อกแล้ว)
            var placed = new List<(LetterTile t, BoardSlot s)>();
            int rowCount = bm.grid.GetLength(0);
            int colCount = bm.grid.GetLength(1);
            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    var sl = bm.grid[r, c];
                    if (sl == null || !sl.HasLetterTile()) continue;
                    var lt = sl.GetLetterTile();
                    if (!lt.isLocked) placed.Add((lt, sl));
                }
            }

            if (placed.Count == 0) { EnableConfirm(); return; }

            // ตรวจรูปแบบการวางให้เป็นเส้นตรงและมี main-word
            if (!MoveValidator.ValidateMove(placed, out var words, out string err))
            {
                RejectMove(placed, err, true);
                return;
            }

            // ---------- 1) แยกหมวดคำ ----------
            int minLen = WordChecker.Instance?.minWordLength ?? 2;
            bool IsShort(MoveValidator.WordInfo wi)
                => string.IsNullOrWhiteSpace(wi.word) || wi.word.Trim().Length < minLen;

            var shortOnes = words.Where(IsShort).ToList(); // คำสั้น (< minLen)
            var invalidDict = words.Except(shortOnes)
                                   .Where(w => !WordChecker.Instance.IsWordValid(w.word))
                                   .ToList();
            var duplicate = words.Where(w => boardWords.Contains(w.word)).ToList();
            var correct = words.Except(shortOnes).Except(invalidDict).Except(duplicate).ToList();
            var bounced = new HashSet<LetterTile>();

            // ✅ ถ้าไม่เกิด "คำที่ถูกต้อง" เลย
            if (correct.Count == 0)
            {
                foreach (var (t, s) in placed)
                {
                    s.Flash(Color.yellow, 3, 0.17f);
                    var tile = s.RemoveLetter();
                    if (tile != null)
                    {
                        BenchManager.Instance.ReturnTileToBench(tile);
                        bounced.Add(tile);
                    }
                }

                ShowMessage($"ต้องเกิดคำที่ถูกต้องอย่างน้อย 1 คำ (ยาว ≥ {minLen})", Color.yellow);
                StartCoroutine(SkipTurnAfterBounce());
                return;
            }

            // ---------- 2) หา main-word ----------
            var placedSet = placed.Select(p => (p.s.row, p.s.col)).ToHashSet();
            MoveValidator.WordInfo mainWord;
            bool hasMain;

            if (placed.Count == 1)
            {
                // วางแค่ 1 ตัว: นิยามคำหลักเป็น “คำที่ยาวที่สุด” จาก H/V รอบช่องที่วาง
                mainWord = words
                    .OrderByDescending(w => (w.word ?? string.Empty).Length)
                    .FirstOrDefault();
                hasMain = !string.IsNullOrEmpty(mainWord.word);
            }
            else
            {
                // วางหลายตัว: ใช้เกณฑ์เดิม ต้องมีตัวใหม่ ≥ 2 ในเส้นนั้น
                mainWord = words.FirstOrDefault(w => CountNewInWord(w, placedSet) >= 2);
                hasMain = !string.IsNullOrEmpty(mainWord.word);
            }

            LastConfirmedWord = hasMain ? mainWord.word : string.Empty;

            // ---------- 3) เตรียมคำที่จะเด้ง + โทษ ----------
            int penalty = 0;
            var toBounceRed = new List<MoveValidator.WordInfo>();       // ผิดดิก → หักโทษ
            var toBounceYellow = new List<MoveValidator.WordInfo>();    // คำสั้น/เตือน → ไม่หัก
            var toBounceDup = new List<MoveValidator.WordInfo>();       // ซ้ำ → ไม่หัก

            bool mainShort = hasMain && IsShort(mainWord);
            bool mainInvalid = hasMain && !mainShort && invalidDict.Any(w => w.word == mainWord.word);
            bool mainDuplicate = hasMain && duplicate.Any(w => w.word == mainWord.word);

            // A) MAIN สั้นเกินไป
            if (mainShort)
            {
                toBounceYellow.Add(mainWord);
                toBounceYellow.AddRange(shortOnes.Where(w => w.word != mainWord.word));
                toBounceDup.AddRange(duplicate);
                ShowMessage($"คำสั้นเกินไป (ขั้นต่ำ {minLen}) – เด้งกลับ", Color.yellow);
            }
            // B) MAIN ผิดดิก → หักโทษ 50%
            else if (mainInvalid)
            {
                int s = ScoreManager.CalcWord(mainWord.r0, mainWord.c0, mainWord.r1, mainWord.c1);
                penalty += Mathf.CeilToInt(s * 0.5f);
                toBounceRed.Add(mainWord);
                ShowMessage($"คำผิด -{penalty}", Color.red);

                foreach (var w in invalidDict.Where(w => w.word != mainWord.word))
                {
                    int sc = ScoreManager.CalcWord(w.r0, w.c0, w.r1, w.c1);
                    penalty += Mathf.CeilToInt(sc * 0.5f);
                    toBounceRed.Add(w);
                }
                toBounceDup.AddRange(duplicate);
            }
            // C) MAIN ซ้ำ
            else if (mainDuplicate)
            {
                toBounceDup.Add(mainWord);
                toBounceDup.AddRange(duplicate.Where(w => w.word != mainWord.word));
                ShowMessage("คำซ้ำ – เด้งกลับ", Color.yellow);
            }

            // เด้งตามสี
            foreach (var w in toBounceRed)    BounceWord(w, placed, Color.red,    bounced);    // หักโทษ
            foreach (var w in toBounceYellow) BounceWord(w, placed, Color.yellow, bounced);    // ไม่หัก
            foreach (var w in toBounceDup)    BounceWord(w, placed, Color.yellow, bounced);    // ไม่หัก

            // เสียเทิร์นเมื่อ main-word สั้น / ผิดดิก / ซ้ำ
            bool skipTurn = mainShort || mainInvalid || mainDuplicate;

            // เริ่มนับเวลาของด่าน เมื่อกดยืนยันครั้งแรก
            LevelManager.Instance?.OnFirstConfirm();

            // ---------- 5) คำนวณคะแนน ----------
            int moveScore = 0;
            int newWordCountThisMove = 0;

            if (!skipTurn) // main-word ถูกเท่านั้น
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

            // COMBO...
            int comboMul = Mathf.Clamp(newWordCountThisMove, 1, 4);
            if (comboMul > 1) { moveScore = Mathf.CeilToInt(moveScore * comboMul); }

            // <<< ใส่บรรทัดนี้ ก่อนเริ่ม loop ที่อาจแจกการ์ดพิเศษ
            CardManager.Instance?.HoldUI(true);

            // แจกการ์ดจาก special letter / เก็บมานา
            foreach (var (tile, slot) in placed)
            {
                if (tile.IsSpecial)
                {
                    Debug.Log($"[Placement] พบตัวพิเศษ {tile.GetData().letter} – เรียก GiveRandomCard()");
                    CardManager.Instance.GiveRandomCard();  // ยัง enqueue ได้ปกติ แต่ UI จะยังไม่โผล่
                }
                if (slot.manaGain > 0) AddMana(slot.manaGain);
            }

            // โทษเปิดพจนานุกรม (จำ flag ไว้เพื่อใช้โชว์อนิเมชันหัก %)
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

            // ตัวคูณจากเอฟเฟกต์พิเศษ (ครั้งเดียว)
            if (nextWordMul > 1)
            {
                moveScore = Mathf.CeilToInt(moveScore * nextWordMul);
                nextWordMul = 1;
            }

            // 🟢 รายงาน “คำที่ถูก + ไม่ซ้ำ” ของเทิร์นนี้ให้ LevelManager (ใช้เฉพาะด่าน 1)
            LevelManager.Instance?.RegisterConfirmedWords(correct.Select(w => w.word));

            // เรียกคอร์รุตีน
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
            // เผื่อมี exception ใด ๆ — อย่าให้ปุ่มค้าง
            if (inConfirmProcess) inConfirmProcess = false;
        }

        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();
        EnableConfirm();
        return;
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

        // กันค่าเก่าค้าง
        LastConfirmedWord = string.Empty;

        string msg = applyPenalty ? $"{reason}  -{penalty}" : reason;
        ShowMessage(msg, Color.red);
        UpdateBagUI();
        EnableConfirm();
    }
}
