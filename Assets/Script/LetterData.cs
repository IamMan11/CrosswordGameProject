using UnityEngine;

[System.Serializable]
public class LetterData
{
    [Tooltip("ตัวอักษร 1 ตัว เช่น A, B, C หรือ BLANK")]
    public string letter;       // ใช้ string เพื่อใส่ใน Inspector ได้ง่าย
    public Sprite sprite;       // รูปภาพของตัวอักษร
    public int score;           // คะแนน
    [HideInInspector] public bool isSpecial = false;
}
