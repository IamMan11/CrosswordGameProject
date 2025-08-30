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
    private bool freePassActiveThisTurn = false;

    Coroutine fadeCo;
    Coroutine autoRemoveCo;
    readonly HashSet<string> boardWords = new();
    int nextWordMul = 1;

    [Header("Mana System")]
    public int maxMana = 10;
    public int currentMana;
    [SerializeField] private TMP_Text manaText;
    private bool infiniteManaMode = false;
    private Coroutine manaInfiniteCoroutine = null;
    private Dictionary<string, int> usageCountThisTurn = new Dictionary<string, int>();
    public string LastConfirmedWord { get; private set; }
    bool inConfirmProcess = false;
    public GameObject inputBlocker;       // Image เต็มจอที่ Raycast Target = true
    public Animator scoreOverlayAnimator; // ถ้าทำอนิเมชันเฟด/ป้าย
    public TMP_Text phaseLabel;           // ไว้โชว์ “Card Multiplier…”, “Combo x3…”
    public float letterStepDelay = 0.08f;
    public float setDelay        = 0.20f;
    public float phaseDelay      = 0.25f;
    public bool  pauseTimeDuringScoring = true;

    void Awake()
    {
        Instance = this;
        confirmBtn.onClick.AddListener(OnConfirm);
        currentMana = 0;
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

    public void ResetTotalScore()
    {
        TotalScore = 0;
    }
    void Update()
    {
        // ถ้าไม่มี BoardManager หรือกำลังประมวลผล ก็ข้าม
        if (inConfirmProcess || BoardManager.Instance == null) return;

        // ตรวจว่ามีตัวอักษรบนบอร์ดหรือไม่
        bool hasTile = BoardManager.Instance.grid
                        .Cast<BoardSlot>()
                        .Any(s => s.HasLetterTile());

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
        confirmBtn.interactable = true;
        StopAutoRemove();
        UpdateScoreUI();
    }

    public void AddScore(int delta)
    {
        Score += delta;
        TotalScore += delta;
        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        scoreText.text = $"Score : {Score}";
    }

    public void ActivateInfiniteMana(float duration)
    {
        if (manaInfiniteCoroutine != null)
            StopCoroutine(manaInfiniteCoroutine);

        infiniteManaMode = true;
        UpdateManaUI();
        ShowMessage("Mana Infinity – ใช้มานาไม่จำกัด 1 นาที!", Color.cyan);

        manaInfiniteCoroutine = StartCoroutine(DeactivateInfiniteManaAfter(duration));
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
        maxMana = newMax;
        currentMana = Mathf.Min(currentMana, maxMana);
        UpdateManaUI();
    }

    void UpdateManaUI()
    {
        if (manaText != null)
            manaText.text = infiniteManaMode
                ? $"Mana: ∞"
                : $"Mana: {currentMana}/{maxMana}";
    }

    public void SetDictionaryUsed() => usedDictionaryThisTurn = true;

    public void ApplyFreePass()
    {
        freePassActiveThisTurn = true;
        ShowMessage("Free Pass – ยกเลิกโทษการเปิดพจนานุกรมในเทิร์นนี้!", Color.cyan);
    }

    public void SetScoreMultiplier(int mul)
    {
        nextWordMul = Mathf.Max(1, mul);
    }

    public void OnWordChecked(bool isCorrect)
    {
        if (isCorrect) CheckedWordCount++;
    }

    public void StartAutoRemove(float interval)
    {
        StopAutoRemove();
        autoRemoveCo = StartCoroutine(AutoRemoveLetterCoroutine(interval));
    }

    public void StopAutoRemove()
    {
        if (autoRemoveCo != null)
        {
            StopCoroutine(autoRemoveCo);
            autoRemoveCo = null;
        }
    }

    IEnumerator AutoRemoveLetterCoroutine(float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            if (confirmBtn.interactable)
                RemoveOneLetter();
            else
                yield break;
        }
    }

    void RemoveOneLetter()
    {
        var slots = BoardManager.Instance.grid
                        .Cast<BoardSlot>()
                        .Where(s => s.HasLetterTile())
                        .ToList();
        if (slots.Count == 0) return;

        var slot = slots[Random.Range(0, slots.Count)];
        var tile = slot.RemoveLetter();
        if (tile != null)
        {
            Destroy(tile.gameObject);
            int penalty = tile.GetData().score;
            Score = Mathf.Max(0, Score - penalty);
            UpdateScoreUI();
            ShowMessage($"Auto remove '{tile.GetData().letter}' -{penalty}", Color.red);
        }
    }

    public void AutoRemoveNow() => RemoveOneLetter();

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
        if (!usageCountThisTurn.ContainsKey(card.id))
            usageCountThisTurn[card.id] = 1;
        else
            usageCountThisTurn[card.id]++;
    }

    public int GetUsageCount(CardData card)
    {
        if (card == null) return 0;
        if (!usageCountThisTurn.ContainsKey(card.id)) return 0;
        return usageCountThisTurn[card.id];
    }

    public void UpdateBagUI()
        => bagCounterText.text = $"{TileBag.Instance.Remaining}/{TileBag.Instance.TotalInitial}";

    void ShowMessage(string msg, Color? col = null)
    {
        if (messageText == null) return;
        if (fadeCo != null) StopCoroutine(fadeCo);
        messageText.text = msg;
        messageText.color = col ?? Color.white;
        if (!string.IsNullOrEmpty(msg))
            fadeCo = StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(2f);
        float t = 0f;
        Color start = messageText.color;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            messageText.color = new Color(start.r, start.g, start.b, 1 - t);
            yield return null;
        }
        messageText.text = string.Empty;
    }

    public void EnableConfirm()
    {
        inConfirmProcess = false;

        // ถ้ามีตัวอักษรบนบอร์ด จึงเปิดกด
        bool hasTile = BoardManager.Instance.grid
                        .Cast<BoardSlot>()
                        .Any(s => s.HasLetterTile());
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
    List<BoardSlot> SlotsInWord(MoveValidator.WordInfo w)
    {
        var list = new List<BoardSlot>();
        int dr = w.r0 == w.r1 ? 0 : (w.r1 > w.r0 ? 1 : -1);
        int dc = w.c0 == w.c1 ? 0 : (w.c1 > w.c0 ? 1 : -1);
        int r = w.r0, c = w.c0;
        while (true)
        {
            list.Add(BoardManager.Instance.GetSlot(r, c));
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
        BeginScoreSequence();

        int startScore = Score, shown = startScore;
        scoreText.text = $"Score : {shown}";

        // =============== เฟส 1: บวก "คะแนนดิบ" ทีละตัวอักษร ===============
        foreach (var w in correct)
        {
            var slots = SlotsInWord(w);
            foreach (var slot in slots)
            {
                var tile = slot.GetLetterTile();
                if (!tile) continue;
                tile.Pulse();                // Animator Trigger (หรือ fallback)
                slot.Flash(Color.green, 1, 0.08f);
                shown += Mathf.Max(0, tile.GetData().score);
                scoreText.text = $"Score : {shown}";
                yield return new WaitForSecondsRealtime(letterStepDelay);
            }
            yield return new WaitForSecondsRealtime(setDelay);
        }
        yield return new WaitForSecondsRealtime(phaseDelay);

        // =============== เฟส 2: ช่องพิเศษ (DL/TL/DW/TW) ===============
        bool usedLetterOverride = false, usedWordOverride = false;
        foreach (var w in correct)
        {
            var slots = SlotsInWord(w);

            // 2.1 DL/TL – บวก “ส่วนเพิ่ม” ของแต่ละตัวอักษร
            foreach (var slot in slots)
            {
                var tile = slot.GetLetterTile();
                if (!tile) continue;
                int baseSc = Mathf.Max(0, tile.GetData().score);
                int mul = ScoreManager.EffectiveLetterMulFor(slot.type);
                if (mul > 1 && (slot.type == SlotType.DoubleLetter || slot.type == SlotType.TripleLetter))
                {
                    slot.Flash(new Color(0.2f,0.6f,1f,1f), 1, 0.1f);
                    tile.Pulse();
                    int extra = baseSc * (mul - 1);
                    shown += extra;
                    scoreText.text = $"Score : {shown}";
                    if (ScoreManager.GetLetterOverride() > 0) usedLetterOverride = true;
                    yield return new WaitForSecondsRealtime(letterStepDelay);
                }
            }

            // 2.2 DW/TW – บวก “ส่วนเพิ่มทั้งคำ”
            int subtotal = 0, wordMul = 1;
            foreach (var slot in slots)
            {
                var tile = slot.GetLetterTile(); if (!tile) continue;
                int baseSc = Mathf.Max(0, tile.GetData().score);
                subtotal += baseSc * Mathf.Max(1, ScoreManager.EffectiveLetterMulFor(slot.type));
                int wm = ScoreManager.EffectiveWordMulFor(slot.type);
                if (wm > 1) wordMul *= wm;
            }
            if (wordMul > 1)
            {
                foreach (var s in slots)
                    if (s.type == SlotType.DoubleWord || s.type == SlotType.TripleWord)
                        s.Flash(new Color(1f,0.25f,0.25f,1f), 2, 0.1f);

                int extra = subtotal * (wordMul - 1);
                shown += extra;
                scoreText.text = $"Score : {shown}";
                if (ScoreManager.GetWordOverride() > 0) usedWordOverride = true;
                yield return new WaitForSecondsRealtime(setDelay);
            }
        }
        yield return new WaitForSecondsRealtime(phaseDelay);

        // =============== เฟส 3: ป้ายตัวคูณจากการ์ด (โชว์เฉย ๆ ถ้ามีใช้จริง) ===============
        if (usedLetterOverride || usedWordOverride)
        {
            if (phaseLabel)
            {
                string msg = "Card Multiplier:";
                if (usedLetterOverride) msg += $" Letter x{ScoreManager.GetLetterOverride()}";
                if (usedWordOverride)   msg += $" Word x{ScoreManager.GetWordOverride()}";
                phaseLabel.text = msg;
            }
            yield return new WaitForSecondsRealtime(0.6f);
        }

        // =============== เฟส 4: คอมโบจำนวนคำใหม่ (x2→x3→... สูงสุด x4) ===============
        if (comboMul > 1)
        {
            int baseAfterSpecial = shown - startScore;
            int highlightLimit = Mathf.Min(4, correct.Count);
            for (int m = 2; m <= comboMul; m++)
            {
                // ไฮไลต์ทีละคำ (ไม่เกิน 4)
                for (int i = 0; i < Mathf.Min(m, highlightLimit); i++)
                    foreach (var s in SlotsInWord(correct[i]))
                        s.Flash(new Color(0.2f,1f,1f,1f), 1, 0.08f);

                if (phaseLabel) phaseLabel.text = $"Combo x{m}";
                shown += baseAfterSpecial;                  //ดันตัวเลขขึ้นทีละก้อน
                scoreText.text = $"Score : {shown}";
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }

        // ===== Snap เข้าผลลัพธ์จริงจากระบบเดิม (กันปัดเศษ/โทษ) =====
        int target = startScore + moveScore;
        if (shown != target)
        {
            shown = target;
            scoreText.text = $"Score : {shown}";
            yield return new WaitForSecondsRealtime(0.1f);
        }

        // ===== จบพิธี: คืนระบบเดิมทั้งหมด =====
        EndScoreSequence();

        // อัปคะแนนจริง, ล็อกไทล์, รีเฟรชตาม flow เดิม
        AddScore(moveScore);
        foreach (var (t, _) in placed) if (!bounced.Contains(t)) t.Lock();
        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();
        EnableConfirm();
    }

    // เด้งตัวอักษรใน word กลับ Bench + กระพริบ (ถ้ามีสี)
    void BounceWord(MoveValidator.WordInfo w,
                    IEnumerable<(LetterTile t, BoardSlot s)> placed,
                    Color? flashCol,
                    HashSet<LetterTile> bouncedSet)          // ← เพิ่มพารามิเตอร์
    {
        var slots = SlotsInWord(w);
        foreach (var (t, s) in placed)
        {
            if (!slots.Contains(s)) continue;

            if (flashCol.HasValue) s.Flash(flashCol.Value, 3, 0.17f);

            // ดึงออกก่อน
            LetterTile tile = s.RemoveLetter();
            if (tile == null) continue;

            // ย้ายเข้า Bench ช่องว่างซ้ายสุด
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
                    moveScore += ScoreManager.CalcWord(w.r0, w.c0, w.r1, w.c1);
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
        yield return new WaitForSeconds(0.6f);   // ให้เวลาบลิ๊ง/เด้งตาม effect
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
        }

        foreach (var (t, _) in tiles)
            SpaceManager.Instance.RemoveTile(t);

        string msg = applyPenalty
            ? $"{reason}  -{penalty}"
            : reason;
        ShowMessage(msg, Color.red);
        UpdateBagUI();
        EnableConfirm();
    }
}
