using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScoreManager (STATIC)
/// - ดูแลการคำนวณคะแนนคำ และตัวคูณแบบต่าง ๆ
/// - รองรับเอฟเฟกต์การ์ดที่ตั้งค่าตัวคูณอักษร/คำ "ในตานี้" (turn-scoped)
/// - รองรับโหมด "ครั้งเดียว" สำหรับเอฟเฟกต์เฉพาะกิจ
/// - มี Global Letter Multiplier แบบชั่วคราว (ตามเวลา)
/// - เพิ่มระบบ ZeroScoreTiles สำหรับไทล์ที่ต้องได้ 0 คะแนน
/// 
/// หมายเหตุการเชื่อมต่อ:
/// - BoardManager.Instance.grid : BoardSlot[,]
/// - BoardSlot : มี property `type` (SlotType) และ method `GetLetterTile()`
/// - LetterTile : มี `GetData()?.score`
/// 
/// API สำคัญที่ระบบอื่นเรียก:
///  - SetDoubleLetterOverride(int), SetDoubleWordOverride(int)          → ตั้งตัวคูณ DL/TL, DW/TW "ทั้งเทิร์น"
///  - SetDoubleLetterOverrideOnce(int), SetDoubleWordOverrideOnce(int)  → ตั้งตัวคูณแบบใช้ครั้งเดียว
///  - ClearTurnScopedOverrides()                                        → ให้ TurnManager เรียกเมื่อจบเทิร์น
///  - CalcWord(r0,c0,r1,c1)                                            → คำนวณคะแนนจริง (กิน override แบบครั้งเดียว)
///  - CalcWordPreview(r0,c0,r1,c1)                                     → คำนวณพรีวิว (ไม่กิน override)
///  - ActivateGlobalLetterMultiplier(int, float)                        → คูณคะแนนตัวอักษรทั้งหมดแบบชั่วคราว
/// </summary>
public static class ScoreManager
{
    // =====================================================================
    #region Turn-Scoped Overrides (DL/TL & DW/TW)

    private static int  s_doubleLetterOverride = 0;
    private static int  s_doubleWordOverride   = 0;

    // บอกว่า override ปัจจุบันเป็นแบบอยู่ถึงจบเทิร์นหรือไม่
    private static bool s_letterOverrideTurnScoped = true;
    private static bool s_wordOverrideTurnScoped   = true;

    /// <summary>ตั้ง DL/TL override (ค่าอยู่ถึงจบเทิร์นเป็นค่าเริ่มต้น)</summary>
    public static void SetDoubleLetterOverride(int multiplier)
    {
        s_doubleLetterOverride        = Mathf.Max(multiplier, 0);
        s_letterOverrideTurnScoped    = true;
    }

    /// <summary>ตั้ง DW/TW override (ค่าอยู่ถึงจบเทิร์นเป็นค่าเริ่มต้น)</summary>
    public static void SetDoubleWordOverride(int multiplier)
    {
        s_doubleWordOverride        = Mathf.Max(multiplier, 0);
        s_wordOverrideTurnScoped    = true;
    }

    /// <summary>ตั้ง DL/TL override แบบใช้ครั้งเดียวจริง ๆ (หายหลัง CalcWord ครั้งแรก)</summary>
    public static void SetDoubleLetterOverrideOnce(int multiplier)
    {
        s_doubleLetterOverride        = Mathf.Max(multiplier, 0);
        s_letterOverrideTurnScoped    = false;
    }

    /// <summary>ตั้ง DW/TW override แบบใช้ครั้งเดียวจริง ๆ (หายหลัง CalcWord ครั้งแรก)</summary>
    public static void SetDoubleWordOverrideOnce(int multiplier)
    {
        s_doubleWordOverride        = Mathf.Max(multiplier, 0);
        s_wordOverrideTurnScoped    = false;
    }

    public static int GetLetterOverride() => s_doubleLetterOverride;
    public static int GetWordOverride()   => s_doubleWordOverride;

    /// <summary>ล้าง override ที่เป็นแบบ "ทั้งเทิร์น" — ให้ TurnManager เรียกเมื่อจบเทิร์น</summary>
    public static void ClearTurnScopedOverrides()
    {
        if (s_letterOverrideTurnScoped) s_doubleLetterOverride = 0;
        if (s_wordOverrideTurnScoped)   s_doubleWordOverride   = 0;
        s_letterOverrideTurnScoped = false;
        s_wordOverrideTurnScoped   = false;
    }

    #endregion
    // =====================================================================

    // =====================================================================
    #region Global Letter Multiplier (ตามเวลา)

    private static int   s_globalLetterMul        = 1;
    private static float s_globalLetterMulEndTime = 0f;

    /// <summary>คูณคะแนนตัวอักษรทั้งหมดด้วย multiplier ชั่วคราวตาม duration (วินาที)</summary>
    public static void ActivateGlobalLetterMultiplier(int multiplier, float duration)
    {
        s_globalLetterMul        = Mathf.Max(multiplier, 1);
        s_globalLetterMulEndTime = Time.time + Mathf.Max(0f, duration);
    }

    private static bool IsGlobalLetterMulActive() => Time.time < s_globalLetterMulEndTime;

    #endregion
    // =====================================================================

    // =====================================================================
    #region Zero-score Tiles

    private static readonly HashSet<int> s_zeroScoreTileIds = new HashSet<int>();

