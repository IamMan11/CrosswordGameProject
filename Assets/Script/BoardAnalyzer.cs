using UnityEngine;

/// <summary>
/// Utilities for reading existing words on the board.
/// ปรับปรุงให้ปลอด NullReferenceException เมื่อ child ลำดับที่ 1 ไม่ใช่ LetterTile
/// </summary>
public static class BoardAnalyzer
{
    /// <summary>
    /// เก็บคำ (head → tail) เริ่มจาก slot <paramref name="start"/> ตามแกน <paramref name="orient"/>
    /// คืน false ถ้าตำแหน่งใด ๆ ในคำไม่มี LetterTile (เช่นโดนลบออกหลัง undo)
    /// </summary>
    public static bool GetWord(BoardSlot start, Orient orient,
                               out string word,
                               out int r0, out int c0, out int r1, out int c1)
    {
        word = string.Empty;
        r0 = r1 = start.row;
        c0 = c1 = start.col;
        BoardSlot[,] g = BoardManager.Instance.grid;

        int dr = orient == Orient.Vertical ? -1 : 0;
        int dc = orient == Orient.Horizontal ? -1 : 0;

        // ---------- head ----------
        int r = start.row, c = start.col;
        while (InBounds(r + dr, c + dc) && g[r + dr, c + dc].HasLetterTile())
        {
            r += dr;
            c += dc;
        }
        r0 = r;
        c0 = c;

        // ---------- collect string ----------
        dr = -dr;
        dc = -dc;
        while (true)
        {
            if (!g[r, c].HasLetterTile())
            {
                word = string.Empty;
                return false;
            }

            // ป้องกัน Index / Component null
            LetterTile tile = null;
            if (g[r, c].transform.childCount > 1)
            {
                tile = g[r, c].transform.GetChild(1).GetComponent<LetterTile>();
            }
            if (tile == null)
            {
                word = string.Empty;
                return false;
            }

            word += tile.GetData().letter;

            if (!InBounds(r + dr, c + dc) || !g[r + dr, c + dc].HasLetterTile())
            {
                r1 = r;
                c1 = c;
                break;
            }
            r += dr;
            c += dc;
        }

        // ---------- head & tail closed? ----------
        bool headClosed = !InBounds(r0 - dr, c0 - dc) || !g[r0 - dr, c0 - dc].HasLetterTile();
        bool tailClosed = !InBounds(r1 + dr, c1 + dc) || !g[r1 + dr, c1 + dc].HasLetterTile();

        return headClosed && tailClosed;
    }

    private static bool InBounds(int r, int c)
        => r >= 0 && r < BoardManager.Instance.rows &&
           c >= 0 && c < BoardManager.Instance.cols;
}
