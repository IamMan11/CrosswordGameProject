using UnityEngine;

/// <summary>
/// BoardAnalyzer
/// ยูทิลิตี้สำหรับอ่าน "คำ" ที่มีอยู่บนกระดานจากจุดเริ่มต้น (slot) ไปตามแกนแนวนอน/แนวตั้ง
/// - ปลอดภัยต่อ Null/Out-of-bounds (เช็กทุกทางออก)
/// - คงพฤติกรรมเดิม: ถ้าพบช่องว่างระหว่างทาง ให้คืน false และตั้ง word เป็น string.Empty
/// - นิยาม "คำสมบูรณ์" = head และ tail ปิด (ตำแหน่งถัดไปของหัว/ท้ายต้องไม่มีตัวอักษร)
/// </summary>
public static class BoardAnalyzer
{
    /// <summary>
    /// รวบรวมคำ (จาก head → tail) เริ่มที่ <paramref name="start"/> ตามแกน <paramref name="orient"/>
    /// คืนค่า:
    /// - true  : ได้คำสมบูรณ์ (head/tail ปิด)
    /// - false : ระหว่างทางเจอช่องที่ไม่มี LetterTile หรือข้อมูลบอร์ดไม่พร้อม
    /// Output:
    /// - word : คำที่อ่านได้ (ถ้าล้มเหลวจะเป็น string.Empty)
    /// - r0,c0: ตำแหน่งหัวคำ
    /// - r1,c1: ตำแหน่งท้ายคำ
    /// </summary>
    public static bool IsRealLetter(BoardSlot s)
    {
        if (s == null || !s.HasLetterTile()) return false;
        if (Level1GarbledIT.Instance != null && Level1GarbledIT.Instance.IsGarbledSlot(s)) return false;
        return true;
    }
    public static bool GetWord(
        BoardSlot start, Orient orient,
        out string word,
        out int r0, out int c0, out int r1, out int c1)
    {
        word = string.Empty;
        r0 = c0 = r1 = c1 = 0;

        // ---------- ตรวจความพร้อมของบอร์ดและจุดเริ่ม ----------
        var bm = BoardManager.Instance;
        if (start == null || bm == null || bm.grid == null)
            return false;

        var g = bm.grid;
        int rows = bm.rows, cols = bm.cols;

        if (!InBounds(start.row, start.col, rows, cols))
            return false;

        // ตั้งค่าเริ่มต้นเป็นจุดเริ่ม
        r0 = r1 = start.row;
        c0 = c1 = start.col;

        // ระบุทิศทางเริ่ม "ไล่ย้อนกลับ" เพื่อหา head
        // Vertical : ไล่ขึ้น (dr = -1, dc = 0)
        // Horizontal: ไล่ซ้าย (dr = 0,  dc = -1)
        int dr = orient == Orient.Vertical   ? -1 : 0;
        int dc = orient == Orient.Horizontal ? -1 : 0;

        // ---------- หา head (ถอยไปจนกว่าช่องก่อนหน้าจะไม่ใช่ตัวอักษร) ----------
        int r = start.row, c = start.col;
        while (InBounds(r + dr, c + dc, rows, cols) && g[r + dr, c + dc] != null && IsRealLetter(g[r+dr,c+dc]))
        {
            r += dr;
            c += dc;
        }
        r0 = r; c0 = c;

        // ---------- สะสมตัวอักษรจาก head → tail ----------
        // พลิกทิศจากย้อนกลับ → เดินหน้า
        dr = -dr;
        dc = -dc;

        while (true)
        {
            // ถ้าตำแหน่งปัจจุบันไม่มีตัวอักษร ถือว่าล้มเหลว
            var cur = g[r, c];
            if (!IsRealLetter(cur)) { word = string.Empty; return false; }

            // อ่านอักษรจาก LetterTile (ป้องกัน null)
            LetterTile tile = cur.GetLetterTile();
            if (tile == null)
            {
                word = string.Empty;
                return false;
            }
            word += (tile != null ? tile.CurrentLetter : string.Empty);

            // ถ้าตำแหน่งถัดไป (ตามแกน) ออกนอกขอบ หรือไม่มีตัวอักษร → เจอ tail แล้ว
            int nr = r + dr, nc = c + dc;
            if (!InBounds(nr, nc, rows, cols) || g[nr, nc] == null || !g[nr, nc].HasLetterTile())
            {
                r1 = r; c1 = c;
                break;
            }

            r = nr; c = nc;
        }

        // ---------- ตรวจว่าหัว/ท้าย "ปิด" หรือไม่ ----------
        // "ปิด" = ช่องก่อนหน้าหัว/ช่องถัดไปท้าย ต้องอยู่นอกขอบ หรือไม่มี LetterTile
        bool headClosed =
            !InBounds(r0 - dr, c0 - dc, rows, cols) ||
            g[r0 - dr, c0 - dc] == null || !g[r0 - dr, c0 - dc].HasLetterTile();

        bool tailClosed =
            !InBounds(r1 + dr, c1 + dc, rows, cols) ||
            g[r1 + dr, c1 + dc] == null || !g[r1 + dr, c1 + dc].HasLetterTile();

        return headClosed && tailClosed;
    }

    /// <summary>เช็กว่า (r,c) อยู่ในกรอบบอร์ดหรือไม่</summary>
    private static bool InBounds(int r, int c, int rows, int cols)
        => r >= 0 && r < rows && c >= 0 && c < cols;
}
