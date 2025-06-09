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

    public int Score { get; private set; }
    public int CheckedWordCount { get; private set; }

    bool usedDictionaryThisTurn = false;
    bool isFirstWord = true;

    Coroutine fadeCo;
    Coroutine autoRemoveCo;
    readonly HashSet<string> boardWords = new();
    int nextWordMul = 1;

    [Header("Mana System")]
    public int maxMana = 10;
    public int currentMana;
    [SerializeField] private TMP_Text manaText;

    void Awake()
    {
        Instance = this;
        confirmBtn.onClick.AddListener(OnConfirm);
        currentMana = 0;
    }

    void Start()
    {
        UpdateScoreUI();
        UpdateManaUI();
    }

    public void AddMana(int amount)
    {
        currentMana = Mathf.Min(maxMana, currentMana + amount);
        UpdateManaUI();
        ShowMessage($"+{amount} Mana", Color.cyan);
    }

    public bool UseMana(int cost)
    {
        if (currentMana < cost) return false;
        currentMana -= cost;
        UpdateManaUI();
        return true;
    }

    void UpdateManaUI()
    {
        if (manaText != null)
            manaText.text = $"Mana: {currentMana}/{maxMana}";
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
        UpdateScoreUI();
    }

    public void SetDictionaryUsed() => usedDictionaryThisTurn = true;

    public void SetScoreMultiplier(int mul)
    {
        nextWordMul = Mathf.Max(1, mul);
    }

    public void OnWordChecked(bool isCorrect)
    {
        if (isCorrect) CheckedWordCount++;
    }

    void UpdateScoreUI()
    {
        scoreText.text = $"Score : {Score}";
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

    public void AutoRemoveNow()
    {
        RemoveOneLetter();
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

        var newCoords = placed.Select(p => (p.s.row, p.s.col)).ToHashSet();
        var newWords = new List<MoveValidator.WordInfo>();
        var linkWords = new List<MoveValidator.WordInfo>();
        foreach (var w in words)
        {
            int cnt = CountNewInWord(w, newCoords);
            if (cnt >= 2) newWords.Add(w);
            else linkWords.Add(w);
        }

        if (newWords.Count == 0 && linkWords.Count > 0)
        {
            newWords.Add(linkWords[0]);
            linkWords.RemoveAt(0);
        }

        var wrongNew = newWords.Where(w => !WordChecker.Instance.IsWordValid(w.word)).ToList();
        var dupNew = newWords.Where(w => boardWords.Contains(w.word)).ToList();

        if (isFirstWord && (wrongNew.Count > 0 || dupNew.Count > 0))
        {
            StartCoroutine(BlinkWords(wrongNew.Concat(dupNew), Color.red));
            RejectMove(placed, "✗ คำแรกต้องเป็นคำใหม่ที่ถูกต้อง", true);
            return;
        }

        if (wrongNew.Count > 0 || dupNew.Count > 0)
        {
            if (wrongNew.Count > 0)
            {
                StartCoroutine(BlinkWords(wrongNew, Color.red));
                RejectMove(placed, "invalid word", true);
            }
            else
            {
                StartCoroutine(BlinkWords(dupNew, Color.yellow));
                RejectMove(placed, "duplicate word", false);
            }
            return;
        }

        var wrongLink = linkWords.Where(w => !WordChecker.Instance.IsWordValid(w.word)).ToList();
        if (wrongLink.Count > 0)
            StartCoroutine(BlinkWords(wrongLink, Color.red));

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

        int moveScore = 0;
        foreach (var w in newWords)
        {
            if (!boardWords.Contains(w.word))
            {
                moveScore += ScoreManager.CalcWord(w.r0, w.c0, w.r1, w.c1);
                boardWords.Add(w.word);
            }
        }

        if (usedDictionaryThisTurn)
        {
            moveScore = Mathf.CeilToInt(moveScore * 0.5f);
            usedDictionaryThisTurn = false;
        }

        AddScore(moveScore);

        if (isFirstWord)
        {
            isFirstWord = false;
            LevelManager.Instance.OnFirstConfirm();
        }

        foreach (var (t, _) in placed) t.Lock();
        ShowMessage($"✓ +{moveScore}", Color.green);
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

    void UpdateBagUI()
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
}
