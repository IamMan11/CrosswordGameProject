using UnityEngine;

public struct StageResult
{
    public int levelIndex;

    // metrics
    public int timeUsedSec;
    public int words;          // จำนวนคำในด่านนี้ (คุณเลือกใช้ Unique หรือ CheckedWordCount)
    public int turns;          // จำนวนครั้งที่กด Confirm
    public int tilesLeft;      // TileBag ที่เหลือ

    // score
    public int moveScore;      // คะแนนเกมเพลย์ที่สะสมมาระหว่างด่าน (TurnManager.Score)
    public int bonusScore;     // คะแนนโบนัสจากสูตรสรุป
    public int totalScore;     // moveScore + bonusScore

    // money
    public int baseCoins;
    public int bonusCoins;
    public int totalCoins;
}
