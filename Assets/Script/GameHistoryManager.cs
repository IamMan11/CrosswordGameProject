using System;
using SQLite4Unity3d;
using UnityEngine;

public class GameHistoryManager : MonoBehaviour
{
    public static GameHistoryManager Instance { get; private set; }

    private SQLiteConnection db;
    private int currentGameId;
    private int currentPlayerId;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        string path = System.IO.Path.Combine(Application.persistentDataPath, "data.db");
        db = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
    }

    public void StartNewGame(string playerName)
    {
        // ตรวจสอบว่าผู้เล่นมีอยู่หรือยัง
        var existing = db.Table<PlayerRecord>().FirstOrDefault(p => p.player_name == playerName);
        if (existing == null)
        {
            db.Insert(new PlayerRecord { player_name = playerName });
            existing = db.Table<PlayerRecord>().First(p => p.player_name == playerName);
        }

        currentPlayerId = existing.player_id;

        var game = new GameRecord
        {
            player_id = currentPlayerId,
            start_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        db.Insert(game);
        currentGameId = game.game_id;

        Debug.Log($"🟢 เริ่มเกมใหม่: GameID={currentGameId}, Player={playerName}");
    }

    public void EndGame(int score, string feedback = "")
    {
        var game = db.Table<GameRecord>().First(g => g.game_id == currentGameId);
        game.end_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        game.total_score = score;
        game.feedback = feedback;

        db.Update(game);

        Debug.Log($"🏁 จบเกม: GameID={game.game_id}, Score={score}");
    }

    public void LogAction(string actionType, string details)
    {
        var action = new PlayerAction
        {
            game_id = currentGameId,
            action_type = actionType,
            details = details,
            action_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        db.Insert(action);
        Debug.Log($"📌 Action: {actionType} – {details}");
    }
}
