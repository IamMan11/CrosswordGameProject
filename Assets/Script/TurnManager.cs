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
            ShowMessage($"✗ Auto remove '{tile.GetData().letter}' -{penalty}", Color.red);
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

    public void EnableConfirm() => confirmBtn.interactable = true;

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
        var invalid   = words.Where(w => !WordChecker.Instance.IsWordValid(w.word)).ToList();
        var duplicate = words.Where(w => boardWords.Contains(w.word)).ToList();
        var correct   = words.Except(invalid).Except(duplicate).ToList();
        var bounced = new HashSet<LetterTile>();

        // ---------- 2. หา main-word ----------
        var placedSet = placed.Select(p => (p.s.row, p.s.col)).ToHashSet();
        var mainWord  = words.FirstOrDefault(w => CountNewInWord(w, placedSet) >= 2);
        bool hasMain  = !string.IsNullOrEmpty(mainWord.word);
        bool mainCorrect = hasMain
                        && !invalid .Any(w => w.word == mainWord.word)
                        && !duplicate.Any(w => w.word == mainWord.word);

        // ---------- 3. เตรียมคำที่จะเด้ง + เก็บ penalty ----------

        int penalty = 0;                               // โทษหักคะแนนรวม
        var invalidToBounce   = new List<MoveValidator.WordInfo>();
        var duplicateToBounce = new List<MoveValidator.WordInfo>();

        // --- identify main status ---
        bool mainInvalid   = invalid.Any(w   => w.word == mainWord.word);
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
                StartCoroutine(BlinkWordsSequential(correct, Color.green, 1f));
            }
        }

        // ---------- 5. คิดคะแนนคำถูกใหม่ ----------

        int moveScore = 0;
        if (!skipTurn)                                         // main-word ถูกเท่านั้น
        {
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
            ShowMessage("❌ คำหลักผิด/ซ้ำ – เสียเทิร์น", Color.red);

            // รอให้แอนิเมชันเด้งจบก่อนเปลี่ยนเทิร์น
            StartCoroutine(SkipTurnAfterBounce());         // ▶️ Coroutine ด้านล่าง
            return;                                        // **ตัด flow ที่เหลือ**
        }

        // ---------- 7. สรุปคะแนน ----------
        moveScore -= penalty;
        if (moveScore < 0) moveScore = 0;

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

        AddScore(moveScore);

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
            ShowMessage($"Word Correct +{moveScore}", Color.green);
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

    IEnumerator BlinkWords(IEnumerable<MoveValidator.WordInfo> list, Color col)
    {
        foreach (var w in list)
        {
            int dr = w.r0 == w.r1 ? 0 : (w.r1 > w.r0 ? 1 : -1);
            int dc = w.c0 == w.c1 ? 0 : (w.c1 > w.c0 ? 1 : -1);
            int r = w.r0, c = w.c0;
            while (true)
            {
                var slot = BoardManager.Instance.GetSlot(r, c);
                slot?.Flash(col);
                if (r == w.r1 && c == w.c1) break;
                r += dr; c += dc;
            }
        }
        yield return null;
    }
    // กระพริบชุดคำ (list) ด้วยสี col เป็นเวลา totalDuration วินาที
// แต่ละตัวอักษรกระพริบวนทุก interval วินาที
    IEnumerator BlinkWordsForDuration(IEnumerable<MoveValidator.WordInfo> list, Color col, float totalDuration, float interval = 0.2f)
    {
        // เก็บทุกช่องที่ต้องกระพริบ
        var slots = new List<BoardSlot>();
        foreach (var w in list)
        {
            int dr = w.r0 == w.r1 ? 0 : (w.r1 > w.r0 ? 1 : -1);
            int dc = w.c0 == w.c1 ? 0 : (w.c1 > w.c0 ? 1 : -1);
            int r = w.r0, c = w.c0;
            while (true)
            {
                var slot = BoardManager.Instance.GetSlot(r, c);
                if (slot != null) slots.Add(slot);
                if (r == w.r1 && c == w.c1) break;
                r += dr; c += dc;
            }
        }

        float elapsed = 0f;
        bool on = true;
        while (elapsed < totalDuration)
        {
            foreach (var slot in slots)
            {
                if (on) slot.Flash(col);
                else slot.HidePreview();  // สมมติให้ซ่อน effect ของ Flash
            }
            on = !on;
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
        // ปิด highlight ให้เรียบร้อย
        foreach (var slot in slots)
            slot.HidePreview();
    }

    // กระพริบ correctWords นาน 2 วิ แล้วตามด้วย wrongWords อีก 2 วิ
    IEnumerator BlinkCorrectThenWrong(
            IEnumerable<MoveValidator.WordInfo> correctWords,
            IEnumerable<MoveValidator.WordInfo> wrongWords)
    {
        if (correctWords.Any())
            // กระพริบทีละคำ 1 วิ
            yield return StartCoroutine(
                BlinkWordsSequential(correctWords, Color.green, 1f));

        // เว้น 0.2 วิ ระหว่างสองเฟส (เหมือนเดิม)
        yield return new WaitForSeconds(0.2f);

        if (wrongWords.Any())
            // ยังใช้วิธีกระพริบพร้อมกัน 2 วิ
            yield return StartCoroutine(
                BlinkWordsForDuration(wrongWords, Color.red, 2f));
    }
        // รอ totalDelay วิ ก่อนเรียก RejectMove เพื่อให้กระพริบเสร็จ
    IEnumerator DelayedReject(List<(LetterTile t, BoardSlot s)> tiles, string reason, bool applyPenalty, float totalDelay)
    {
        yield return new WaitForSeconds(totalDelay);
        RejectMove(tiles, reason, applyPenalty);
    }
    /// <summary>กระพริบคำทีละคำ (sequential) ด้วยสีที่กำหนด</summary>
    IEnumerator BlinkWordsSequential(
            IEnumerable<MoveValidator.WordInfo> list,
            Color col,
            float perWordDur = 1f)
    {
        foreach (var w in list)
        {
            // ไล่ทุกช่องในคำนี้แล้วสั่ง Flash
            int dr = w.r0 == w.r1 ? 0 : (w.r1 > w.r0 ? 1 : -1);
            int dc = w.c0 == w.c1 ? 0 : (w.c1 > w.c0 ? 1 : -1);
            int r  = w.r0, c = w.c0;
            while (true)
            {
                var slot = BoardManager.Instance.GetSlot(r, c);
                // 3 ครั้ง × 0.17 s ≈ 1 วินาที
                slot?.Flash(col, times: 3, dur: 0.17f);
                if (r == w.r1 && c == w.c1) break;
                r += dr; c += dc;
            }

            // รอให้คำนี้กระพริบครบก่อนจะไปคำถัดไป
            yield return new WaitForSeconds(perWordDur);
        }
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
