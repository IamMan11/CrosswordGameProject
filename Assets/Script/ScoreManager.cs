using System.Collections.Generic;
using UnityEngine;

public static class ScoreManager
{
    // ===== Overrides / Multipliers =====
    private static int doubleLetterOverride = 0;
    private static int doubleWordOverride = 0;

    // Global letter multiplier (e.g., x2 letters for N seconds)
    private static int   globalLetterMultiplier       = 1;
    private static float globalLetterMultiplierEndTime = 0f;

    // ===== NEW: zero-score letters (one-shot per scoring) =====
    // ใช้กับ Bench issue ของด่าน 2: ตัวอักษรที่ถูก mark ไว้ จะมีคะแนนตัวอักษร = 0 เฉพาะรอบคิดคะแนนครั้งนี้
    private static readonly HashSet<LetterTile> zeroScoreTiles = new HashSet<LetterTile>();

    public static void SetDoubleLetterOverride(int multiplier) => doubleLetterOverride = Mathf.Max(multiplier, 0);
    public static void SetDoubleWordOverride(int multiplier)  => doubleWordOverride  = Mathf.Max(multiplier, 0);

    public static void ActivateGlobalLetterMultiplier(int multiplier, float duration)
    {
        globalLetterMultiplier       = Mathf.Max(multiplier, 1);
        globalLetterMultiplierEndTime = Time.time + Mathf.Max(duration, 0f);
    }

    // ===== NEW: Bench issue helpers =====
    public static void MarkZeroScoreTiles(IEnumerable<LetterTile> tiles)
    {
        zeroScoreTiles.Clear();
        if (tiles == null) return;
        foreach (var t in tiles) if (t != null) zeroScoreTiles.Add(t);
    }
    public static void ClearZeroScoreTiles() => zeroScoreTiles.Clear();
    public static bool IsZeroScoreTile(LetterTile t) => t != null && zeroScoreTiles.Contains(t);

    public static int CalcWord(int r0, int c0, int r1, int c1)
    {
        int total  = 0;
        int wordMul = 1;
        var g = BoardManager.Instance.grid;

        int dr = r0 == r1 ? 0 : (r1 > r0 ? 1 : -1);
        int dc = c0 == c1 ? 0 : (c1 > c0 ? 1 : -1);

        int r = r0, c = c0;
        while (true)
        {
            var tile = g[r, c].GetLetterTile();
            if (tile == null)
            {
                Debug.LogError($"[Score] ไม่มี LetterTile ที่ช่อง {r},{c}");
                return 0;
            }

            int letter = Mathf.Max(0, tile.GetData().score);

            // NEW: zero-score จาก bench issue
            if (IsZeroScoreTile(tile)) letter = 0;

            // Global letter multiplier (time-bounded)
            if (Time.time < globalLetterMultiplierEndTime)
                letter *= globalLetterMultiplier;

            switch (g[r, c].type)
            {
                case SlotType.DoubleLetter:
                    letter *= doubleLetterOverride > 0 ? doubleLetterOverride : 2;
                    break;
                case SlotType.TripleLetter:
                    letter *= doubleLetterOverride > 0 ? doubleLetterOverride : 3;
                    break;
                case SlotType.DoubleWord:
                    wordMul *= doubleWordOverride > 0 ? doubleWordOverride : 2;
                    break;
                case SlotType.TripleWord:
                    wordMul *= doubleWordOverride > 0 ? doubleWordOverride : 3;
                    break;
            }

            total += letter;

            if (r == r1 && c == c1) break;
            r += dr; c += dc;
        }

        // one-shot overrides
        doubleLetterOverride = 0;
        doubleWordOverride   = 0;

        return total * Mathf.Max(1, wordMul);
    }

    public static int GetLetterOverride() => doubleLetterOverride;
    public static int GetWordOverride()   => doubleWordOverride;

    public static int EffectiveLetterMulFor(SlotType slotType)
    {
        if (slotType == SlotType.DoubleLetter)
            return doubleLetterOverride > 0 ? doubleLetterOverride : 2;
        if (slotType == SlotType.TripleLetter)
            return doubleLetterOverride > 0 ? doubleLetterOverride : 3;
        return 1;
    }

    public static int EffectiveWordMulFor(SlotType slotType)
    {
        if (slotType == SlotType.DoubleWord)
            return doubleWordOverride > 0 ? doubleWordOverride : 2;
        if (slotType == SlotType.TripleWord)
            return doubleWordOverride > 0 ? doubleWordOverride : 3;
        return 1;
    }

    // ===== Special Bonuses =====
    public static int sevenLetterBonus = 50;
    public static void SetSevenLetterBonus(int v) => sevenLetterBonus = Mathf.Max(0, v);
    public static int  GetSevenLetterBonus() => sevenLetterBonus;
}
