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

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        foreach (var lc in initialLetters)
        {
            TotalInitial += lc.count;
            for (int i = 0; i < lc.count; i++) pool.Add(lc.data);
        }

        if (TotalInitial != 100)
            Debug.LogWarning($"[TileBag] คุณตั้งรวม {TotalInitial} ตัว (ควร = 100)");
    }

    /// <summary>ดึงสุ่ม 1 ตัว (ถ้าหมดคืน null)</summary>
    public LetterData DrawRandomTile()
    {
        if (pool.Count == 0) return null;
        int idx = Random.Range(0, pool.Count);
        LetterData data = pool[idx];
        pool.RemoveAt(idx);
        return data;
    }

    /// <summary>คืนตัวอักษรกลับถุง (กรณี Undo / ยกเลิก)</summary>
    public void ReturnTile(LetterData data) => pool.Add(data);
}
