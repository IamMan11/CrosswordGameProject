using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>ตรวจคำตามกฎ Scrabble 7 ข้อ (ย่อ)</summary>
public static class MoveValidator
{
    // ========== ENTRY ==========
    public static bool ValidateMove(List<(LetterTile t, BoardSlot s)> placed,
                                    out List<WordInfo> words,
                                    out string error)
    {
        words = new List<WordInfo>();
        error = "";

        // --- 0. เตรียมข้อมูล ---
        var grid  = BoardManager.Instance.grid;
        int rows  = BoardManager.Instance.rows;
        int cols  = BoardManager.Instance.cols;
        bool firstMove = !grid.Cast<BoardSlot>().Any(sl => sl.HasLetterTile() &&
                                                           sl.transform.GetChild(1)
                                                             .GetComponent<LetterTile>()
                                                             .isLocked);

        // --- 1. ไม่วางทับผิดกฎ & เก็บตำแหน่งที่แท้จริง ---
        foreach (var (t,s) in placed)
        {
            if (s.HasLetterTile() && s.transform.GetChild(1).GetComponent<LetterTile>() != t)
            { error = "cannot overwrite tile"; return false; }
        }

        // --- 2. ต้องอยู่แนวเดียว & ต่อเนื่อง (ข้อ 3 + 4) ---
        bool sameRow = placed.All(p => p.s.row == placed[0].s.row);
        bool sameCol = placed.All(p => p.s.col == placed[0].s.col);
        if (!(sameRow ^ sameCol)) { error = "tiles not in single line"; return false; }

        // เรียงช่องเพื่อเช็กช่องว่าง
        var ordered = sameRow
            ? placed.OrderBy(p=>p.s.col).ToList()
            : placed.OrderBy(p=>p.s.row).ToList();

        for (int i=1;i<ordered.Count;i++)
        {
            int gap = sameRow
                ? ordered[i].s.col - ordered[i-1].s.col
                : ordered[i].s.row - ordered[i-1].s.row;
            if (gap!=1 && !ordered[i-1].s.HasLetterTile())   // เว้นช่อง
            { error = "gap inside word"; return false; }
        }

        // --- 3. ตาแรกต้องผ่านจุดกลาง (ข้อ 1) ---
        if (firstMove)
        {
            int ctrR = rows/2, ctrC = cols/2;
            bool touchesCenter = placed.Any(p => p.s.row==ctrR && p.s.col==ctrC);
            if (!touchesCenter) { error = "first move must cover center"; return false; }
        }
        else
        {
            // --- 4. ต้องเชื่อมกับคำบนกระดาน (ข้อ 2) ---
            bool connected = placed.Any(p => HasNeighborLocked(p.s));
            if (!connected) { error="move not connected"; return false; }
        }

        // --- 5. สร้าง word หลัก (แนวที่วาง) + cross-words (ข้อ 7) ---
        CollectWord(ordered[0].s, sameRow?Orient.Horizontal:Orient.Vertical, words);

        foreach(var (t,s) in placed)
        {
            CollectWord(s, sameRow?Orient.Vertical:Orient.Horizontal, words);
        }

        // เอาคำซ้ำออก
        words = words.Distinct().ToList();
        return true;
    }

    // ---------- helper ----------
    static bool HasNeighborLocked(BoardSlot s)
    {
        var g = BoardManager.Instance.grid;
        int r=s.row, c=s.col;
        int rows = BoardManager.Instance.rows;
        int cols = BoardManager.Instance.cols;
        int[] dr={-1,1,0,0};
        int[] dc={0,0,-1,1};
        for(int k=0;k<4;k++)
        {
            int rr=r+dr[k], cc=c+dc[k];
            if(rr<0||rr>=rows||cc<0||cc>=cols) continue;
            if(!g[rr,cc].HasLetterTile()) continue;
            var lt=g[rr,cc].transform.GetChild(1).GetComponent<LetterTile>();
            if(lt.isLocked) return true;
        }
        return false;
    }

    public struct WordInfo
    {
        public string word;  public int r0,c0,r1,c1;
    }

    static bool CollectWord(BoardSlot start, Orient ori, List<WordInfo> list)
    {
        string word; int r0,c0,r1,c1;
        bool ok = BoardAnalyzer.GetWord(start, ori, out word,
                                        out r0, out c0, out r1, out c1);   // :contentReference[oaicite:0]{index=0}&#8203;:contentReference[oaicite:1]{index=1}
        if (ok && word.Length>1)
            list.Add(new WordInfo{word=word.ToUpper(), r0=r0,c0=c0,r1=r1,c1=c1});
        return ok;
    }
}
