using UnityEngine;

/// <summary>
/// LetterData
/// - ข้อมูลตัวอักษร 1 ตัว ที่ใช้สร้าง LetterTile
/// - letter: ตัวอักษร (เช่น "A", "B", "BLANK", "?" หรือ "_")
/// - sprite: รูปไอคอนตัวอักษร
/// - score : คะแนนฐานของตัวอักษร (Blank ให้ 0 เสมอที่ฝั่ง LetterTile)
/// - isSpecial: ธงสถานะตัวพิเศษ (ซิงก์กับ UI กรอบ/มาร์กพิเศษ)
/// </summary>
[System.Serializable]
public class LetterData
{
    [Tooltip("ตัวอักษร 1 ตัว เช่น A, B, C หรือ BLANK/?/_ สำหรับช่องเปล่า")]
    public string letter;
    public Sprite sprite;
    public int score;

    [HideInInspector] public bool isSpecial = false;
}
