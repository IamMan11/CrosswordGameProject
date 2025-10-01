using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ShopSceneController : MonoBehaviour
{
    [Header("Optional UI")]
    public TMP_Text summaryText;   // โชว์สรุปที่เอามาจาก StageResultBus

    void Start()
    {
        if (summaryText != null && StageResultBus.LastResult.HasValue)
        {
            var r = StageResultBus.LastResult.Value;
            summaryText.text =
                $"Stage {r.levelIndex} Clear!\n" +
                $"Score: {r.totalScore} (+{r.bonusScore})\n" +
                $"Coins: {r.totalCoins} (+{r.bonusCoins})";
        }
    }

    public void OnClickContinue()
    {
        var gameScene = string.IsNullOrEmpty(StageResultBus.GameplaySceneName)
            ? "Game" : StageResultBus.GameplaySceneName;

        SceneManager.LoadScene(gameScene);
    }
}
