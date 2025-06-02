using UnityEngine;

[CreateAssetMenu(menuName = "CrossClash/Card")]
public class CardData : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    public CardEffectType effectType;
    public int Mana;              // ใช้เป็นพารามิเตอร์ generic
    [Tooltip("จำนวนครั้งสูงสุดที่ใช้การ์ดใบนี้ได้ในหนึ่งเทิร์น")]
    public int maxUsagePerTurn = 1;
}

public enum CardEffectType
{
    ExtraDraw,          // เติม Bench x ตัว
    DoubleNextScore,    // คูณคะแนนคำถัดไป
    FillBenchAll,       // เติม Bench ว่างทั้งหมด  (#6)
    BonusCardChoice,
    LetterQuadSurge,
    WordQuadSurge,
    LetterHexSurge,
    WordHexSurge,
    EchoBurst,
    TwinDraw,
    QuadSupply,
    BenchBlitz,
    DoubleRecast,
    FullRerack,
    GlyphSpark,
    TwinSparks,
    FreePass,
    MinorInfusion,
    MajorInfusion,
    ManaOverflow,
    WildBloom,
    ChaosBloom,
    TargetedFlux,
    CleanSlate
    
}