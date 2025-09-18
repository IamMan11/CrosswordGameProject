using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScoreManager
/// - คำนวณคะแนนคำ (DL/TL/DW/TW + overrides + GlobalLetterMultiplier)
/// - มี override ครั้งเดียว (ใช้แล้วเคลียร์) สำหรับ DL/TL และ DW/TW
/// - เพิ่ม ZeroScore helpers: ClearZeroScoreTiles / MarkZeroScoreTiles / IsZeroScoreTile
/// - แก้โค้ดซ้ำซ้อน/unreachable ใน CalcWord()
/// </summary>
public static class ScoreManager
{
    /* ===================== Overrides (ครั้งเดียว) ===================== */

    private static int doubleLetterOverride = 0; // ใช้แทน DL/TL ถ้าตั้ง >0
    private static int doubleWordOverride   = 0; // ใช้แทน DW/TW ถ้าตั้ง >0

    public static void SetDoubleLetterOverride(int multiplier)
        => doubleLetterOverride = Mathf.Max(multiplier, 0);

    public static void SetDoubleWordOverride(int multiplier)
        => doubleWordOverride = Mathf.Max(multiplier, 0);

    public static int GetLetterOverride() => doubleLetterOverride;
    public static int GetWordOverride()   => doubleWordOverride;

    /* ===================== Global Letter Multiplier (ชั่วคราว) ===================== */

    private static int   globalLetterMultiplier        = 1;
    private static float globalLetterMultiplierEndTime = 0f;

    public static void ActivateGlobalLetterMultiplier(int multiplier, float duration)
    {
        globalLetterMultiplier        = Mathf.Max(multiplier, 1);
        globalLetterMultiplierEndTime = Time.time + Mathf.Max(0f, duration);
    }

    static bool IsGlobalLetterMulActive() => Time.time < globalLetterMultiplierEndTime;

    /* ===================== Zero-score (ใช้กับ Bench Issue) ===================== */

    private static readonly HashSet<int> zeroScoreTileIds = new HashSet<int>();

    public static void ClearZeroScoreTiles() => zeroScoreTileIds.Clear();

    public static void MarkZeroScoreTiles(IEnumerable<LetterTile> tiles)
    {
        if (tiles == null) return;
        foreach (var t in tiles)
            if (t) zeroScoreTileIds.Add(t.GetInstanceID());
    }

    public static bool IsZeroScoreTile(LetterTile t)
        => t && zeroScoreTileIds.Contains(t.GetInstanceID());

    /* ===================== คำนวณคะแนนคำ ===================== */

    public static int CalcWord(int r0, int c0, int r1, int c1)
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null)
        {
            Debug.LogError("[Score] BoardManager/grid ไม่พร้อม");
            return 0;
        }

        var g = bm.grid;
        int total  = 0;
        int wordMul = 1;

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

            var tile = slot.GetLetterTile();
            if (tile == null)
            {
                Debug.LogError($"[Score] ไม่มี LetterTile ที่ช่อง {r},{c}");
                return 0;
            }

            int letter = Mathf.Max(0, tile.GetData()?.score ?? 0);

            if (IsGlobalLetterMulActive())
                letter *= globalLetterMultiplier;

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

            if (r == r1 && c == c1) break;
            r += dr; c += dc;
        }

        // ใช้แล้วเคลียร์ override
        doubleLetterOverride = 0;
        doubleWordOverride   = 0;

        return total * Mathf.Max(1, wordMul);
    }

    /* ===================== Helper สำหรับ UI FX ===================== */

    public static int EffectiveLetterMulFor(SlotType slotType)
    {
        if (slotType == SlotType.DoubleLetter)
            return (doubleLetterOverride > 0 ? doubleLetterOverride : 2);
        if (slotType == SlotType.TripleLetter)
            return (doubleLetterOverride > 0 ? doubleLetterOverride : 3);
        return 1;
    }

    public static int EffectiveWordMulFor(SlotType slotType)
    {
        if (slotType == SlotType.DoubleWord)
            return (doubleWordOverride > 0 ? doubleWordOverride : 2);
        if (slotType == SlotType.TripleWord)
            return (doubleWordOverride > 0 ? doubleWordOverride : 3);
        return 1;
    }

    /* ===================== โบนัสพิเศษ ===================== */

    public static int sevenLetterBonus = 50;

    public static void SetSevenLetterBonus(int v) => sevenLetterBonus = Mathf.Max(0, v);
    public static int  GetSevenLetterBonus()      => sevenLetterBonus;
}