using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("UI")]
    public Button   confirmBtn;
    public TMP_Text scoreText;
    public TMP_Text bagCounterText;
    
    public TMP_Text messageText;          // ★ NEW – ข้อความสถานะ

    int score = 0;

    // info ของการวางครั้งล่าสุด
    BoardSlot lastStart;
    Orient    lastOrient;
    List<(LetterTile tile, BoardSlot slot)> lastPlaced = new();

    void Awake()
    {
        Instance = this;
        confirmBtn.onClick.AddListener(OnConfirm);
        confirmBtn.interactable = false;
    }

    IEnumerator Start()
    {
        BenchManager.Instance.RefillEmptySlots();
        yield return null;
        UpdateBagUI();
        ShowMessage("");                   // clear
    }

    public void SetLastMoveInfo(BoardSlot start, Orient o,
                                List<(LetterTile,BoardSlot)> placed)
    {
        lastStart   = start;
        lastOrient  = o;
        lastPlaced  = placed;
        confirmBtn.interactable = true;
    }

    // =========================================================
    //                ยืนยัน / ตรวจคำ
    // =========================================================
    void OnConfirm()
    {
        confirmBtn.interactable = false;

        // --- รวบรวม tile “ใหม่” บนบอร์ด ---
        var placed = new List<(LetterTile t, BoardSlot s)>();
        foreach (var sl in BoardManager.Instance.grid)
            if (sl.HasLetterTile())
            {
                var lt = sl.transform.GetChild(1).GetComponent<LetterTile>();
                if (!lt.isLocked) placed.Add((lt,sl));
            }
        if (placed.Count==0) return;

        // --- ตรวจตามกฎทั้งหมด ---
        if (!MoveValidator.ValidateMove(placed, out var words, out string err))
        {
            RejectMove(placed, err);   // เด้ง + -50 %
            return;
        }

        // --- เช็กดิก & คิดคะแนนคำทั้งหมด ---
        int moveScore=0;
        foreach (var w in words)
        {
            if (!WordChecker.Instance.IsWordValid(w.word))
            { RejectMove(placed, $"'{w.word}' not found"); return; }

            moveScore += ScoreManager.CalcWord(w.r0,w.c0,w.r1,w.c1);    // :contentReference[oaicite:2]{index=2}&#8203;:contentReference[oaicite:3]{index=3}
        }

        // --- ผ่านทุกคำ → อัปเดตคะแนน & ล็อก tile ใหม่ ---
        score += moveScore;
        scoreText.text = $"Score : {score}";
        foreach(var (t,_) in placed) t.Lock();

        ShowMessage($"✓ +{moveScore}", Color.green);
        BenchManager.Instance.RefillEmptySlots();                          // :contentReference[oaicite:4]{index=4}&#8203;:contentReference[oaicite:5]{index=5}
        UpdateBagUI();
    }


    // =========================================================
    //                  Helper UI
    // =========================================================
    void UpdateBagUI()
    {
        bagCounterText.text = $"{TileBag.Instance.Remaining}/{TileBag.Instance.TotalInitial}";   // :contentReference[oaicite:10]{index=10}&#8203;:contentReference[oaicite:11]{index=11}
    }

    void ShowMessage(string msg, Color? col = null)
    {
        if (messageText == null) return;
        messageText.text  = msg;
        messageText.color = col ?? Color.white;
        // ดับเองใน 2 วิ
        StopAllCoroutines();
        if (msg != "") StartCoroutine(FadeOut());
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
    void RejectMove(List<(LetterTile t, BoardSlot s)> tiles, string reason)
    {
        int sum = 0;
        foreach (var (t,_) in tiles)
        {
            sum += t.GetData().score;                            // :contentReference[oaicite:8]{index=8}&#8203;:contentReference[oaicite:9]{index=9}
            SpaceManager.Instance.RemoveTile(t);                 // คืนเข้า Bench :contentReference[oaicite:10]{index=10}&#8203;:contentReference[oaicite:11]{index=11}
        }
        int penalty = Mathf.CeilToInt(sum * 0.5f);
        score = Mathf.Max(0, score - penalty);
        scoreText.text = $"Score : {score}";
        ShowMessage($"✗ {reason}  -{penalty}", Color.red);

        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();
    }
}
