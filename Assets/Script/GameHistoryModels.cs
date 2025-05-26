using SQLite4Unity3d;

public class PlayerRecord
{
    [PrimaryKey, AutoIncrement]
    public int player_id { get; set; }

    public string player_name { get; set; }
}

public class GameRecord
{
    [PrimaryKey, AutoIncrement]
    public int game_id { get; set; }

    public int player_id { get; set; }
    public string start_time { get; set; }
    public string end_time { get; set; }
    public int total_score { get; set; }
    public string feedback { get; set; }
}

public class PlayerAction
{
    [PrimaryKey, AutoIncrement]
    public int action_id { get; set; }

    public int game_id { get; set; }
    public string action_time { get; set; }
    public string action_type { get; set; }
    public string details { get; set; }
}
