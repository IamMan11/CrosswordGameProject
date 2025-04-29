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
                case SlotType.DoubleLetter: letter *= 2; break;
                case SlotType.TripleLetter: letter *= 3; break;
                case SlotType.DoubleWord:   wordMul *= 2; break;
                case SlotType.TripleWord:   wordMul *= 3; break;
            }
            total += letter;

            if (r == r1 && c == c1) break;   // ถึงตัวสุดท้ายแล้ว → ออกจากลูป
            r += dr;
            c += dc;
        }
        return total * wordMul;
    }
}
