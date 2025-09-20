using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LetterCount
{
    public LetterData data;          // ข้อมูลตัวอักษร (sprite/คะแนน)
    [Range(1, 199)]
    public int count = 1;            // จำนวนตัวที่ใส่ลงถุง
}

/// <summary>
/// TileBag
/// - ถุงตัวอักษรของเกม: สร้างพูลจากสัดส่วนตั้งต้น + extraTiles จาก Progress
/// - จั่วแบบสุ่ม, รองรับโหมด Infinite (ไม่ลดจำนวน), คืนไทล์ได้
/// - มีตัวพิเศษ (isSpecial) ทุก ๆ 6 การจั่ว (ทั้งโหมดปกติและ Infinite)
/// </summary>
public class TileBag : MonoBehaviour
{
    public static TileBag Instance { get; private set; }

    [Header("Setup (ควรรวมกันได้ค่าพื้นฐาน เช่น 100)")]
    public List<LetterCount> initialLetters = new();   // จัดสรร A-Z ตามต้องการ

    // พูลจริงของไทล์ที่จั่วได้
    private readonly List<LetterData> pool = new();

    // ความจุพื้นฐาน (sum ของ count)
    int baseCapacity;

    /// <summary>จำนวนตัวทั้งหมดตั้งต้น + extra ณ ตอนสร้าง/รีฟิล</summary>
    public int TotalInitial { get; private set; }

    /// <summary>จำนวนตัวที่เหลืออยู่ในถุงตอนนี้</summary>
    public int Remaining => pool.Count;

    // ตัวพิเศษทุก ๆ 6 จั่ว
    int drawsSinceSpecial = 0;

    // โหมด Infinite (จั่วไม่ลด)
    private bool infiniteMode = false;
    private Coroutine infiniteCoroutine = null;

