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

    [Header("การจัดประเภทและน้ำหนัก (Drop Rates)")]
    public CardCategory category;      // ยกตัวอย่าง: Buff, Dispell, Wildcard, ฯลฯ
    [Tooltip("น้ำหนักของการ์ดนี้ภายในประเภท (ยิ่งมาก ยิ่งมีโอกาสถูกสุ่มเจอในประเภท)")]
    public int weight = 1;
    [Header("Shop")]
    public bool  requirePurchase = false;   // ✔ true = ต้องซื้อก่อน
    public int   price           = 0;       // ✔ ราคาเหรียญ
}
public enum CardCategory
{
    Buff,        // ออกง่ายสุด
    Dispell,     // ออกง่าย
    Neutral,     // ออกปานกลาง
    Wildcard,
    FusionCard    // ออกยากสุด
}

public enum CardEffectType
{
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
    CleanSlate,
    GlobalEcho,
    MasterDraft,
    PandemoniumField,
    WordForge,
    OmniSpark,
    CardRefresh,
    InfiniteTiles,
    PackRenewal,
    ManaInfinity
}