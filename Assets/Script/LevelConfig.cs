using UnityEngine;

[System.Serializable]
public class LevelConfig
{
    public int    levelIndex;           // ด่านที่ x
    public int    requiredScore;        // คะแนนขั้นต่ำ
    public int    requiredWords;        // จำนวนคำที่ต้องวางถูก
    public float  timeLimit;            // เวลาจำกัด (0 = ไม่ใช้)
    
    [Header("Auto-Remove Settings")]
    public bool   enableAutoRemove;     // เปิดระบบลบอักษรอัตโนมัติหรือไม่
    public float  autoRemoveInterval;   // ถ้าเปิด จะลบทุกกี่วินาที
}