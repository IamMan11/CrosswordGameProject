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
    readonly HashSet<string> boardWords = new HashSet<string>();
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
    public GameObject inputBlocker;       // Image เต็มจอที่ Raycast Target = true
    public Animator scoreOverlayAnimator; // ถ้าทำอนิเมชันเฟด/ป้าย
    public TMP_Text phaseLabel;           // ไว้โชว์ “Card Multiplier…”, “Combo x3…”
    public float letterStepDelay = 0.08f;
    public float setDelay        = 0.20f;
    public float phaseDelay      = 0.25f;
    public bool  pauseTimeDuringScoring = true;
    [Header("Score Pop (Anchors & Prefab)")]
    public RectTransform anchorLetters;   // จุด A
    public RectTransform anchorMults;     // จุด B
    public RectTransform anchorTotal;     // จุด C
    public RectTransform scoreHud;        // RectTransform ของข้อความ Score HUD
    public ScorePopUI scorePopPrefab;

    [Header("Score Pop Settings")]
    public int tier2Min = 3;   // ✅ เกณฑ์เด้งระดับกลาง (ปรับได้)
    public int tier3Min = 6;   // ✅ เกณฑ์เด้งระดับใหญ่ (ปรับได้)
    public float stepDelay = 0.08f;
    public float sectionDelay = 0.20f;
    public float flyDur = 0.6f;

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
    void ClearAllSlotFx()
    {
        var grid = BoardManager.Instance.grid;
        int R = BoardManager.Instance.rows, C = BoardManager.Instance.cols;
        for (int r = 0; r < R; r++)
        for (int c = 0; c < C; c++)
        {
            var s = grid[r, c];
            s.CancelFlash();   // ← เมธอดใหม่ใน BoardSlot
            s.HidePreview();   // ← ของเดิมที่มีอยู่แล้ว
        }
    }

    void BeginScoreSequence()
    {
        ClearAllSlotFx();          // ✅ เคลียร์ของค้างก่อนทุกครั้ง
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
        if (scoreOverlayAnimator) scoreOverlayAnimator.SetBool("Scoring", false);
        if (inputBlocker) inputBlocker.SetActive(false);
        ClearAllSlotFx();          // ✅ กันหลงเหลือ
    }

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

        // ✅ แจ้งให้ LevelManager เช็กเงื่อนไขผ่านด่านอีกครั้ง
        LevelManager.Instance?.OnScoreOrWordProgressChanged();
    }

    // ---- Score & UI ----
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

    // ---- Mana ----
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
            if (v != last) { scoreText.text = $"Score : {v}"; last = v; }
            yield return null;
        }
        scoreText.text = $"Score : {target}";
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

    // ---- Turn flags / cards ----
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
    // รวมแฟคเตอร์ตัวคูณแบบ "บวกกัน" ตามที่ต้องการ
    List<int> BuildMultiplierFactors(List<MoveValidator.WordInfo> correct)
    {
        var factors = new List<int>();

        // 2.1 ช่องพิเศษคูณคำ (DW/TW) ต่อ "แต่ละคำ"
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

        // 2.2 การ์ดคูณคำ (ถ้ามี)
        if (ScoreManager.GetWordOverride() > 1)
            factors.Add(ScoreManager.GetWordOverride());

        // 2.3 คอมโบจำนวนคำใหม่ — ไม่ใส่ที่นี่! (ไปทำเป็น step ใน Part 2)
        return factors;
    }

    // รวมแต้มตัวอักษร (รวมคูณ "ตัวอักษร" L2/L3 แล้ว) เป็นทีละก้อนเพื่ออนิเมชัน Part 1
    List<int> BuildLetterAdds(List<MoveValidator.WordInfo> correct)
    {
        var adds = new List<int>();
        foreach (var w in correct)
        {
            foreach (var s in SlotsInWord(w))
            {
                var t = s.GetLetterTile(); if (!t) continue;
                int baseSc = Mathf.Max(0, t.GetData().score);
                int lm = ScoreManager.EffectiveLetterMulFor(s.type); // L2/L3
                adds.Add(baseSc * Mathf.Max(1, lm));
            }
        }
        return adds;
    }
    // รวมลิสต์ (ไทล์, สล็อต, แต้มเพิ่มของไทล์นั้น) ตามลำดับที่ใช้ตรวจคำ
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
    IEnumerator AnimateAndFinalizeScoring(
        List<(LetterTile t, BoardSlot s)> placed,
        List<MoveValidator.WordInfo> correct,
        int moveScore,
        int comboMul,
        HashSet<LetterTile> bounced
    )
    {
        // เริ่มโหมดนับคะแนน (บล็อกอินพุต/หยุดเวลา ถ้าคุณมีฟังก์ชันนี้)
        BeginScoreSequence();

        // ===== ใช้เวอร์ชันใหม่อย่างเดียว =====
        var letterAdds = BuildLetterAdds(correct);        // คะแนนฐานที่คิด L2/L3 แล้ว
        var mulFactors = BuildMultiplierFactors(correct); // แฟคเตอร์คูณแบบ "บวกกัน" (x2+x3=x5)

        int lettersRunning = 0;
        int mulRunning     = 0;

        // ---------- Part 1: ตัวอักษร (จุด A) ----------
        var steps = BuildLetterSteps(correct);
        var uiA = SpawnPop(anchorLetters, 0);

        foreach (var step in steps)
        {
            // เอฟเฟกต์ภาพ: ไฮไลต์ช่อง + เด้งตัวอักษร (ใช้เวลาแบบ unscaled ได้)
            step.s.Flash(Color.white, 1, 0.08f);  // แถบสว่างสั้น ๆ
            step.t.Pulse();                        // ขยายเล็กน้อย (มี fallback โค้ดถ้าไม่มี Trigger)

            lettersRunning += step.add;
            uiA.SetValue(lettersRunning);
            uiA.PopByDelta(step.add, tier2Min, tier3Min);

            yield return new WaitForSecondsRealtime(stepDelay);
        }
        yield return new WaitForSecondsRealtime(sectionDelay);

        // ---------- Part 2: ตัวคูณ (จุด B) ----------
        var uiB = SpawnPop(anchorMults, 0);

        // 2.1 ตัวคูณจาก DW/TW และการ์ด (เดิม)
        foreach (var f in mulFactors)
        {
            mulRunning += f;                   // x2+x3 = x5 (ดีไซน์รวมแบบบวก)
            uiB.SetText("x" + mulRunning);
            uiB.PopByDelta(f, tier2Min, tier3Min);
            yield return new WaitForSecondsRealtime(stepDelay);
        }

        // 2.2 คอมโบจำนวนคำใหม่ → ค่อย ๆ ไฮไลต์ทีละคำจนถึง x4 (ถ้ามากกว่านั้นก็ไม่เกิน 4)
        int comboSteps = Mathf.Min(correct.Count, 4);
        for (int i = 0; i < comboSteps; i++)
        {
            // ไฮไลต์ “คำที่ทำให้เกิดสเต็ปนี้” (จำกัด 4 คำแรก)
            var w = correct[i];
            foreach (var s in SlotsInWord(w))
            {
                var t = s.GetLetterTile();
                if (t) t.Pulse();
                s.Flash(new Color(1f, 0.55f, 0.20f, 1f), 1, 0.08f); // โทนส้มสำหรับคอมโบ
            }

            mulRunning += 1;                   // เพิ่มทีละ +1 ไปจนถึง x4
            uiB.SetText("x" + mulRunning);
            uiB.PopByDelta(1, tier2Min, tier3Min);
            yield return new WaitForSecondsRealtime(stepDelay);
        }

        yield return new WaitForSecondsRealtime(sectionDelay);
        if (mulRunning <= 0) mulRunning = 1;   // กันเคสไม่มีตัวคูณเลย

        // ---------- รวมสองอันเข้ากลาง ----------
        float joinDur = 0.35f;
        var flyA = uiA.FlyTo(anchorTotal, joinDur);
        var flyB = uiB.FlyTo(anchorTotal, joinDur);
        StartCoroutine(flyA);
        yield return StartCoroutine(flyB);

        int displayedTotal = lettersRunning * mulRunning;
        var uiC = SpawnPop(anchorTotal, displayedTotal);
        uiC.transform.localScale = uiA.transform.localScale;
        uiC.PopByDelta(displayedTotal, tier2Min, tier3Min);
        yield return new WaitForSecondsRealtime(0.15f);

        // ---------- ส่งเข้า Score HUD ----------
        int hudStart  = Score;
        int hudTarget = hudStart + displayedTotal;
        var fly = uiC.FlyTo(scoreHud, flyDur);
        var tweenHud = TweenHudScoreTemp(hudStart, hudTarget, flyDur);
        StartCoroutine(tweenHud);
        yield return StartCoroutine(fly);

        // ---------- Commit คะแนนจริง ----------
        AddScore(displayedTotal);

        // ถ้าคะแนนพรีเซนต์ไม่เท่ากับคะแนนระบบ (moveScore) ให้ปรับ HUD ให้ตรง
        if (displayedTotal != moveScore)
        {
            yield return StartCoroutine(
                TweenHudScoreTemp(hudStart + displayedTotal, hudStart + moveScore, 0.15f)
            );
            AddScore(moveScore - displayedTotal);
        }

        // เก็บงานท้ายเทิร์น (ล็อกไทล์/รีฟิล/ฯลฯ ตามของคุณ)
        foreach (var (t, _) in placed) if (!bounced.Contains(t)) t.Lock();
        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();
        EnableConfirm();

        EndScoreSequence(); // ถ้าไม่มีฟังก์ชันนี้ในโปรเจกต์ ให้ลบบรรทัดนี้ได้
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

            var tile = s.RemoveLetter();
            if (tile == null) continue;

            BenchManager.Instance.ReturnTileToBench(tile);

            bouncedSet.Add(tile);            // ← จดว่า “เด้งแล้ว”
        }
    }
    void OnConfirm()
    {
        inConfirmProcess = true;
        confirmBtn.interactable = false;
        var placed = new List<(LetterTile t, BoardSlot s)>();
        foreach (BoardSlot sl in BoardManager.Instance.grid.Cast<BoardSlot>())
        {
            if (!sl.HasLetterTile()) continue;
            var lt = sl.GetLetterTile();
            if (!lt.isLocked) placed.Add((lt, sl));
        }

        if (placed.Count == 0)
        {
            EnableConfirm();
            return;
        }
        if (!MoveValidator.ValidateMove(placed, out var words, out string err))
        {
            RejectMove(placed, err, true);
            return;
        }
        // ---------- 1. แยกหมวดคำ ----------
        var invalid = words.Where(w => !WordChecker.Instance.IsWordValid(w.word)).ToList();
        var duplicate = words.Where(w => boardWords.Contains(w.word)).ToList();
        var correct = words.Except(invalid).Except(duplicate).ToList();
        var bounced = new HashSet<LetterTile>();

        // ---------- 2. หา main-word ----------
        var placedSet = placed.Select(p => (p.s.row, p.s.col)).ToHashSet();
        var mainWord = words.FirstOrDefault(w => CountNewInWord(w, placedSet) >= 2);
        LastConfirmedWord = mainWord.word;
        bool hasMain = !string.IsNullOrEmpty(mainWord.word);
        bool mainCorrect = hasMain
                        && !invalid.Any(w => w.word == mainWord.word)
                        && !duplicate.Any(w => w.word == mainWord.word);

        // ---------- 3. เตรียมคำที่จะเด้ง + เก็บ penalty ----------

        int penalty = 0;                               // โทษหักคะแนนรวม
        var invalidToBounce = new List<MoveValidator.WordInfo>();
        var duplicateToBounce = new List<MoveValidator.WordInfo>();

        // --- identify main status ---
        bool mainInvalid = invalid.Any(w => w.word == mainWord.word);
        bool mainDuplicate = duplicate.Any(w => w.word == mainWord.word);

        // ---------- A) MAIN-word “ผิด” ----------
        if (mainInvalid)          // ตรวจคำผิดก่อน
        {
            // คิดคะแนนก่อนเด้ง
            int s = ScoreManager.CalcWord(mainWord.r0, mainWord.c0,
                                        mainWord.r1, mainWord.c1);
            penalty += Mathf.CeilToInt(s * 0.5f);          // หัก 50 %

            invalidToBounce.Add(mainWord);                 // เด้งทีหลัง (แดง)
            ShowMessage($"คำผิด -{penalty}", Color.red);  // แจ้งผล

            // เอา main ออกจาก list invalid ไม่ให้วนซ้ำอีก
            invalid.RemoveAll(w => w.word == mainWord.word);
        }
        // ---------- B) MAIN-word “ซ้ำ” ----------
        else if (mainDuplicate)
        {
            duplicateToBounce.Add(mainWord);               // เด้งทีหลัง (เหลือง)
            ShowMessage("คำซ้ำ", Color.yellow);

            // เอา main ออกจาก list duplicate
            duplicate.RemoveAll(w => w.word == mainWord.word);
        }

        // ---------- C) cross-word ที่เหลือ เมื่อ main “ผิด” ----------
        if (mainInvalid)              // (ถ้า main ถูก → ข้ามตามกติกา)
        {
            // cross-word ผิด: หัก 50 % + เด้งแดง
            foreach (var w in invalid)
            {
                int s = ScoreManager.CalcWord(w.r0, w.c0, w.r1, w.c1);
                penalty += Mathf.CeilToInt(s * 0.5f);
                invalidToBounce.Add(w);
            }

            // cross-word ซ้ำ: เด้งเหลือง (ไม่หักคะแนน)
            duplicateToBounce.AddRange(duplicate);
        }
        bool skipTurn = mainInvalid || mainDuplicate;
        // ---------- 4. กระพริบคำถูก ----------
        if (!skipTurn)
        {
            if (correct.Count > 0)
            {

            }
        }

        // ---------- 5. คิดคะแนนคำถูกใหม่ ----------
        int moveScore = 0;
        // COMBO: นับจำนวนคำใหม่ของเทิร์นนี้ (คำถูก + ไม่ซ้ำ)
        int newWordCountThisMove = 0;

        if (!skipTurn) // main-word ถูกเท่านั้น
        {
            // จำนวนคำใหม่ = จำนวน "correct" ทั้งหมด (เพราะถูกกรองซ้ำ/ผิดออกแล้ว)
            newWordCountThisMove = correct.Count;

            foreach (var w in correct)
            {
                if (!boardWords.Contains(w.word))
                {
                    int s = ScoreManager.CalcWord(w.r0, w.c0, w.r1, w.c1);  // คะแนนพื้นฐานของคำ

                    // ✅ โบนัสพิเศษ: คำยาว 7 ตัวอักษร
                    if (w.word.Length == 7)
                        s += ScoreManager.GetSevenLetterBonus();

                    moveScore += s;
                    boardWords.Add(w.word);
                }
            }
        }

        // ---------- 6. เด้งคำผิด/ซ้ำ หลังจากคิดคะแนนเสร็จ ----------
        foreach (var w in invalidToBounce)
            BounceWord(w, placed, Color.red, bounced);
        foreach (var w in duplicateToBounce)
            BounceWord(w, placed, Color.yellow, bounced);

        if (skipTurn)
        {
            if (penalty > 0)
            {
                Score = Mathf.Max(0, Score - penalty);
                UpdateScoreUI();
            }
            ShowMessage("คำหลักผิด/ซ้ำ – เสียเทิร์น", Color.red);

            // รอให้แอนิเมชันเด้งจบก่อนเปลี่ยนเทิร์น
            StartCoroutine(SkipTurnAfterBounce());         // ▶️ Coroutine ด้านล่าง
            return;                                        // **ตัด flow ที่เหลือ**
        }

        // ---------- 7. สรุปคะแนน ----------
        moveScore -= penalty;
        if (moveScore < 0) moveScore = 0;

        // COMBO: คูณคะแนนตามจำนวนคำใหม่ (สูงสุด x4)
        int comboMul = Mathf.Clamp(newWordCountThisMove, 1, 4);
        if (comboMul > 1)
        {
            moveScore = Mathf.CeilToInt(moveScore * comboMul);
        }

        foreach (var (tile, slot) in placed)
        {
            if (tile.IsSpecial)
            {
                Debug.Log($"[Placement] พบตัวพิเศษ {tile.GetData().letter} – เรียก GiveRandomCard()");
                CardManager.Instance.GiveRandomCard();
            }
            if (slot.manaGain > 0)
                AddMana(slot.manaGain);
        }

        if (usedDictionaryThisTurn)
        {
            if (!freePassActiveThisTurn)
            {
                // ถ้าไม่ได้ใช้ Free Pass → ลดคะแนนครึ่งนึง
                moveScore = Mathf.CeilToInt(moveScore * 0.5f);
                ShowMessage("Penalty: ลดคะแนน 50% จากการเปิดพจนานุกรม", Color.red);
            }
            // regardless of freePass, รีเซ็ต usedDictionary flag
            usedDictionaryThisTurn = false;
        }

        if (isFirstWord)
        {
            isFirstWord = false;
            LevelManager.Instance.OnFirstConfirm();
        }

        foreach (var (t, _) in placed)
        {
            if (bounced.Contains(t)) continue;   // ข้ามไทล์ที่เด้ง
            t.Lock();                            // ล็อกเฉพาะไทล์ที่ยังบนบอร์ด
        }
        if (!skipTurn)
        {
            string comboText = comboMul > 1 ? $" x{comboMul}" : "";
            ShowMessage($"Word Correct{comboText} +{moveScore}", Color.green);
        }
        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();
        EnableConfirm();

        if (moveScore > 0)
            LevelManager.Instance.ResetTimer();

        // ✅ เพิ่มการเริ่ม AutoRemove ใหม่หลังยืนยันคำ
        if (!LevelManager.Instance.IsGameOver() &&
            LevelManager.Instance.levels[LevelManager.Instance.CurrentLevel].enableAutoRemove)
        {
            float interval = LevelManager.Instance.levels[LevelManager.Instance.CurrentLevel].autoRemoveInterval;
            StartAutoRemove(interval);
        }
        StartCoroutine(AnimateAndFinalizeScoring(
            placed,
            correct,
            moveScore,
            comboMul,
            bounced
        ));
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
    IEnumerator DelayedReject(List<(LetterTile t, BoardSlot s)> tiles, string reason, bool applyPenalty, float totalDelay)
    {
        yield return new WaitForSeconds(totalDelay);
        RejectMove(tiles, reason, applyPenalty);
    }
    private IEnumerator SkipTurnAfterBounce()
    {
        yield return WFS_06;   // ให้เวลาแอนิเมชันเด้ง/แฟลช
        EnableConfirm();       // ✅ ปลดล็อกปุ่มและเคลียร์ inConfirmProcess
    }

    // ---------- Core: Confirm ----------
    void OnConfirm()
    {
        // ✅ กันกดยืนยันตอนจบเกม
        if (LevelManager.Instance != null && LevelManager.Instance.IsGameOver())
        {
            EnableConfirm();
            return;
        }

        if (inConfirmProcess) return;
        inConfirmProcess = true;
        if (confirmBtn != null) confirmBtn.interactable = false;

        try
        {
            var bm = BoardManager.Instance;
            if (bm == null || bm.grid == null)
            {
                EnableConfirm();
                return;
            }

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

            if (!MoveValidator.ValidateMove(placed, out var words, out string err))
            {
                RejectMove(placed, err, true);
                return;
            }

            var invalid   = words.Where(w => !WordChecker.Instance.IsWordValid(w.word)).ToList();
            var duplicate = words.Where(w => boardWords.Contains(w.word)).ToList();
            var correct   = words.Except(invalid).Except(duplicate).ToList();
            var bounced = new HashSet<LetterTile>();

            var placedSet = placed.Select(p => (p.s.row, p.s.col)).ToHashSet();
            var mainWord  = words.FirstOrDefault(w => CountNewInWord(w, placedSet) >= 2);
            LastConfirmedWord = mainWord.word;
            bool hasMain  = !string.IsNullOrEmpty(mainWord.word);
            bool mainCorrect = hasMain
                            && !invalid .Any(w => w.word == mainWord.word)
                            && !duplicate.Any(w => w.word == mainWord.word);

            int penalty = 0;
            var invalidToBounce   = new List<MoveValidator.WordInfo>();
            var duplicateToBounce = new List<MoveValidator.WordInfo>();

            bool mainInvalid   = invalid.Any(w   => w.word == mainWord.word);
            bool mainDuplicate = duplicate.Any(w => w.word == mainWord.word);

            if (mainInvalid)
            {
                int s = ScoreManager.CalcWord(mainWord.r0, mainWord.c0, mainWord.r1, mainWord.c1);
                penalty += Mathf.CeilToInt(s * 0.5f);
                invalidToBounce.Add(mainWord);
                ShowMessage($"คำผิด -{penalty}", Color.red);
                invalid.RemoveAll(w => w.word == mainWord.word);
            }
            else if (mainDuplicate)
            {
                duplicateToBounce.Add(mainWord);
                ShowMessage("คำซ้ำ", Color.yellow);
                duplicate.RemoveAll(w => w.word == mainWord.word);
            }

            if (mainInvalid)
            {
                foreach (var w in invalid)
                {
                    int s = ScoreManager.CalcWord(w.r0, w.c0, w.r1, w.c1);
                    penalty += Mathf.CeilToInt(s * 0.5f);
                    invalidToBounce.Add(w);
                }
                duplicateToBounce.AddRange(duplicate);
            }

            bool skipTurn = mainInvalid || mainDuplicate;

            if (!skipTurn && correct.Count > 0)
                StartCoroutine(BlinkWordsSequential(correct, Color.green, 1f));

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

            foreach (var w in invalidToBounce)   BounceWord(w, placed, Color.red,    bounced);
            foreach (var w in duplicateToBounce) BounceWord(w, placed, Color.yellow, bounced);

            if (skipTurn)
            {
                if (penalty > 0)
                {
                    Score = Mathf.Max(0, Score - penalty);
                    UpdateScoreUI();
                    LevelManager.Instance?.OnScoreOrWordProgressChanged();
                }
                ShowMessage("คำหลักผิด/ซ้ำ – เสียเทิร์น", Color.red);
                StartCoroutine(SkipTurnAfterBounce());  // ✅ จะ EnableConfirm ให้
                return;
            }

            moveScore = Mathf.Max(0, moveScore - penalty);

            int comboMul = Mathf.Clamp(newWordCountThisMove, 1, 4);
            if (comboMul > 1) moveScore = Mathf.CeilToInt(moveScore * comboMul * 1f);

            foreach (var (tile, slot) in placed)
            {
                if (bounced.Contains(tile)) continue;

                if (tile.IsSpecial)
                {
                    Debug.Log($"[Placement] พบตัวพิเศษ {tile.GetData().letter} – เรียก GiveRandomCard()");
                    CardManager.Instance.GiveRandomCard();
                }
                if (slot.manaGain > 0) AddMana(slot.manaGain);
            }

            if (usedDictionaryThisTurn)
            {
                if (!freePassActiveThisTurn)
                {
                    moveScore = Mathf.CeilToInt(moveScore * 0.5f);
                    ShowMessage("Penalty: ลดคะแนน 50% จากการเปิดพจนานุกรม", Color.red);
                }
                usedDictionaryThisTurn = false;
            }

            // ✅ รองรับตัวคูณคะแนนคำถัดไป (ถ้ามีการ์ด/เอฟเฟกต์ตั้งไว้)
            if (nextWordMul > 1)
            {
                moveScore = Mathf.CeilToInt(moveScore * nextWordMul);
                nextWordMul = 1;           // ใช้ครั้งเดียว
            }

            AddScore(moveScore);

            if (isFirstWord)
            {
                isFirstWord = false;
                LevelManager.Instance?.OnFirstConfirm();
            }

            foreach (var (t, _) in placed)
            {
                if (bounced.Contains(t)) continue;
                t.Lock();
            }

            if (moveScore > 0)
            {
                string comboText = comboMul > 1 ? $" x{comboMul}" : "";
                ShowMessage($"Word Correct{comboText} +{moveScore}", Color.green);
            }

            BenchManager.Instance.RefillEmptySlots();
            UpdateBagUI();
            EnableConfirm();

            // เคลียร์สถานะ free pass หลังจบเทิร์น
            freePassActiveThisTurn = false;
        }
        finally
        {
            // เผื่อมี exception ใด ๆ — อย่าให้ปุ่มค้าง
            if (inConfirmProcess) inConfirmProcess = false;
        }
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

        // ✅ กันค่าเก่าค้าง
        LastConfirmedWord = string.Empty;

        string msg = applyPenalty ? $"{reason}  -{penalty}" : reason;
        ShowMessage(msg, Color.red);
        UpdateBagUI();
        EnableConfirm();
    }
}
