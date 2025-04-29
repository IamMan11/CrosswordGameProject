using System.Collections;
using System.Collections.Generic;
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

        // 1) เอาสตริงเต็มคำจาก BoardAnalyzer
        bool closed = BoardAnalyzer.GetWord(
            lastStart, lastOrient,
            out string word, out int r0, out int c0, out int r1, out int c1);   // :contentReference[oaicite:0]{index=0}&#8203;:contentReference[oaicite:1]{index=1}

        // 2) เช็กคำเพียงครั้งเดียว
        bool valid = closed && WordChecker.Instance.IsWordValid(word);

        if (valid)
        {
            int gained = ScoreManager.CalcWord(r0, c0, r1, c1);                 // :contentReference[oaicite:2]{index=2}&#8203;:contentReference[oaicite:3]{index=3}
            score += gained;
            scoreText.text = $"Score : {score}";

            foreach (var (tile, _) in lastPlaced) tile.Lock();
            ShowMessage($"✓ {word.ToUpper()}  +{gained}", Color.green);
        }
        else
        {
            int letterSum = 0;
            foreach (var (tile, _) in lastPlaced)
            {
                letterSum += tile.GetData().score;                               // :contentReference[oaicite:4]{index=4}&#8203;:contentReference[oaicite:5]{index=5}
                SpaceManager.Instance.RemoveTile(tile);                          // :contentReference[oaicite:6]{index=6}&#8203;:contentReference[oaicite:7]{index=7}
            }
            lastPlaced.Clear();

            int penalty = Mathf.CeilToInt(letterSum * 0.5f);
            score = Mathf.Max(0, score - penalty);
            scoreText.text = $"Score : {score}";

            ShowMessage($"✗ {word.ToUpper()}  -{penalty}", Color.red);
        }

        BenchManager.Instance.RefillEmptySlots();                                // :contentReference[oaicite:8]{index=8}&#8203;:contentReference[oaicite:9]{index=9}
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
}
