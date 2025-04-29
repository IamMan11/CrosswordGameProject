using UnityEngine;

public static class ScoreManager
{
    public static int CalcWord(int r0, int c0, int r1, int c1)
    {
        int total   = 0;
        int wordMul = 1;
        var g = BoardManager.Instance.grid;

        // กำหนดทิศ (+1, 0, -1) ให้ถูกต้องทั้งแนวตั้ง-แนวนอน
        int dr = r0 == r1 ? 0 : (r1 > r0 ? 1 : -1);
        int dc = c0 == c1 ? 0 : (c1 > c0 ? 1 : -1);

        int r = r0, c = c0;
        while (true)
        {
            var tile   = g[r, c].transform.GetChild(1).GetComponent<LetterTile>();
            int letter = tile.GetData().score;

            switch (g[r, c].type)
            {
                case SlotType.DoubleLetter: letter *= 2; break;
                case SlotType.TripleLetter: letter *= 3; break;
                case SlotType.DoubleWord:   wordMul *= 2; break;
                case SlotType.TripleWord:   wordMul *= 3; break;
            }
            total += letter;

            // ถึงตัวสุดท้ายแล้ว → ออกจากลูป
            if (r == r1 && c == c1) break;

            r += dr;
            c += dc;
        }
        return total * wordMul;
    }
}
