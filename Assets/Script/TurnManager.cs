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

    Coroutine fadeCo;

    // ---------- runtime ----------
    int score = 0;
    HashSet<string> boardWords = new();           // ★ เก็บคำทั้งหมดบนกระดาน

    void Awake()
    {
        Instance = this;
        confirmBtn.onClick.AddListener(OnConfirm);
    }

    // =========================================================
    void OnConfirm()
    {
        confirmBtn.interactable = false;

        // ถ้า Board ยังไม่พร้อม อย่าดำเนินต่อ
        if (BoardManager.Instance == null || BoardManager.Instance.grid == null)
        {
            Debug.LogError("[TurnManager] BoardManager ยังไม่พร้อม");
            EnableConfirm();
            return;
        }

        // 1) เก็บ LetterTile “ใหม่” บนบอร์ด
        var placed = new List<(LetterTile t, BoardSlot s)>();
        foreach (var sl in BoardManager.Instance.grid)
        {
            if (sl == null) continue;            // กัน null slot
            if (!sl.HasLetterTile()) continue;

            var lt = sl.GetLetterTile();
            if (lt != null && !lt.isLocked)
                placed.Add((lt, sl));
        }
        if (placed.Count == 0) { EnableConfirm(); return; }

        // 2) ตรวจตามกฎทั้งหมด
        if (!MoveValidator.ValidateMove(placed, out var words, out string err))
        {
            RejectMove(placed, err);
            return;
        }

        // === FLASH wrong / duplicate words =====================
        List<MoveValidator.WordInfo> wrongWords = new();
        List<MoveValidator.WordInfo> dupWords   = new();

        foreach (var w in words)
        {
            bool valid = WordChecker.Instance.IsWordValid(w.word);
            bool dup   = boardWords.Contains(w.word);

            if      (!valid) wrongWords.Add(w);
            else if (dup)    dupWords  .Add(w);
        }

        if (wrongWords.Count > 0) StartCoroutine(BlinkWords(wrongWords, Color.red));
        if (dupWords  .Count > 0) StartCoroutine(BlinkWords(dupWords  , Color.yellow));

        // 3) เช็กดิก + คิดคะแนนทุกคำ
        bool hasValid = false;
        int  moveScore = 0;

        foreach (var w in words)
        {
            if (!WordChecker.Instance.IsWordValid(w.word)) continue;   // คำผิด → ข้าม

            hasValid = true;
            // ★ คิดคะแนนเฉพาะคำ “ใหม่” (ยังไม่อยู่บนกระดาน)
            if (!boardWords.Contains(w.word))
            {
                moveScore += ScoreManager.CalcWord(w.r0, w.c0, w.r1, w.c1);
                boardWords.Add(w.word);          // บันทึกว่าเกิดขึ้นแล้ว
            }
        }

        if (!hasValid)                   // ไม่มีคำถูกเลย → ไม่ทำอะไร
        {
            ShowMessage("✗ no valid word", Color.yellow);
            EnableConfirm();             // เปิดปุ่มให้ลองใหม่
            return;
        }

        // 4) ล็อก tile ใหม่ + update score
        score += moveScore;
        scoreText.text = $"Score : {score}";
        foreach (var (t, _) in placed) t.Lock();

        ShowMessage($"✓ +{moveScore}", Color.green);
        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();

        // เปิดปุ่มให้กดตาถัดไปหลังเติมเบนช์เสร็จ
        EnableConfirm();
    }

    // =========================================================
    void RejectMove(List<(LetterTile t, BoardSlot s)> tiles, string reason)
    {
        int sum = 0;
        foreach (var (t, _) in tiles)
        {
            sum += t.GetData().score;
            SpaceManager.Instance.RemoveTile(t);
        }
        int penalty = Mathf.CeilToInt(sum * 0.5f);
        score = Mathf.Max(0, score - penalty);
        scoreText.text = $"Score : {score}";
        ShowMessage($"✗ {reason}  -{penalty}", Color.red);

        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();
        EnableConfirm();
    }

    // =========================================================
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
                if (slot != null) slot.Flash(col);
                if (r == w.r1 && c == w.c1) break;
                r += dr; c += dc;
            }
        }
        yield return null;
    }

    // =========================================================
    void UpdateBagUI() =>
        bagCounterText.text =
            $"{TileBag.Instance.Remaining}/{TileBag.Instance.TotalInitial}";

    void ShowMessage(string msg, Color? col = null)
    {
        if (messageText == null) return;

        if (fadeCo != null) StopCoroutine(fadeCo);   // หยุด fade เดิม
        messageText.text  = msg;
        messageText.color = col ?? Color.white;

        if (msg != "")
            fadeCo = StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(2f);
        float t = 0;
        Color start = messageText.color;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            messageText.color = new Color(start.r, start.g, start.b, 1 - t);
            yield return null;
        }
        messageText.text = "";
    }

    public void EnableConfirm() => confirmBtn.interactable = true;
}
