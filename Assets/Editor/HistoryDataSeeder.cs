#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SQLite4Unity3d;

public class HistoryDataSeeder
{
    [MenuItem("Tools/Seed Demo History Data")]
    public static void SeedData()
    {
        string dbPath = System.IO.Path.Combine(Application.persistentDataPath, "data.db");
        var db = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);

        Debug.Log("📦 Seeding database at: " + dbPath);

        // ✅ สร้างตาราง ถ้ายังไม่มี (จะไม่ซ้ำซ้อนหากเคยมีแล้ว)
        db.CreateTable<PlayerRecord>();
        db.CreateTable<GameRecord>();
        db.CreateTable<PlayerAction>();

        // ✅ ตรวจว่าผู้เล่นซ้ำหรือยัง
        var existingA = db.Table<PlayerRecord>().Where(p => p.player_name == "PlayerA").FirstOrDefault();
        var existingB = db.Table<PlayerRecord>().Where(p => p.player_name == "PlayerB").FirstOrDefault();

        if (existingA == null)
        {
            db.Insert(new PlayerRecord { player_name = "PlayerA" });
            existingA = db.Table<PlayerRecord>().Where(p => p.player_name == "PlayerA").First();
        }
        if (existingB == null)
        {
            db.Insert(new PlayerRecord { player_name = "PlayerB" });
            existingB = db.Table<PlayerRecord>().Where(p => p.player_name == "PlayerB").First();
        }

        DateTime now = DateTime.Now;
        string nowStr = now.ToString("yyyy-MM-dd HH:mm:ss");

        // ✅ เพิ่มเกมใหม่
        var gameA = new GameRecord
        {
            player_id = existingA.player_id,
            start_time = nowStr,
            end_time = nowStr,
            total_score = 120,
            feedback = "เกมแรกของฉัน"
        };
        db.Insert(gameA);

        var gameB = new GameRecord
        {
            player_id = existingB.player_id,
            start_time = nowStr,
            end_time = nowStr,
            total_score = 85,
            feedback = "ลองระบบดู"
        };
        db.Insert(gameB);

        // ✅ เพิ่ม log
        var actions = new List<PlayerAction>
        {
            new PlayerAction
            {
                game_id = gameA.game_id,
                action_time = nowStr,
                action_type = "place_letter",
                details = "P-L-A-Y"
            },
            new PlayerAction
            {
                game_id = gameA.game_id,
                action_time = nowStr,
                action_type = "use_card",
                details = "Double Score"
            },
            new PlayerAction
            {
                game_id = gameB.game_id,
                action_time = nowStr,
                action_type = "place_letter",
                details = "W-O-R-D"
            }
        };

        foreach (var action in actions)
            db.Insert(action);

        Debug.Log("✅ เพิ่มข้อมูลจำลองสำเร็จแล้ว!");
    }
}
#endif