    void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }

        // คำนวณ baseCapacity แบบปลอดภัย
        baseCapacity = 0;
        foreach (var lc in initialLetters)
            if (lc != null) baseCapacity += Mathf.Max(0, lc.count);

        // ดึง extra จาก Progress (กัน null)
        int extra = PlayerProgressSO.Instance?.data?.extraTiles ?? 0;
        extra = Mathf.Max(0, extra);

        TotalInitial = baseCapacity + extra;

        RebuildPool();                      // สร้าง pool ครั้งแรก
        TurnManager.Instance?.UpdateBagUI();// ซิงก์ UI
    }

    /* -------------------------------------------------- Public API -------------------------------------------------- */

    /// <summary>เพิ่มความจุถุงแบบ runtime + เติมไทล์สุ่มทันที (ใช้ตอนอัปเกรดถุง)</summary>
    public void AddExtraLetters(int extra)
    {
        extra = Mathf.Max(0, extra);
        if (extra == 0) return;

        TotalInitial += extra;

        if (initialLetters == null || initialLetters.Count == 0)
        {
            Debug.LogWarning("[TileBag] initialLetters ว่าง — ไม่สามารถเพิ่มไทล์สุ่มได้");
            TurnManager.Instance?.UpdateBagUI();
            return;
        }

        for (int i = 0; i < extra; i++)
        {
            var pick = initialLetters[Random.Range(0, initialLetters.Count)];
            if (pick?.data != null) pool.Add(pick.data);
        }

        Debug.Log($"[TileBag] +{extra} tiles → {Remaining}/{TotalInitial}");
        TurnManager.Instance?.UpdateBagUI();
    }
    public void AddRandomToPool(int count)
    {
        count = Mathf.Max(0, count);
        if (count == 0) return;
        if (initialLetters == null || initialLetters.Count == 0) return;

        for (int i = 0; i < count; i++)
        {
            var pick = initialLetters[Random.Range(0, initialLetters.Count)];
            if (pick?.data != null) pool.Add(pick.data);
        }
        TurnManager.Instance?.UpdateBagUI(); // อัปเดต UI 70/100 -> 80/100 ฯลฯ
    }

    /// <summary>รีเซ็ต pool ทั้งหมดให้เหมือนเริ่มเกมใหม่ (ใช้ตอนเปลี่ยนด่าน/กดรีเซ็ต)</summary>
    public void ResetPool()
    {
        RebuildPool();
        drawsSinceSpecial = 0;
        Debug.Log("[TileBag] Pool ถูกรีเซ็ตใหม่ทั้งหมด");
        TurnManager.Instance?.UpdateBagUI();
    }

    /// <summary>เปิดโหมด InfiniteTiles (จั่วไม่ลด และสุ่มพิเศษทุก ๆ 6 จั่ว)</summary>
    public void ActivateInfinite(float duration)
    {
        if (infiniteCoroutine != null) StopCoroutine(infiniteCoroutine);

        infiniteMode = true;
        Debug.Log($"[TileBag] InfiniteTiles Mode ON ({duration:0.#}s)");

        infiniteCoroutine = StartCoroutine(DeactivateInfiniteAfter(duration));
    }

    /// <summary>จั่ว 1 ตัวแบบสุ่ม (null ถ้าหมด และไม่ใช่โหมด Infinite)</summary>
    public LetterData DrawRandomTile()
    {
        // โหมด Infinite: สร้างสำเนาจาก template โดยไม่ลดจำนวนในถุง
        if (infiniteMode)
        {
            if (initialLetters == null || initialLetters.Count == 0) return null;

            var template = initialLetters[Random.Range(0, initialLetters.Count)]?.data;
            if (template == null) return null;

            var data = new LetterData
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

        // โหมดปกติ: ดึงจาก pool (ลดจำนวน)
        if (pool.Count == 0) return null;

        int idx = Random.Range(0, pool.Count);
        var templateNormal = pool[idx];
        pool.RemoveAt(idx);
        TurnManager.Instance?.UpdateBagUI();

        if (templateNormal == null) return null;

        var dataNorm = new LetterData
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
        }

        return dataNorm;
    }

    /// <summary>คืนตัวอักษรกลับถุง (เฉพาะโหมดปกติเท่านั้น)</summary>
    public void ReturnTile(LetterData data)
    {
        if (!infiniteMode && data != null)
            pool.Add(data);
    }

    /// <summary>
    /// รีฟิลถุงทั้งหมดใหม่ (คำนวณ TotalInitial จาก progress ล่าสุด แล้วสร้างพูลใหม่)
    /// ใช้ในจังหวะออกจาก Shop/อัปเกรดจำนวนไทล์ เป็นต้น
    /// </summary>
    public void RefillTileBag()
    {
        // 1) ล้างถุงเดิม
        pool.Clear();

        // 2) เติมตัวอักษรพื้นฐาน
        int baseCount = 0;
        if (initialLetters != null)
        {
            foreach (var lc in initialLetters)
            {
                if (lc == null || lc.data == null || lc.count <= 0) continue;
                baseCount += lc.count;
                for (int i = 0; i < lc.count; i++) pool.Add(lc.data);
            }
        }

        // 3) อ่าน extraTiles ปัจจุบัน
        int extra = Mathf.Max(0, PlayerProgressSO.Instance?.data?.extraTiles ?? 0);

        // 4) อัปเดต TotalInitial = base + extra
        TotalInitial = baseCount + extra;

        // 5) เติมตัวอักษรสุ่มจำนวน extra
        if (initialLetters != null && initialLetters.Count > 0)
        {
            for (int i = 0; i < extra; i++)
            {
                var pick = initialLetters[Random.Range(0, initialLetters.Count)];
                if (pick?.data != null) pool.Add(pick.data);
            }
        }

        // 6) รีเซ็ตตัวนับพิเศษ
        drawsSinceSpecial = 0;

        // 7) ซิงก์ UI
        TurnManager.Instance?.UpdateBagUI();
    }

    /* -------------------------------------------------- Internal -------------------------------------------------- */

    /// <summary>สร้าง (หรือสร้างใหม่) พูลทั้งหมดจาก initialLetters + extra (อิงค่า TotalInitial ปัจจุบัน)</summary>
    void RebuildPool()
    {
        pool.Clear();

        // เติมพื้นฐาน
        if (initialLetters != null)
        {
            foreach (var lc in initialLetters)
            {
                if (lc == null || lc.data == null || lc.count <= 0) continue;
                for (int i = 0; i < lc.count; i++)
                    pool.Add(lc.data);
            }
        }

        // เติม extras = TotalInitial - baseCapacity (กันค่าติดลบ)
        int extra = Mathf.Max(0, TotalInitial - baseCapacity);

        if (initialLetters != null && initialLetters.Count > 0)
        {
            for (int i = 0; i < extra; i++)
            {
                var pick = initialLetters[Random.Range(0, initialLetters.Count)];
                if (pick?.data != null) pool.Add(pick.data);
            }
        }
    }

    /// <summary>คอร์รุตีนปิดโหมด Infinite อัตโนมัติหลังครบเวลา</summary>
    private IEnumerator DeactivateInfiniteAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        infiniteMode = false;
        infiniteCoroutine = null;
        Debug.Log("[TileBag] InfiniteTiles Mode OFF (หมดเวลา)");
    }
}
