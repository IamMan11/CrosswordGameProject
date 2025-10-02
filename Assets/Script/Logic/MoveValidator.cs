using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ตรวจความถูกต้องของการวางตัวอักษร (ตามกฎ Scrabble ของโปรเจกต์)
/// เรียกจาก TurnManager.OnConfirm
/// กติกาหลัก:
///   A) ตรวจข้อมูลตาแรก
///   B) ห้ามวางทับตัวเดิมที่ล็อกไว้ (ยกเว้นวัตถุเดียวกันตอนจัดวาง)
///   C) ตัวใหม่ทั้งหมดต้องอยู่ในแนวเดียวกันและต่อเนื่อง (อนุญาตมีไทล์เก่าคั่น)
///   D) ตาแรกต้องคร่อมช่องกึ่งกลาง และต้องวางขั้นต่ำตาม minWordLength (≥2)
///   E) ตาถัด ๆ ไป ต้องติดกับตัวที่ล็อกไว้แล้วอย่างน้อย 1 จุด
///   F) สร้างคำหลักและ cross-words
///   G) ต้องเกิดอย่างน้อย 1 คำ และเรียงผลลัพธ์จากบน→ล่าง ซ้าย→ขวา
/// </summary>
public static class MoveValidator
{
    /// <summary>
    /// ตรวจการวางครั้งนี้
    /// </summary>
    /// <param name="placed">ลิสต์คู่ (ไทล์, สลอต) ของตัวที่ “วางใหม่” ในตานี้เท่านั้น</param>
    /// <param name="words">ผลลัพธ์: คำทั้งหมดที่เกิดขึ้น (คำหลัก + cross)</param>
    /// <param name="error">ข้อความอธิบายสาเหตุที่ไม่ผ่าน</param>
    public static bool ValidateMove(
        List<(LetterTile t, BoardSlot s)> placed,
        out List<WordInfo> words,
        out string error)
    {
        words = new List<WordInfo>();
        error = string.Empty;

        // ---------- Guard พื้นฐาน ----------
        if (placed == null || placed.Count == 0)
        { error = "no tiles placed"; return false; }

        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null)
        { error = "board not ready"; return false; }

        var grid = bm.grid;
        int rows = bm.rows, cols = bm.cols;

        // ---------- A. ข้อมูลตาแรก ----------
        // firstMove = “ยังไม่มีตัวอักษรที่ถูกล็อกบนกระดานเลย”
        bool firstMove = !grid.Cast<BoardSlot>().Any(sl =>
        {
            if (sl == null || !sl.HasLetterTile()) return false;
            if (Level1GarbledIT.Instance != null && Level1GarbledIT.Instance.IsGarbledSlot(sl)) return false;
            var lt = sl.GetLetterTile();
            return lt != null && lt.isLocked;
        });

        // ---------- B. ห้ามวางทับผิด ----------
        // ถ้าช่องมีไทล์อยู่แล้ว และไม่ใช่ตัวเดียวกับที่กำลังวาง → ผิด
        foreach (var (t, s) in placed)
        {
            if (s == null || t == null) { error = "invalid target"; return false; }
            if (s.HasLetterTile() && s.GetLetterTile() != t)
            { error = "cannot overwrite tile"; return false; }
        }
        if (Level3Controller.Instance != null && Level3Controller.Instance.IsFreePlacementPhase())
        {
            foreach (var (t, s) in placed)
            {
                if (s == null) { error = "invalid target"; return false; }
                if (TryGetCoords(s, out int rr, out int cc) && Level2Controller.IsTriangleCell(rr, cc))
                {
                    error = "cannot place on triangle point";
                    return false;
                }
            }
        }
        // ===== Free-placement (สำหรับ L3 vanish) =====
        // ระหว่างบอสเฟส vanish (HP ≤ 50%): อนุญาตวางกระจัดกระจาย ไม่บังคับแนว/ต่อเนื่อง/คร่อมศูนย์/ติดกับของเก่า
        // แต่ยังคง "อ่านคำ" แนวนอน+แนวตั้ง รอบตำแหน่งที่วางใหม่ และส่งไปเช็กถูก/ผิด/คำซ้ำตามปกติ
        if (Level3Controller.Instance != null && Level3Controller.Instance.IsFreePlacementPhase())
        {
            // เก็บคำที่เกิดขึ้นจากทุกตัวที่วางใหม่ (อ่าน H+V ทุกจุด)
            var temp = new List<WordInfo>(8);
            foreach (var (_, s) in placed)
            {
                if (s == null) { error = "invalid target"; return false; }
                CollectWord(s, Orient.Horizontal, temp);
                CollectWord(s, Orient.Vertical,   temp);
            }

            // กรองซ้ำด้วย key ตำแหน่งหัว-ท้าย (ปลายทางกำหนดแนวอยู่แล้ว)
            var uniqKeys = new HashSet<string>();
            words = new List<WordInfo>(temp.Count);
            foreach (var w in temp)
            {
                string key = $"{w.r0}|{w.c0}|{w.r1}|{w.c1}";
                if (uniqKeys.Add(key)) words.Add(w);
            }

            if (words.Count == 0)
            { error = "no word formed"; return false; }

            // ข้ามกฎ C, D, E ไปเลยในโหมดนี้ แล้วให้ระบบเดิมไปเช็กคำถูก/ผิด/คำซ้ำต่อ
            return true;
        }

        // ---------- C. ต้องเรียงแนวเดียว & ต่อเนื่อง ----------
        bool oneTile = placed.Count == 1;
        bool sameRow = placed.All(p => p.s.row == placed[0].s.row);
        bool sameCol = placed.All(p => p.s.col == placed[0].s.col);

