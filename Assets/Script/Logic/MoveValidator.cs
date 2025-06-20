using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>ตรวจเงื่อนไขการวางตามกฎ Scrabble ที่กำหนด</summary>
public static class MoveValidator
{
    // เรียกใช้จาก TurnManager.OnConfirm
    public static bool ValidateMove(
        List<(LetterTile t, BoardSlot s)> placed,
        out List<WordInfo> words,
        out string error)
    {
        words = new List<WordInfo>();
        error = "";

        var g    = BoardManager.Instance.grid;
        int rows = BoardManager.Instance.rows;
        int cols = BoardManager.Instance.cols;

        // ---------- A. ข้อมูลตาแรก ----------
        bool firstMove = !g.Cast<BoardSlot>().Any(sl =>
            sl.HasLetterTile() &&
            sl.GetLetterTile() != null &&          // << เพิ่ม
            sl.GetLetterTile().isLocked);

        // ---------- B. ห้ามวางทับผิด ----------
        foreach (var (t, s) in placed)
        {
            if (s.HasLetterTile() && s.GetLetterTile() != t)   // << เปลี่ยน
            { error = "cannot overwrite tile"; return false; }
        }

        // ---------- C. ต้องเรียงแนวเดียว & ต่อเนื่อง ----------
        bool oneTile = placed.Count == 1;
        bool sameRow = placed.All(p => p.s.row == placed[0].s.row);
        bool sameCol = placed.All(p => p.s.col == placed[0].s.col);
        if (!(sameRow ^ sameCol) && !oneTile)          // ★ ยกเว้นกรณีวางแค่ 1 ตัว
        { error = "tiles not in a straight line"; return false; }

        var ordered = sameRow
            ? placed.OrderBy(p => p.s.col).ToList()
            : placed.OrderBy(p => p.s.row).ToList();

        for (int i = 1; i < ordered.Count; i++)
        {
            int gap = sameRow
                ? ordered[i].s.col - ordered[i - 1].s.col
                : ordered[i].s.row - ordered[i - 1].s.row;

            // อนุญาตให้ช่องที่ห่างกันมีกระเบื้องเดิมคั่นกลางได้
            if (gap > 1)
            {
                // จังหวะระหว่างต้องมีตัวอักษรอยู่แล้ว
                for (int step = 1; step < gap; step++)
                {
                    int rr = sameRow ? ordered[i].s.row : ordered[i - 1].s.row + step;
                    int cc = sameRow ? ordered[i - 1].s.col + step : ordered[i].s.col;
                    if (!g[rr, cc].HasLetterTile())
                    { error = "gap inside word"; return false; }
                }
            }
        }

        // ---------- D. ตาแรกต้องคร่อมศูนย์ ----------
        if (firstMove)
        {
            int ctrR = rows / 2, ctrC = cols / 2;
            bool touchesCenter = placed.Any(p => p.s.row == ctrR && p.s.col == ctrC);
            if (!touchesCenter)
            { error = "first move must cover center"; return false; }
        }
        else
        {
            // ---------- E. ต้องเชื่อมกับตัวที่ล็อกแล้ว ----------
            bool connected = placed.Any(p => HasLockedNeighbor(p.s));
            if (!connected)
            { error = "move not connected"; return false; }
        }

        // ---------- F. สร้างคำหลัก + cross-words ----------
        if (oneTile)
        {
            // กรณีวาง 1 ตัว – ต้องเช็กทั้ง H และ V เพื่อดูว่าประกอบคำใหม่หรือไม่
            CollectWord(placed[0].s, Orient.Horizontal, words);
            CollectWord(placed[0].s, Orient.Vertical,   words);
        }
        else
        {
            Orient mainOri  = sameRow ? Orient.Horizontal : Orient.Vertical;
            Orient crossOri = sameRow ? Orient.Vertical   : Orient.Horizontal;

            CollectWord(ordered[0].s, mainOri, words);       // คำหลัก
            foreach (var (_, slot) in placed) CollectWord(slot, crossOri, words);
        }

        // ตัดคำซ้ำ & ยอมรับยาว 1 ตัวได้แล้ว
        if (words.Count == 0)          // ★ ไม่มีคำเกิดขึ้น → ไม่อนุญาต
        {
            error = "no word formed";
            return false;
        }
        // ---------- G. เรียงคำจากซ้าย→ขวา บน→ล่าง ----------
        words.Sort((a, b) =>
        {
            if (a.r0 != b.r0) return a.r0.CompareTo(b.r0);   // ① ตามแถว (row)
            return a.c0.CompareTo(b.c0);                      // ② ตามคอลัมน์ (col)
        });
        // อย่าทำ Distinct — ต้องเก็บคำซ้ำไว้ด้วย
        return true;
    }

    // ---------- helper ----------
    static bool HasLockedNeighbor(BoardSlot s)
    {
        var g = BoardManager.Instance.grid;
        int r = s.row, c = s.col;
        int rows = BoardManager.Instance.rows, cols = BoardManager.Instance.cols;
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = {  0, 0,-1, 1 };
        for (int k = 0; k < 4; k++)
        {
            int rr = r + dr[k], cc = c + dc[k];
            if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) continue;
            if (!g[rr, cc].HasLetterTile()) continue;
            var lt = g[rr, cc].GetLetterTile(); 
            if (lt.isLocked) return true;
        }
        return false;
    }

    public struct WordInfo { public string word; public int r0, c0, r1, c1; }

    static void CollectWord(BoardSlot start, Orient ori, List<WordInfo> list)
    {
        string w; int r0, c0, r1, c1;
        bool ok = BoardAnalyzer.GetWord(start, ori, out w, out r0, out c0, out r1, out c1);
        if (ok && w.Length > 0)      // ← ยอมรับความยาว 1 ตัว
            list.Add(new WordInfo { word = w.ToUpper(), r0 = r0, c0 = c0, r1 = r1, c1 = c1 });
    }
}
