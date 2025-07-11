using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using SQLite4Unity3d;

public class UIHistoryViewer : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject rootPanel;
    public TMP_InputField scoreFilterInput;

    public Transform historyContainer;
    public GameObject historyEntryPrefab;
    public GameObject logPanel;
    public Transform logContainer;
    public GameObject logEntryPrefab;

    [Header("Stats Panel")]
    public TMP_Text statsText;
    public LineRenderer lineGraph;
    public RectTransform graphContainer;
    public Transform topScoreTable;
    public GameObject topScoreRowPrefab;

    [Header("Graph Labels")]
    public GameObject graphLabelPrefab;
    public Transform graphLabelContainer;

    [Header("Graph Point Prefab")]
    public GameObject graphPointPrefab;
    public Transform graphPointContainer;

    private SQLiteConnection db;
    private const string currentPlayerName = "Player1";

    IEnumerator Start()
    {
        yield return null;

        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "data.db");
        Debug.Log("\ud83d\udce6 DB path: " + path);
        db = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);

        var player = db.Table<PlayerRecord>().FirstOrDefault(p => p.player_name == currentPlayerName);
        if (player == null)
        {
            db.Insert(new PlayerRecord { player_name = currentPlayerName });
            Debug.Log("\ud83d\udfe2 Added Player1 to database");
        }

        rootPanel.SetActive(true);
        logPanel.SetActive(false);

        Material lineMat = new Material(Shader.Find("Sprites/Default"));
        lineMat.color = Color.cyan;
        lineGraph.material = lineMat;
        lineGraph.startWidth = 5f;
        lineGraph.endWidth = 5f;
        lineGraph.useWorldSpace = false;
        lineGraph.sortingLayerName = "UI";
        lineGraph.sortingOrder = 0;
        lineGraph.startColor = Color.cyan;
        lineGraph.endColor = Color.cyan;
        lineGraph.positionCount = 0;

        OnClickShowHistory();
    }

    public void OnClickShowHistory()
    {
        int minScore = 0;
        if (scoreFilterInput != null)
            int.TryParse(scoreFilterInput.text, out minScore);

        ShowPlayerHistory(currentPlayerName, minScore);
        ShowPlayerStats(currentPlayerName);
        DrawGraph(currentPlayerName);
        ShowTop5(currentPlayerName);
    }

    void ShowPlayerHistory(string playerName, int minScore)
    {
        var player = db.Table<PlayerRecord>().FirstOrDefault(p => p.player_name == playerName);
        if (player == null) return;

        foreach (Transform child in historyContainer)
            Destroy(child.gameObject);

        var games = db.Table<GameRecord>()
            .Where(g => g.player_id == player.player_id && g.total_score >= minScore)
            .OrderByDescending(g => g.game_id);

        foreach (var game in games)
        {
            var go = Instantiate(historyEntryPrefab, historyContainer);
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = $"#{game.game_id} | {game.start_time} - {game.end_time}\nScore: {game.total_score}\n{game.feedback}";

            var btn = go.GetComponentInChildren<Button>();
            int gid = game.game_id;
            btn.onClick.AddListener(() => ShowGameLog(gid));
        }
    }

    void ShowGameLog(int gameId)
    {
        logPanel.SetActive(true);

        foreach (Transform child in logContainer)
            Destroy(child.gameObject);

        var logs = db.Table<PlayerAction>()
            .Where(a => a.game_id == gameId)
            .OrderBy(a => a.action_time)
            .ToList();

        foreach (var log in logs)
        {
            string prefix = log.action_type switch
            {
                "place_letter" => "[Place Letter]",
                "use_card" => "[Use Card]",
                "check_word" => "[Check Word]",
                _ => $"[{log.action_type}]"
            };

            var go = Instantiate(logEntryPrefab, logContainer);
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = $"{prefix} {log.action_time} → {log.details}";
        }
    }

    void ShowPlayerStats(string playerName)
    {
        var player = db.Table<PlayerRecord>().FirstOrDefault(p => p.player_name == playerName);
        if (player == null || statsText == null) return;

        var games = db.Table<GameRecord>().Where(g => g.player_id == player.player_id).ToList();
        int totalGames = games.Count;
        int totalScore = games.Sum(g => g.total_score);
        float avgScore = totalGames > 0 ? (float)totalScore / totalGames : 0f;

        var allActions = db.Table<PlayerAction>().ToList();
        var actions = allActions.Where(a => games.Any(g => g.game_id == a.game_id)).ToList();

        int lettersPlaced = actions.Count(a => a.action_type == "place_letter");
        int cardsUsed = actions.Count(a => a.action_type == "use_card");

        statsText.text =
            $"Player: {playerName}\n" +
            $"- Games Played: {totalGames}\n" +
            $"- Total Score: {totalScore}\n" +
            $"- Average Score: {avgScore:F1}\n" +
            $"- Letters Placed: {lettersPlaced}\n" +
            $"- Cards Used: {cardsUsed}";
    }

    void DrawGraph(string playerName)
    {
        if (lineGraph == null || graphContainer == null || graphLabelContainer == null || graphPointContainer == null) return;

        foreach (Transform child in graphLabelContainer)
            Destroy(child.gameObject);
        foreach (Transform child in graphPointContainer)
            Destroy(child.gameObject);

        var player = db.Table<PlayerRecord>().FirstOrDefault(p => p.player_name == playerName);
        if (player == null) return;

        var games = db.Table<GameRecord>()
            .Where(g => g.player_id == player.player_id)
            .OrderBy(g => g.start_time)
            .ToList();

        float width = graphContainer.rect.width;
        float height = graphContainer.rect.height;
        float xPadding = 40f;
        float yPadding = 30f;
        float usableWidth = width - xPadding * 2f;
        float usableHeight = height - yPadding * 2f;
        float leftX = -width / 2f + xPadding;
        float bottomY = -height / 2f + yPadding;

        if (games.Count == 0)
        {
            lineGraph.positionCount = 2;
            lineGraph.SetPosition(0, new Vector3(leftX, bottomY, 0));
            lineGraph.SetPosition(1, new Vector3(-leftX, bottomY, 0));
            return;
        }

        float maxScore = Mathf.Max(1, games.Max(g => g.total_score));
        lineGraph.positionCount = games.Count;

        for (int i = 0; i < games.Count; i++)
        {
            float t = i / (float)(games.Count - 1);
            float x = leftX + t * usableWidth;
            float y = bottomY + (games[i].total_score / maxScore) * usableHeight;
            Vector3 point = new Vector3(x, y, 0);
            lineGraph.SetPosition(i, point);

            if (graphPointPrefab != null)
            {
                var pointGO = Instantiate(graphPointPrefab, graphPointContainer);
                pointGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            }

            var label = Instantiate(graphLabelPrefab, graphLabelContainer);
            var rect = label.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, bottomY - 25f);
            rect.rotation = Quaternion.Euler(0, 0, 45f);

            var textComp = label.GetComponentInChildren<TMP_Text>();
            if (textComp != null)
            {
                textComp.fontSize = 18;
                textComp.color = Color.white;
                if (System.DateTime.TryParse(games[i].start_time, out System.DateTime parsedDate))
                    textComp.text = parsedDate.ToString("MM/dd");
                else
                    textComp.text = "??";
            }
        }
    }

    void ShowTop5(string playerName)
    {
        if (topScoreTable == null || topScoreRowPrefab == null) return;

        foreach (Transform child in topScoreTable)
            Destroy(child.gameObject);

        var player = db.Table<PlayerRecord>().FirstOrDefault(p => p.player_name == playerName);
        if (player == null) return;

        var topGames = db.Table<GameRecord>()
            .Where(g => g.player_id == player.player_id)
            .OrderByDescending(g => g.total_score)
            .Take(5);

        foreach (var game in topGames)
        {
            var go = Instantiate(topScoreRowPrefab, topScoreTable);
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = $"#{game.game_id} - {game.total_score} pts - {game.start_time}";
        }
    }

    public void ToggleVisible()
    {
        rootPanel.SetActive(!rootPanel.activeSelf);
        logPanel.SetActive(false);
    }
}
