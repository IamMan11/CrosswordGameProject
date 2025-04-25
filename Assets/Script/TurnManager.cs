using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("UI")]
    public Button confirmBtn;
    public Button challengeBtn;
    public TMP_Text[] scoreTexts;
    public TMP_Text   bagCounterText;

    // runtime
    private int currentIdx = 0;
    private int[] scores = { 0, 0 };

    // last-turn info
    private BoardSlot lastStart;
    private Orient    lastOrient;
    private string    lastWord = "";
    private int       lastScore = 0;

    // ถูกเรียกจาก PlacementManager
    public void SetLastMoveInfo(BoardSlot start, Orient o, List<(LetterTile,BoardSlot)> _)
    {
        lastStart  = start;
        lastOrient = o;
    }

    void Awake()
    {
        Instance = this;
        if (confirmBtn == null || challengeBtn == null || bagCounterText == null)
        {
            Debug.LogError("[TurnManager] Please assign Confirm/Challenge/Button/BagCounter in Inspector!");
            enabled = false;
            return;
        }
    }

    private IEnumerator Start()
    {
        // เติม Bench + รอให้เสร็จ
        BenchManager.Instance.RefillEmptySlots();
        yield return null;
        // แสดงตัวเลขถุงตอนเริ่ม
        UpdateBagUI();

        // ผูก listener
        confirmBtn  .onClick.AddListener(OnConfirm);
        challengeBtn.onClick.AddListener(OnChallenge);
    }

    void OnConfirm()
    {
        // ล็อกทุก Tile บนกระดาน
        foreach (var slot in BoardManager.Instance.grid)
            if (slot.HasLetterTile())
                slot.transform.GetChild(1).GetComponent<LetterTile>().Lock();

        // ตรวจคำหลัก
        if (BoardAnalyzer.GetWord(lastStart, lastOrient,
                                  out lastWord,
                                  out int r0, out int c0,
                                  out int r1, out int c1)
            && lastWord.Length >= 2)
        {
            lastScore = ScoreManager.CalcWord(r0, c0, r1, c1);
            scores[currentIdx] += lastScore;
        }
        else
        {
            lastScore = 0;
            lastWord = "";
        }

        // เติม Bench แล้วอัปเดต UI ถุง
        BenchManager.Instance.RefillEmptySlots();
        UpdateBagUI();

        // อัปเดตคะแนน + ปุ่ม Challenge + สลับคนเล่น
        UpdateScoreUI();
        challengeBtn.interactable = lastScore > 0;
        currentIdx = 1 - currentIdx;
    }

    void OnChallenge()
    {
        int challenger = currentIdx;
        int target     = 1 - currentIdx;

        bool valid = !string.IsNullOrEmpty(lastWord)
                     && WordChecker.Instance.IsValid(lastWord.ToLower());

        if (valid) scores[challenger] -= lastScore;
        else       scores[target]     -= lastScore;

        UpdateScoreUI();
        challengeBtn.interactable = false;
    }

    // —————— Helpers ——————

    // เรียกหลังเติมตัวทุกครั้ง
    private void UpdateBagUI()
    {
        bagCounterText.text =
            $"{TileBag.Instance.Remaining}/{TileBag.Instance.TotalInitial}";
    }

    private void UpdateScoreUI()
    {
        for (int i = 0; i < scores.Length && i < scoreTexts.Length; i++)
            scoreTexts[i].text = scores[i].ToString();
    }
}
