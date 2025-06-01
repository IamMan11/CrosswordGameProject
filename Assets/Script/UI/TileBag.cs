using System.Collections.Generic;
using UnityEngine;

[System.Serializable]          // ใช้คู่กับ Inspector
public class LetterCount
{
    public LetterData data;    // Sprite / คะแนน ฯลฯ
    [Range(1,99)]
    public int count = 1;      // จำนวนตัวที่ใส่ลงถุง
}

public class TileBag : MonoBehaviour
{
    public static TileBag Instance { get; private set; }

    [Header("Setup (ต้องรวมกัน = 100)")]
    public List<LetterCount> initialLetters = new();   // จัดสรร A‑Z ตามใจ

    private readonly List<LetterData> pool = new();    // ถุงจริงหลังแตกตัว

    public int TotalInitial { get; private set; }      // 100
    public int Remaining   => pool.Count;              // เหลือในถุง
    int drawsSinceSpecial = 0; 

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        foreach (var lc in initialLetters)
        {
            TotalInitial += lc.count;
            for (int i = 0; i < lc.count; i++) pool.Add(lc.data);
        }

        int extra = PlayerProgressSO.Instance.data.extraTiles;        // ✨
        for (int i = 0; i < extra; i++)
        {
            var template = initialLetters[Random.Range(0, initialLetters.Count)].data;
            pool.Add(template);
        }
        TotalInitial += extra; 
    }

    /// <summary>ดึงสุ่ม 1 ตัว (ถ้าหมดคืน null)</summary>
    public void AddExtraLetters(int extra)
    {
        for (int i = 0; i < extra; i++)
        {
            // ดึง template สักตัวจาก initialLetters (สุ่ม)
            var template = initialLetters[Random.Range(0, initialLetters.Count)].data;
            pool.Add(template);
        }
        TotalInitial += extra;         // ให้ตัวนับรวมเพิ่มด้วย
        Debug.Log($"[TileBag] เพิ่ม {extra} tiles → รวม {TotalInitial}");
    }
    
    public LetterData DrawRandomTile()
    {
        if (pool.Count == 0) return null;

        // 1) หยิบ template
        int idx      = Random.Range(0, pool.Count);
        var template = pool[idx];
        pool.RemoveAt(idx);

        // 2) สร้างสำเนาใหม่ (ไม่แชร์ obj)
        LetterData data = new LetterData
        {
            letter = template.letter,
            sprite = template.sprite,
            score  = template.score,
            isSpecial = false
        };

        // 3) ทำให้พิเศษทุก ๆ 6 draw
        drawsSinceSpecial++;
        if (drawsSinceSpecial >= 6)
        {
            data.isSpecial = true;
            drawsSinceSpecial = 0;
            
            Debug.Log($"[TileBag] สร้างตัวอักษรพิเศษ: '{data.letter}'");
        }
        return data;
    }

    /// <summary>คืนตัวอักษรกลับถุง (กรณี Undo / ยกเลิก)</summary>
    public void ReturnTile(LetterData data) => pool.Add(data);
}
