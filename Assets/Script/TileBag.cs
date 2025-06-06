using System.Collections;
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
    private bool infiniteMode = false;
    private Coroutine infiniteCoroutine = null;

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
        RebuildPool(); 
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
    /// <summary>สร้าง (หรือสร้างใหม่) พูลตัวอักษรทั้งหมดตาม initialLetters</summary>
    private void RebuildPool()
    {
        pool.Clear();
        TotalInitial = 0;
        foreach (var lc in initialLetters)
        {
            TotalInitial += lc.count;
            for (int i = 0; i < lc.count; i++)
                pool.Add(lc.data);
        }

        int extra = PlayerProgressSO.Instance.data.extraTiles;        // ✨
        for (int i = 0; i < extra; i++)
        {
            var template = initialLetters[Random.Range(0, initialLetters.Count)].data;
            pool.Add(template);
        }
        TotalInitial += extra;
    }
    /// <summary>เปิดโหมด InfiniteTiles (tilepack ไม่หมด)</summary>
    public void ActivateInfinite(float duration)
    {
        if (infiniteCoroutine != null)
            StopCoroutine(infiniteCoroutine);

        infiniteMode = true;
        Debug.Log("[TileBag] InfiniteTiles Mode ON (1 นาที)");

        infiniteCoroutine = StartCoroutine(DeactivateInfiniteAfter(duration));
    }

    /// <summary>
    /// Coroutine รอแล้วปิดโหมด InfiniteTiles อัตโนมัติ
    /// </summary>
    private IEnumerator DeactivateInfiniteAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        infiniteMode = false;
        infiniteCoroutine = null;
        Debug.Log("[TileBag] InfiniteTiles Mode AUTO-OFF (หมดเวลา 1 นาที)");
    }
    /// <summary>Reset tilepack ทั้งหมด (PackRenewal) → สร้าง pool ใหม่เต็ม เช่นเดียวกับเริ่มเกม</summary>
    public void ResetPool()
    {
        RebuildPool();
        drawsSinceSpecial = 0;
        Debug.Log("[TileBag] Pool ถูกรีเซ็ตใหม่ทั้งหมด");
    }
    
    public LetterData DrawRandomTile()
    {
        if (infiniteMode)
        {
            var template = initialLetters[Random.Range(0, initialLetters.Count)].data;
            LetterData data = new LetterData
            {
                letter    = template.letter,
                sprite    = template.sprite,
                score     = template.score,
                isSpecial = false
            };
            drawsSinceSpecial++;
            if (drawsSinceSpecial >= 6)
            {
                data.isSpecial = true;
                drawsSinceSpecial = 0;
                Debug.Log($"[TileBag] (Infinite) สร้างตัวอักษรพิเศษ: '{data.letter}'");
            }
            return data;
        }

        if (pool.Count == 0) return null;

        int idx = Random.Range(0, pool.Count);
        var templateNormal = pool[idx];
        pool.RemoveAt(idx);

        LetterData dataNorm = new LetterData
        {
            letter    = templateNormal.letter,
            sprite    = templateNormal.sprite,
            score     = templateNormal.score,
            isSpecial = false
        };

        drawsSinceSpecial++;
        if (drawsSinceSpecial >= 6)
        {
            dataNorm.isSpecial = true;
            drawsSinceSpecial = 0;
            Debug.Log($"[TileBag] สร้างตัวอักษรพิเศษ: '{dataNorm.letter}'");
        }
        return dataNorm;
    }

    /// <summary>คืนตัวอักษรกลับถุง (กรณี Undo / ยกเลิก)</summary>
    public void ReturnTile(LetterData data)
    {
        if (!infiniteMode)
            pool.Add(data);
    }
}
