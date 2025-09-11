using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScoreManager
/// - ฟังก์ชันคำนวณคะแนนคำ (รวมตัวคูณช่อง DL/TL/DW/TW และตัวคูณพิเศษจากการ์ด)
/// - ตัวคูณชั่วคราวแบบ override:
///     • ตัวอักษร (Double/Triple Letter)   : ใช้กับช่อง DL/TL
///     • ตัวคำ (Double/Triple Word)        : ใช้กับช่อง DW/TW
/// - Global Letter Multiplier (เช่น “ตัวอักษรทั้งหมด x2” ชั่วคราวเป็นเวลาหนึ่ง)
/// - โบนัส 7 ตัวอักษร (กำหนดได้)
///
/// หมายเหตุ:
/// - โอเวอร์ไรด์ (doubleLetterOverride/doubleWordOverride) จะถูก “ใช้แล้วเคลียร์” ทุกครั้งที่ CalcWord() จบ
/// - ยังคงดีไซน์ตัวคูณคำแบบ “บวกกัน” สำหรับ UI โชว์ (ที่ TurnManager ใช้) และ “คูณจริง” ตอนสรุปใน CalcWord()
/// </summary>
public static class ScoreManager
{
    /* ===================== Overrides (ครั้งเดียว) ===================== */

    // ใช้แทนค่าตัวคูณของช่อง DL/TL (เช่น การ์ดกำหนดให้ DL= x4 ในครั้งถัดไป)
    private static int doubleLetterOverride = 0;

    // ใช้แทนค่าตัวคูณของช่อง DW/TW (เช่น การ์ดกำหนดให้ DW= x5 ในครั้งถัดไป)
    private static int doubleWordOverride = 0;

    /// <summary>ตั้งค่าตัวคูณแทน DL/TL (ค่าติดลบรึศูนย์จะถูกแก้เป็น 0 = ไม่แทน)</summary>
    public static void SetDoubleLetterOverride(int multiplier)
        => doubleLetterOverride = Mathf.Max(multiplier, 0);

    /// <summary>ตั้งค่าตัวคูณแทน DW/TW (ค่าติดลบรึศูนย์จะถูกแก้เป็น 0 = ไม่แทน)</summary>
    public static void SetDoubleWordOverride(int multiplier)
        => doubleWordOverride = Mathf.Max(multiplier, 0); // คงพฤติกรรมเดียวกับ DL

    public static int GetLetterOverride() => doubleLetterOverride;
    public static int GetWordOverride()   => doubleWordOverride;

    /* ===================== Global Letter Multiplier (ชั่วคราว) ===================== */

    private static int   globalLetterMultiplier       = 1;
    private static float globalLetterMultiplierEndTime = 0f;

    /// <summary>
    /// เปิดเอฟเฟกต์ “ตัวอักษรทั้งหมดคูณ multiplier” เป็นเวลา duration วินาที
    /// ตัวอย่าง: multiplier=2, duration=10f → ทุกตัวอักษร x2 เป็นเวลา 10 วินาที
    /// </summary>
    public static void ActivateGlobalLetterMultiplier(int multiplier, float duration)
    {
        globalLetterMultiplier       = Mathf.Max(multiplier, 1);
        globalLetterMultiplierEndTime = Time.time + Mathf.Max(0f, duration);
    }

    /// <summary>ยังอยู่ในช่วง Global Letter Multiplier อยู่หรือไม่</summary>
    static bool IsGlobalLetterMulActive() => Time.time < globalLetterMultiplierEndTime;

    /* ===================== คำนวณคะแนนคำ ===================== */

