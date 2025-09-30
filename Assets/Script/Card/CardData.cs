using UnityEngine;

/// <summary>
/// ข้อมูลการ์ด 1 ใบ (ScriptableObject)
/// - id: คีย์อ้างอิง
/// - displayName/description/icon: ข้อมูลโชว์ใน UI
/// - effectType/Mana/maxUsagePerTurn: ใช้เพื่อ ApplyEffect + ข้อจำกัดการใช้
/// - category/weight: ใช้สุ่ม drop rate
/// - requirePurchase/price: ใช้ฝั่ง Shop ถ้าต้องซื้อมาก่อนถึงสุ่มได้
/// </summary>
[CreateAssetMenu(menuName = "CrossClash/Card")]
public class CardData : ScriptableObject
{
    [Header("Identity & UI")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Effect")]
    public CardEffectType effectType;
    [Tooltip("ต้นทุน Mana ของการ์ด (ใช้เป็นพารามิเตอร์ทั่วไป)")]
    public int Mana = 0;
    [Tooltip("จำนวนครั้งสูงสุดที่ใช้การ์ดใบนี้ได้ในหนึ่งเทิร์น")]
    public int maxUsagePerTurn = 1;

    [Header("Drop Rates / Category")]
    public CardCategory category;
    [Tooltip("น้ำหนักของการ์ดในประเภท (ยิ่งมาก ยิ่งมีโอกาสถูกสุ่มเจอ)")]
    public int weight = 1;

    [Header("Shop")]
    [Tooltip("ต้องซื้อก่อนจึงจะเข้าสู่ pool สุ่ม/ใช้งาน")]
    public bool requirePurchase = false;
    [Tooltip("ราคาเหรียญใน Shop")]
    public int price = 0;
}

/// <summary>ประเภทของการ์ด (ใช้กำหนดสัดส่วนก่อนสุ่มใบจริง)</summary>
public enum CardCategory
{
    Buff,
    Dispell,
    Neutral,
    Wildcard,
    FusionCard
}

/// <summary>ชนิดของเอฟเฟกต์การ์ด (อย่าเปลี่ยนลำดับที่บันทึกไว้ใน asset เดิม)</summary>
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