        // วาง >1 ตัว ต้องเป็นแนวเดียวกัน (อนุญาตวาง 1 ตัวได้)
        if (!(sameRow ^ sameCol) && !oneTile)
        { error = "tiles not in a straight line"; return false; }

        // จัดลำดับซ้าย→ขวา (ถ้าแนวนอน) หรือบน→ล่าง (ถ้าแนวตั้ง)
        var ordered = sameRow
            ? placed.OrderBy(p => p.s.col).ToList()
            : placed.OrderBy(p => p.s.row).ToList();

        // ตรวจช่องเว้นระหว่างตัวที่วางใหม่: ต้องมี “ตัวเก่า” คั่นครบ
        for (int i = 1; i < ordered.Count; i++)
        {
            int gap = sameRow
                ? ordered[i].s.col - ordered[i - 1].s.col
                : ordered[i].s.row - ordered[i - 1].s.row;

            if (gap > 1)
            {
                for (int step = 1; step < gap; step++)
                {
                    int rr = sameRow ? ordered[i].s.row : ordered[i - 1].s.row + step;
                    int cc = sameRow ? ordered[i - 1].s.col + step : ordered[i].s.col;

                    if (rr < 0 || rr >= rows || cc < 0 || cc >= cols || grid[rr, cc] == null || !grid[rr, cc].HasLetterTile())
                    { error = "gap inside word"; return false; }
                }
            }
        }

        // ---------- D. ตาแรกต้องคร่อมศูนย์ + ต้องวางขั้นต่ำตาม minWordLength ----------
        int minNewTilesPerMove = (WordChecker.Instance != null)
            ? Mathf.Max(2, WordChecker.Instance.minWordLength)  // ผูกกฎพจนานุกรม (อย่างน้อย 2)
            : 2;

        if (placed.Count < minNewTilesPerMove)
        {
            error = $"must place at least {minNewTilesPerMove} tiles";
            return false;
        }

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
            // วาง 1 ตัว: ตรวจทั้งแนวนอนและแนวตั้ง (เพื่อดูว่าประกอบกับตัวเก่าแล้วเกิดคำไหม)
            CollectWord(placed[0].s, Orient.Horizontal, words);
            CollectWord(placed[0].s, Orient.Vertical,   words);
        }
        else
        {
            var mainOri  = sameRow ? Orient.Horizontal : Orient.Vertical;
            var crossOri = sameRow ? Orient.Vertical   : Orient.Horizontal;

            CollectWord(ordered[0].s, mainOri, words);           // คำหลัก
            foreach (var (_, slot) in placed) CollectWord(slot, crossOri, words); // cross-words ตามจุดที่วางใหม่
        }

        // ต้องได้คำอย่างน้อย 1 คำ
        if (words.Count == 0)
        { error = "no word formed"; return false; }

        // ---------- G. เรียงคำจากบน→ล่าง แล้วซ้าย→ขวา ----------
        words.Sort((a, b) =>
        {
            if (a.r0 != b.r0) return a.r0.CompareTo(b.r0);
            return a.c0.CompareTo(b.c0);
        });

        // (จงใจไม่ Distinct: บางดีไซน์ต้องการเก็บคำซ้ำ)
        return true;
    }
    static bool TryGetCoords(BoardSlot s, out int r, out int c)
    {
        r = c = -1;
        var bm = BoardManager.Instance; if (bm?.grid == null || s == null) return false;
        for (int i = 0; i < bm.rows; i++)
            for (int j = 0; j < bm.cols; j++)
                if (bm.grid[i, j] == s) { r = i; c = j; return true; }
        return false;
    }
    // ==================== Helpers ====================

    /// <summary>มีเพื่อนบ้านที่ “ล็อกแล้ว” ติดอยู่หรือไม่ (4 ทิศ)</summary>
    static bool HasLockedNeighbor(BoardSlot s)
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null || s == null) return false;

        var g = bm.grid;
        int r = s.row, c = s.col;
        int rows = bm.rows, cols = bm.cols;

        int[] dr = { -1, 1, 0, 0 };
        int[] dc = {  0, 0,-1, 1 };

        for (int k = 0; k < 4; k++)
        {
            int rr = r + dr[k], cc = c + dc[k];
            if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) continue;

            var nb = g[rr, cc];
            if (nb == null || !nb.HasLetterTile()) continue;

            // ข้ามสลอต Garbled ที่ยังไม่ถูกแก้
            if (Level1GarbledIT.Instance != null && Level1GarbledIT.Instance.IsGarbledSlot(nb))
                continue;

            var lt = nb.GetLetterTile();
            if (lt != null && lt.isLocked) return true;
        }
        return false;
    }


    /// <summary>โครงสร้างเก็บคำที่เจอ</summary>
    public struct WordInfo { public string word; public int r0, c0, r1, c1; }

    /// <summary>
    /// อ่านคำจาก <paramref name="start"/> ตามแกน <paramref name="ori"/>
    /// ใส่ลง <paramref name="list"/> ถ้าผ่านเกณฑ์: เป็น “คำสมบูรณ์” และยาว ≥ minWordLength
    /// </summary>
    static void CollectWord(BoardSlot start, Orient ori, List<WordInfo> list)
    {
        if (start == null || list == null) return;

        string w; int r0, c0, r1, c1;
        bool ok = BoardAnalyzer.GetWord(start, ori, out w, out r0, out c0, out r1, out c1);

        int minLen = (WordChecker.Instance != null)
            ? Mathf.Max(2, WordChecker.Instance.minWordLength)
            : 2;

        if (ok && !string.IsNullOrWhiteSpace(w) && w.Trim().Length >= minLen)
            list.Add(new WordInfo { word = w.ToUpper(), r0 = r0, c0 = c0, r1 = r1, c1 = c1 });
    }
}
