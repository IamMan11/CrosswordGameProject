using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System;

public class StageClearPanel : MonoBehaviour
{
    public static StageClearPanel Instance { get; private set; }
    public object nextButton { get; internal set; }

    public CanvasGroup root;
    public TMP_Text title, lines, scoreText, coinsText;
    public Button nextBtn;
    System.Action onNext;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Hide();
        if (nextBtn) nextBtn.onClick.AddListener(() => { Hide(); onNext?.Invoke(); });
    }

    public void Show(StageResult r, System.Action next)
    {
        onNext = next;
        if (title)     title.text = $"Stage {r.levelIndex} Clear!";
        if (lines)     lines.text =
            $"Time used : {r.timeUsedSec}s\n" +
            $"Words     : {r.words}\n" +
            $"Turns     : {r.turns}\n" +
            $"Tiles left: {r.tilesLeft}";
        if (scoreText) scoreText.text = $"Score: {r.totalScore}  (base {r.moveScore} + bonus {r.bonusScore})";
        if (coinsText) coinsText.text = $"Coins: {r.totalCoins}  (base {r.baseCoins} + bonus {r.bonusCoins})";
        root.alpha = 1; root.blocksRaycasts = true; root.interactable = true;
    }

    void Hide()
    {
        if (!root) return;
        root.alpha = 0; root.blocksRaycasts = false; root.interactable = false;
    }

    internal void SetResult(StageResult result)
    {
        throw new NotImplementedException();
    }
}
