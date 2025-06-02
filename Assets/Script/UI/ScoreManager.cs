using UnityEngine;

public static class ScoreManager
{
    // ตัวแปร
    private static int doubleLetterOverride = 0;
    private static int doubleWordOverride = 0;
    // methon
    public static void SetDoubleLetterOverride(int multiplier)
    {
        doubleLetterOverride = Mathf.Max(multiplier, 0);
    }
    public static void SetDoubleWordOverride(int multiplier)
    {
        doubleLetterOverride = Mathf.Max(multiplier, 0);
    }
    public static int CalcWord(int r0, int c0, int r1, int c1)
    {
        int total = 0;
        int wordMul = 1;
        var g = BoardManager.Instance.grid;

        // กำหนดทิศ (+1, 0, -1) ให้ถูกต้องทั้งแนวตั้ง-แนวนอน
        int dr = r0 == r1 ? 0 : (r1 > r0 ? 1 : -1);
        int dc = c0 == c1 ? 0 : (c1 > c0 ? 1 : -1);

        int r = r0, c = c0;
        while (true)
        {
            /* ---------- จุดที่ต้องแก้ ---------- */
            // เดิม:  var tile = g[r, c].transform.GetChild(1).GetComponent<LetterTile>();
            // ใหม่: ใช้ helper ปลอดภัย ไม่ยึด index
            var tile = g[r, c].GetLetterTile();      // <-- แก้แค่บรรทัดนี้
            if (tile == null)                        // ถ้าไม่เจอให้กันตก null
            {
                Debug.LogError($"[Score] ไม่มี LetterTile ที่ช่อง {r},{c}");
                return 0;
            }
            /* ------------------------------------ */

            int letter = tile.GetData().score;

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

            if (r == r1 && c == c1) break;   // ถึงตัวสุดท้ายแล้ว → ออกจากลูป
            r += dr;
            c += dc;
        }
        doubleLetterOverride = 0;
        doubleWordOverride = 0;
        return total * wordMul;
    }
}
