using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]          // ใช้คู่กับ Inspector
public class LetterCount
{
    public LetterData data;    // Sprite / คะแนน ฯลฯ
    [Range(1,199)]
    public int count = 1;      // จำนวนตัวที่ใส่ลงถุง
}

public class TileBag : MonoBehaviour
{
    public static TileBag Instance { get; private set; }

    [Header("Setup (ต้องรวมกัน = 100)")]
    public List<LetterCount> initialLetters = new();   // จัดสรร A‑Z ตามใจ

    private readonly List<LetterData> pool = new();    // ถุงจริงหลังแตกตัว
    int baseCapacity; 

    public int TotalInitial { get; private set; }      // 100
    public int Remaining   => pool.Count;              // เหลือในถุง
    int drawsSinceSpecial = 0;
    private bool infiniteMode = false;
    private Coroutine infiniteCoroutine = null;

    void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }

        // Base capacity = sum initial counts (100)
        baseCapacity = 0;
        foreach (var lc in initialLetters) baseCapacity += lc.count;

        TotalInitial = baseCapacity + PlayerProgressSO.Instance.data.extraTiles;
        RebuildPool();
        // เรียก UI ครั้งแรกหลังสร้าง pool
        TurnManager.Instance?.UpdateBagUI();
    }

    /// <summary>ดึงสุ่ม 1 ตัว (ถ้าหมดคืน null)</summary>
    /* -------------------------------------------------- public API -------------------------------------------------- */
    public void AddExtraLetters(int extra)
    {
        TotalInitial += extra;                     // expand capacity first
        for (int i = 0; i < extra; i++)            // add letters immediately
        {
            var t = initialLetters[Random.Range(0, initialLetters.Count)].data;
            pool.Add(t);
        }
        Debug.Log($"[TileBag] +{extra} tiles → {Remaining}/{TotalInitial}");
        TurnManager.Instance?.UpdateBagUI();       // sync UI
    }
    /// <summary>สร้าง (หรือสร้างใหม่) พูลตัวอักษรทั้งหมดตาม initialLetters</summary>
    void RebuildPool()
    {
        pool.Clear();
        // initial letters
        foreach (var lc in initialLetters)
            for (int i = 0; i < lc.count; i++)
                pool.Add(lc.data);
        // extras
        int extra = TotalInitial - baseCapacity;
        for (int i = 0; i < extra; i++)
        {
            var t = initialLetters[Random.Range(0, initialLetters.Count)].data;
            pool.Add(t);
        }
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
        TurnManager.Instance?.UpdateBagUI();
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
        TurnManager.Instance?.UpdateBagUI();

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
