using System;
using UnityEngine;
using SQLite4Unity3d;
using System.IO;

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

        string path = Path.Combine(Application.persistentDataPath, "data.db");
        db = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
    }

    public void StartNewGame(string playerName)
    {
        PlayerRecord existing = null;
        foreach (var p in db.Table<PlayerRecord>())
        {
            if (p.player_name == playerName)
            {
                existing = p;
                break;
            }
        }

        if (existing == null)
        {
            db.Insert(new PlayerRecord { player_name = playerName });
            existing = db.Table<PlayerRecord>().Where(p => p.player_name == playerName).First();
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
        var game = db.Table<GameRecord>().Where(g => g.game_id == currentGameId).First();
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
