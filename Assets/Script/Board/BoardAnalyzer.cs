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

        var bm = BoardManager.Instance;
        if (start == null || bm == null || bm.grid == null)
            return false;

        var g = bm.grid;
        int rows = bm.rows, cols = bm.cols;

        if (!InBounds(start.row, start.col, rows, cols))
            return false;

        // กำหนดทิศถอยหลังเพื่อหา "หัว"
        int dr = orient == Orient.Vertical   ? -1 : 0;
        int dc = orient == Orient.Horizontal ? -1 : 0;

        // หา head
        int r = start.row, c = start.col;
        while (InBounds(r + dr, c + dc, rows, cols) && g[r + dr, c + dc] != null && IsRealLetter(g[r + dr, c + dc]))
        {
            r += dr;
            c += dc;
        }
        r0 = r; c0 = c;

        // พลิกเป็นทิศเดินหน้าเพื่ออ่านคำไปจนถึง tail
        dr = -dr; dc = -dc;

        while (true)
        {
            var cur = g[r, c];
            if (!IsRealLetter(cur)) { word = string.Empty; return false; }

            var tile = cur.GetLetterTile();
            if (tile == null) { word = string.Empty; return false; }

            word += tile.CurrentLetter;

            int nr = r + dr, nc = c + dc;
            if (!InBounds(nr, nc, rows, cols) || g[nr, nc] == null || !IsRealLetter(g[nr, nc]))
                break;

            r = nr; c = nc;
        }

        // r,c ตอนนี้คือตำแหน่ง tail จริง
        r1 = r; c1 = c;

        // ช่อง "ก่อนหัว" = ถอยกลับจากหัวหนึ่งก้าว (ตามทิศเดินหน้า dr/dc)
        int headPrevR = r0 - dr, headPrevC = c0 - dc;
        // ช่อง "ถัดท้าย" = เดินต่อจากหางหนึ่งก้าว
        int tailNextR = r1 + dr, tailNextC = c1 + dc;

        bool headClosed = !InBounds(headPrevR, headPrevC, rows, cols)
                        || g[headPrevR, headPrevC] == null
                        || !IsRealLetter(g[headPrevR, headPrevC]);

        bool tailClosed = !InBounds(tailNextR, tailNextC, rows, cols)
                        || g[tailNextR, tailNextC] == null
                        || !IsRealLetter(g[tailNextR, tailNextC]);

        return headClosed && tailClosed;
    }


    /// <summary>เช็กว่า (r,c) อยู่ในกรอบบอร์ดหรือไม่</summary>
    private static bool InBounds(int r, int c, int rows, int cols)
        => r >= 0 && r < rows && c >= 0 && c < cols;
}
