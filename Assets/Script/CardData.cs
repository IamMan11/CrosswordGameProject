using UnityEngine;

[CreateAssetMenu(menuName="CrossClash/Card")]
public class CardData : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    public CardEffectType effectType;
    public int value;              // ใช้เป็นพารามิเตอร์ generic
}

public enum CardEffectType
{
    ExtraDraw,          // เติม Bench x ตัว
    DoubleNextScore,    // คูณคะแนนคำถัดไป
    SwapBench,          // สลับตัวใน Bench สองตำแหน่ง
    DestroyOpponent,    // ลบตัวอักษรคู่แข่ง 1 ตัว ฯลฯ
}