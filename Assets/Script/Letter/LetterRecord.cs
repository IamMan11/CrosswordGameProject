using SQLite4Unity3d;

public class LetterRecord
{
    [PrimaryKey, AutoIncrement]
    public int letter_id { get; set; }

    public string letter_char { get; set; }  // เช่น "A", "B"
    public int score { get; set; }
}
