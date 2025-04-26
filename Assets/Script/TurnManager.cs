// TurnManager.cs
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
    public TMP_Text scoreText;
    public TMP_Text bagCounterText;

    // runtime game state
    private int score = 0;

    // tracking last move
    private BoardSlot lastStart;
    private Orient    lastOrient;
    private List<(LetterTile tile, BoardSlot slot)> lastPlacedTiles = new List<(LetterTile,BoardSlot)>();

    // history of confirmed words to prevent double‐count
    private struct WordInfo { public BoardSlot start; public Orient orient; public int length; }
    private List<WordInfo> confirmedWords = new List<WordInfo>();

    void Awake()
    {
        Instance = this;
        if (confirmBtn == null || scoreText == null || bagCounterText == null)
        {
            Debug.LogError("[TurnManager] Assign UI references in Inspector!");
            enabled = false;
            return;
        }
        confirmBtn.interactable = false;
    }

    private IEnumerator Start()
    {
        // initial fill bench
        BenchManager.Instance.RefillEmptySlots();
        yield return null;
        UpdateBagUI();
        confirmBtn.onClick.AddListener(OnConfirm);
    }

    /// <summary>
    /// เรียกเมื่อ PlacementManager วางตัวอักษรเสร็จ
    /// </summary>
    public void OnTilesPlaced(BoardSlot start, Orient orient,
                                List<(LetterTile, BoardSlot)> placed)
    {
        lastStart = start;
        lastOrient = orient;
        lastPlacedTiles = new List<(LetterTile, BoardSlot)>(placed);
        confirmBtn.interactable = true;
    }

    private void OnConfirm()
    {
        confirmBtn.interactable = false;

        // ถ้าไม่มีการวางใหม่ในรอบนี้ ให้ข้าม
        if (lastPlacedTiles.Count == 0) return;

        // ตรวจคำตามจุดเริ่มและทิศ
        if (BoardAnalyzer.GetWord(lastStart, lastOrient,
                                  out string word,
                                  out int r0, out int c0,
                                  out int r1, out int c1)
            && word.Length >= 2
            && !HasBeenConfirmed(lastStart, lastOrient, word.Length))
        {
            // คำถูกและยังไม่เคยยืนยัน
            int wordScore = ScoreManager.CalcWord(r0, c0, r1, c1);
            score += wordScore;
            scoreText.text = score.ToString();

            // ล็อกตัวอักษรในคำ
            foreach (var slot in GetSlotsInWord(r0, c0, r1, c1))
                slot.GetLetterTile().Lock();

            // บันทึกประวัติ เพื่อไม่ให้คิดซ้ำ
            confirmedWords.Add(new WordInfo { start = lastStart,
                                              orient = lastOrient,
                                              length = word.Length });

            // เติม tile คืนมือ 1 ตัว
            BenchManager.Instance.RefillOneSlot();
        }
        else
        {
            // คำผิด หรือเคยยืนยันแล้ว ให้คืนแค่ตัวที่วางในรอบนี้
            foreach (var entry in lastPlacedTiles)
            {
                entry.slot.RemoveLetter();
                BenchManager.Instance.ReturnTile(entry.tile);
            }
        }

        lastPlacedTiles.Clear();
        UpdateBagUI();
    }

    private bool HasBeenConfirmed(BoardSlot start, Orient orient, int length)
    {
        foreach (var info in confirmedWords)
            if (info.start == start && info.orient == orient && info.length == length)
                return true;
        return false;
    }

    /// <summary>คืนรายการช่องของคำ จากพิกัด</summary>
    private List<BoardSlot> GetSlotsInWord(int r0, int c0, int r1, int c1)
    {
        List<BoardSlot> list = new List<BoardSlot>();
        int dr = r1 > r0 ? 1 : (r1 < r0 ? -1 : 0);
        int dc = c1 > c0 ? 1 : (c1 < c0 ? -1 : 0);
        int steps = Mathf.Max(Mathf.Abs(r1 - r0), Mathf.Abs(c1 - c0));
        for (int i = 0; i <= steps; i++)
        {
            int rr = r0 + dr * i;
            int cc = c0 + dc * i;
            var slot = BoardManager.Instance.GetSlot(rr, cc);
            if (slot != null) list.Add(slot);
        }
        return list;
    }

    private void UpdateBagUI()
    {
        bagCounterText.text = $"{TileBag.Instance.Remaining}/{TileBag.Instance.TotalInitial}";
    }
}