    /// <summary>
    /// คำนวณคะแนนคำบนบอร์ดจากตำแหน่งเริ่ม (r0,c0) ถึงตำแหน่งจบ (r1,c1)
    /// รวมตัวคูณช่อง DL/TL/DW/TW, overrides และ Global Letter Multiplier
    /// </summary>
    public static int CalcWord(int r0, int c0, int r1, int c1)
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null)
        {
            Debug.LogError("[Score] BoardManager/grid ไม่พร้อม");
            return 0;
        }

        var g = bm.grid;

        int total  = 0;   // ผลรวมคะแนนตัวอักษร (หลัง DL/TL และ Global letter)
        int wordMul = 1;  // ตัวคูณคำรวม (จาก DW/TW + override)

        // ทิศทางการเดิน (รองรับทั้งแนวนอนและแนวตั้ง)
        int dr = (r1 == r0) ? 0 : (r1 > r0 ? 1 : -1);
        int dc = (c1 == c0) ? 0 : (c1 > c0 ? 1 : -1);

        int r = r0, c = c0;
        while (true)
        {
            var slot = g[r, c];
            if (slot == null)
            {
                Debug.LogError($"[Score] Slot ว่างที่ {r},{c}");
                return 0;
            }

            // ใช้ helper ปลอดภัย ไม่ยึดลำดับลูกใน Hierarchy
            var tile = slot.GetLetterTile();
            if (tile == null)
            {
                Debug.LogError($"[Score] ไม่มี LetterTile ที่ช่อง {r},{c}");
                return 0;
            }

            // คะแนนฐานของตัวอักษร
            int letter = Mathf.Max(0, tile.GetData()?.score ?? 0);

            // Global letter multiplier
            if (IsGlobalLetterMulActive())
                letter *= globalLetterMultiplier;

            // ตัวคูณจากประเภทช่อง (ใช้ override ถ้ามี)
            switch (slot.type)
            {
                case SlotType.DoubleLetter:
                    letter *= (doubleLetterOverride > 0 ? doubleLetterOverride : 2);
                    break;

                case SlotType.TripleLetter:
                    letter *= (doubleLetterOverride > 0 ? doubleLetterOverride : 3);
                    break;

                case SlotType.DoubleWord:
                    wordMul *= (doubleWordOverride > 0 ? doubleWordOverride : 2);
                    break;

                case SlotType.TripleWord:
                    wordMul *= (doubleWordOverride > 0 ? doubleWordOverride : 3);
                    break;
            }


            total += letter;

            // ถึงตัวสุดท้ายแล้ว → จบลูป
            if (r == r1 && c == c1) break;

            r += dr;
            c += dc;
        }

        // ใช้แล้วเคลียร์ override (ดีไซน์เดิม)
        doubleLetterOverride = 0;
        doubleWordOverride   = 0;

        return total * Mathf.Max(1, wordMul);
        doubleWordOverride   = 0;

        return total * Mathf.Max(1, wordMul);
    }

    /* ===================== ตัวช่วยให้ TurnManager แสดงผล FX ===================== */

    /// <summary>คืนค่าตัวคูณตัวอักษรที่ “มีผลจริง” สำหรับช่องชนิดนั้น (ใช้โชว์ FX)</summary>
    public static int EffectiveLetterMulFor(SlotType slotType)
    {
        if (slotType == SlotType.DoubleLetter)
            return (doubleLetterOverride > 0 ? doubleLetterOverride : 2);

        if (slotType == SlotType.TripleLetter)
            return (doubleLetterOverride > 0 ? doubleLetterOverride : 3);

        return 1;
    }

    /// <summary>คืนค่าตัวคูณคำที่ “มีผลจริง” สำหรับช่องชนิดนั้น (ใช้โชว์ FX)</summary>
    public static int EffectiveWordMulFor(SlotType slotType)
    {
        if (slotType == SlotType.DoubleWord)
            return (doubleWordOverride > 0 ? doubleWordOverride : 2);

        if (slotType == SlotType.TripleWord)
            return (doubleWordOverride > 0 ? doubleWordOverride : 3);

        return 1;
    }

    /* ===================== โบนัสพิเศษ ===================== */

    // แต้มโบนัสเมื่อใช้ตัวอักษรครบ 7 ตัว (ปรับได้)
    public static int sevenLetterBonus = 50;

    public static void SetSevenLetterBonus(int v) => sevenLetterBonus = Mathf.Max(0, v);
    public static int  GetSevenLetterBonus()      => sevenLetterBonus;
}
