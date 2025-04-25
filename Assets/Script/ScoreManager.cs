using UnityEngine;

public static class ScoreManager
{
    public static int CalcWord(int r0, int c0, int r1, int c1)
    {
        int total = 0, wordMul = 1;
        int dr = r0 == r1 ? 0 : 1;
        int dc = c0 == c1 ? 0 : 1;
        var g = BoardManager.Instance.grid;

        for (int r = r0, c = c0; r <= r1 && c <= c1; r += dr, c += dc)
        {
            var tile = g[r, c].transform.GetChild(1).GetComponent<LetterTile>();
            int letter = tile.GetData().score;

            switch (g[r, c].type)      // ตัวคูณใช้ได้เสมอเพราะ tile ถูกล็อกแล้ว
            {
                case SlotType.DoubleLetter: letter *= 2; break;
                case SlotType.TripleLetter: letter *= 3; break;
                case SlotType.DoubleWord:   wordMul *= 2; break;
                case SlotType.TripleWord:   wordMul *= 3; break;
            }
            total += letter;
        }
        return total * wordMul;
    }
}