    /// <summary>ล้างรายการไทล์ที่ต้องให้ 0 คะแนน</summary>
    public static void ClearZeroScoreTiles() => s_zeroScoreTileIds.Clear();

    /// <summary>เพิ่มไทล์ที่ต้องให้ 0 คะแนน (ใช้ instanceID ของ LetterTile)</summary>
    public static void MarkZeroScoreTiles(IEnumerable<LetterTile> tiles)
    {
        if (tiles == null) return;
        foreach (var t in tiles) if (t) s_zeroScoreTileIds.Add(t.GetInstanceID());
    }

    public static bool IsZeroScoreTile(LetterTile t) => t && s_zeroScoreTileIds.Contains(t.GetInstanceID());

    #endregion
    // =====================================================================

    // =====================================================================
    #region คำนวณคะแนนคำ

    /// <summary>คำนวณคะแนน "พรีวิว" — ไม่กิน override แบบครั้งเดียว (ใช้กับ UI hover)</summary>
    public static int CalcWordPreview(int r0, int c0, int r1, int c1)
        => CalcWordInternal(r0, c0, r1, c1, consumeOverrides: false);

    /// <summary>คำนวณคะแนนจริง — กิน override แบบครั้งเดียว (แต่แบบทั้งเทิร์นจะยังอยู่)</summary>
    public static int CalcWord(int r0, int c0, int r1, int c1)
        => CalcWordInternal(r0, c0, r1, c1, consumeOverrides: true);

    private static int CalcWordInternal(int r0, int c0, int r1, int c1, bool consumeOverrides)
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null)
        {
            Debug.LogError("[Score] BoardManager/grid ไม่พร้อม");
            return 0;
        }

        var g = bm.grid; // BoardSlot[,]

        // ตรวจทิศทาง (รองรับคำแนวเดียว — แนวนอนหรือแนวตั้ง)
        int dr = (r1 == r0) ? 0 : (r1 > r0 ? 1 : -1);
        int dc = (c1 == c0) ? 0 : (c1 > c0 ? 1 : -1);
        if (dr != 0 && dc != 0)
        {
            Debug.LogWarning("[Score] CalcWordInternal ถูกเรียกด้วยช่วงที่ไม่เป็นเส้นตรง");
        }

        int total  = 0;
        int wordMul = 1;

        int r = r0, c = c0;
        while (true)
        {
            var slot = g[r, c];
            if (slot == null)
            {
                Debug.LogError($"[Score] Slot ว่างที่ {r},{c}");
                return 0;
            }

            var tile = slot.GetLetterTile();
            if (tile == null)
            {
                Debug.LogError($"[Score] ไม่มี LetterTile ที่ช่อง {r},{c}");
                return 0;
            }

            int letter = Mathf.Max(0, tile.GetData()?.score ?? 0);

            // ไทล์ 0 แต้ม (ถ้าถูกมาร์กไว้)
            if (IsZeroScoreTile(tile)) letter = 0;

            // Global letter multiplier
            if (IsGlobalLetterMulActive()) letter *= s_globalLetterMul;

            // ตัวคูณจากประเภทช่อง
            switch (slot.type)
            {
                case SlotType.DoubleLetter:
                    letter *= (s_doubleLetterOverride > 0 ? s_doubleLetterOverride : 2);
                    break;
                case SlotType.TripleLetter:
                    letter *= (s_doubleLetterOverride > 0 ? s_doubleLetterOverride : 3);
                    break;
                case SlotType.DoubleWord:
                    wordMul *= (s_doubleWordOverride > 0 ? s_doubleWordOverride : 2);
                    break;
                case SlotType.TripleWord:
                    wordMul *= (s_doubleWordOverride > 0 ? s_doubleWordOverride : 3);
                    break;
            }

            total += letter;

            if (r == r1 && c == c1) break;
            r += dr; c += dc;
        }

        // ล้างเฉพาะ override แบบ "ครั้งเดียว" หลังใช้งาน
        if (consumeOverrides)
        {
            if (!s_letterOverrideTurnScoped) s_doubleLetterOverride = 0;
            if (!s_wordOverrideTurnScoped)   s_doubleWordOverride   = 0;
        }

        return total * Mathf.Max(1, wordMul);
    }

    #endregion
    // =====================================================================

    // =====================================================================
    #region Helper สำหรับ UI/เอฟเฟกต์

    public static int EffectiveLetterMulFor(SlotType slotType)
    {
        if (slotType == SlotType.DoubleLetter)
            return (s_doubleLetterOverride > 0 ? s_doubleLetterOverride : 2);
        if (slotType == SlotType.TripleLetter)
            return (s_doubleLetterOverride > 0 ? s_doubleLetterOverride : 3);
        return 1;
    }

    public static int EffectiveWordMulFor(SlotType slotType)
    {
        if (slotType == SlotType.DoubleWord)
            return (s_doubleWordOverride > 0 ? s_doubleWordOverride : 2);
        if (slotType == SlotType.TripleWord)
            return (s_doubleWordOverride > 0 ? s_doubleWordOverride : 3);
        return 1;
    }

    #endregion
    // =====================================================================

    // =====================================================================
    #region โบนัสพิเศษ

    public static int sevenLetterBonus = 50; // เผื่อใช้กรณีพิเศษ (เช่น ลงครบ 7 ตัว)
    public static void SetSevenLetterBonus(int v) => sevenLetterBonus = Mathf.Max(0, v);
    public static int  GetSevenLetterBonus()      => sevenLetterBonus;

    #endregion
    // =====================================================================
}
