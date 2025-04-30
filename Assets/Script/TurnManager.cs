using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TurnManager – ตรวจคำใหม่ก่อนคำเชื่อม   (V2)
/// • คำผิด (invalid)  → กระพริบแดง + เด้งตัว + หัก 50 % คะแนนตัวอักษรที่วาง
/// • คำซ้ำ (duplicate) → กระพริบเหลือง + เด้งตัว   (ไม่หักคะแนน)
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("UI")]
    public Button   confirmBtn;
    public TMP_Text scoreText;
    public TMP_Text bagCounterText;
    public TMP_Text messageText;

    Coroutine fadeCo;

    int score = 0;
    readonly HashSet<string> boardWords = new();

    void Awake()
    {
        Instance = this;
        confirmBtn.onClick.AddListener(OnConfirm);
    }

    // =========================================================
    void OnConfirm()
    {
        confirmBtn.interactable = false;

        // 1) เก็บ tile ที่เพิ่งวาง
        var placed = new List<(LetterTile t, BoardSlot s)>();
        foreach (var sl in BoardManager.Instance.grid)
        {
            if (sl == null || !sl.HasLetterTile()) continue;
            var lt = sl.GetLetterTile();
            if (!lt.isLocked) placed.Add((lt, sl));
        }
        if (placed.Count == 0) { EnableConfirm(); return; }

        // 2) ตรวจ pattern
        if (!MoveValidator.ValidateMove(placed, out var words, out string err))
        {
            RejectMove(placed, err, true); // always apply penalty (pattern ผิด)
            return;
        }

        // 3) แยก new / link
        var newCoords = placed.Select(p => (p.s.row, p.s.col)).ToHashSet();
        var newWords  = new List<MoveValidator.WordInfo>();
        var linkWords = new List<MoveValidator.WordInfo>();

        foreach (var w in words)
        {
            int newCnt = CountNewInWord(w, newCoords);
            if (newCnt >= 2) newWords.Add(w);
            else             linkWords.Add(w);
        }
        if (newWords.Count == 0 && linkWords.Count > 0)
        {
            newWords.Add(linkWords[0]);
            linkWords.RemoveAt(0);
        }

        // 4) เช็กคำใหม่ก่อน
        var wrongNew = new List<MoveValidator.WordInfo>();
        var dupNew   = new List<MoveValidator.WordInfo>();
        foreach (var w in newWords)
        {
            bool valid = WordChecker.Instance.IsWordValid(w.word);
            bool dup   = boardWords.Contains(w.word);
            if      (!valid) wrongNew.Add(w);
            else if (dup)    dupNew.Add(w);
        }
        if (wrongNew.Count > 0 || dupNew.Count > 0)
        {
            if (wrongNew.Count > 0)
            {
                StartCoroutine(BlinkWords(wrongNew, Color.red));
                RejectMove(placed, "invalid word", false); // ‼️ หัก 50 %
            }
            else
            {
                StartCoroutine(BlinkWords(dupNew, Color.yellow));
                RejectMove(placed, "duplicate word", false); // ไม่หักแต้ม
            }
            return;
        }

        // 5) เช็กคำเชื่อม (ถ้าผิดกระพริบแดงเฉย ๆ)
        var wrongLink = linkWords.Where(w => !WordChecker.Instance.IsWordValid(w.word)).ToList();
        if (wrongLink.Count > 0) StartCoroutine(BlinkWords(wrongLink, Color.red));

        // 6) คำนวณคะแนนคำใหม่
        int moveScore = 0;
        foreach (var w in newWords)
        {
            if (!boardWords.Contains(w.word))
            {
                moveScore += ScoreManager.CalcWord(w.r0, w.c0, w.r1, w.c1);
                boardWords.Add(w.word);
            }
        }

        // 7) ล็อก + UI
        score += moveScore;
        scoreText.text = $"Score : {score}";
        foreach (var (t, _) in placed) t.Lock();

        ShowMessage($"✓ +{moveScore}", Color.green);
        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();
        EnableConfirm();
    }

    // =========================================================
    #region Helper

    int CountNewInWord(MoveValidator.WordInfo w, HashSet<(int r,int c)> coords)
    {
        int cnt = 0;
        int dr = w.r0 == w.r1 ? 0 : (w.r1 > w.r0 ? 1 : -1);
        int dc = w.c0 == w.c1 ? 0 : (w.c1 > w.c0 ? 1 : -1);
        int r = w.r0, c = w.c0;
        while (true)
        {
            if (coords.Contains((r,c))) cnt++;
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
            score = Mathf.Max(0, score - penalty);
            scoreText.text = $"Score : {score}";
        }

        foreach (var (t, _) in tiles) SpaceManager.Instance.RemoveTile(t);

        string msg = applyPenalty ? $"✗ {reason}  -{penalty}" : $"✗ {reason}";
        ShowMessage(msg, Color.red);
        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();
        EnableConfirm();
    }

    void UpdateBagUI() => bagCounterText.text = $"{TileBag.Instance.Remaining}/{TileBag.Instance.TotalInitial}";

    void ShowMessage(string msg, Color? col = null)
    {
        if (messageText == null) return;
        if (fadeCo != null) StopCoroutine(fadeCo);
        messageText.text = msg;
        messageText.color = col ?? Color.white;
        if (!string.IsNullOrEmpty(msg)) fadeCo = StartCoroutine(FadeOut());
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

    #endregion
}
