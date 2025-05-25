using UnityEngine;

[System.Serializable]
public class LetterData
{
    [Tooltip("ตัวอักษร 1 ตัว เช่น A, B, C หรือ BLANK")]
    public string letter;
    public Sprite sprite;
    public int score;
}
