using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ใส่สคริปต์นี้บน Empty GameObject "BenchManager"
/// </summary>
public class BenchManager : MonoBehaviour
{
    [Header("Prefabs & Pool")]
    public GameObject letterTilePrefab;

    [Header("Slot Positions (10)")]
    public List<Transform> slotTransforms = new List<Transform>();

    public static BenchManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start() => RefillEmptySlots();

    /// <summary>เติมทุกช่องว่าง (initial หรือเมื่อใช้ Bench Blitz)</summary>
    public void RefillEmptySlots()
    {
        foreach (Transform slot in slotTransforms)
        {
            if (slot.childCount > 0) continue;
            var data = TileBag.Instance.DrawRandomTile();
            if (data == null) break;
            var tileGO = Instantiate(letterTilePrefab, slot, false);
            tileGO.transform.localPosition = Vector3.zero;
            tileGO.transform.localScale = Vector3.one;
            tileGO.GetComponent<LetterTile>().Setup(data);
        }
    }

    /// <summary>เติมแค่ช่องแรกที่ว่าง</summary>
    public void RefillOneSlot()
    {
        var empty = GetFirstEmptySlot();
        if (empty == null)
        {
            Debug.LogWarning("[BenchManager] ไม่มีช่อง Bench ว่าง");
            return;
        }

        var data = TileBag.Instance.DrawRandomTile();
        if (data == null)
        {
            Debug.LogWarning("[BenchManager] ไม่มีตัวอักษรให้ดึงจาก TileBag");
            return;
        }

        var tileGO = Instantiate(letterTilePrefab, empty.transform, false);
        tileGO.transform.localPosition = Vector3.zero;
        tileGO.transform.localScale = Vector3.one;

        var tile = tileGO.GetComponent<LetterTile>();
        tile.Setup(data);
        tile.AdjustSizeToParent();
    }

    /// <summary>คืน Tile ไปที่ Bench (ใส่ใน slot แรกที่ว่าง)</summary>
    public void ReturnTile(LetterTile tile)
    {
        var empty = GetFirstEmptySlot();
        if (empty != null)
        {
            tile.transform.SetParent(empty.transform, false);
            tile.transform.localPosition = Vector3.zero;
            tile.transform.localScale = Vector3.one;
        }
        else Destroy(tile.gameObject);
        RefillEmptySlots();
    }

    /// <summary>คืน BenchSlot แรกที่ว่าง</summary>
    public BenchSlot GetFirstEmptySlot()
    {
        foreach (Transform t in slotTransforms)
        {
            if (t.childCount == 0)
            {
                var slot = t.GetComponent<BenchSlot>();
                if (slot != null)
                    return slot;
            }
        }
        return null;
    }

    // ========================== เมธอดช่วยสำหรับเอฟเฟกต์ใหม่ ==========================

    /// <summary>
    /// (8) Full Rerack – ลบตัวอักษรใน Bench ทั้งหมด แล้วดึงตัวอักษรชุดใหม่จาก TileBag
    /// </summary>
    public void FullRerack()
    {
        // 1) ทำให้ Bench ว่าง: ทำลาย GameObject ของ LetterTile ทุกตัวใน Bench
        foreach (Transform slot in slotTransforms)
        {
            if (slot.childCount > 0)
            {
                // child ตัวเดียวคือ GameObject ของ LetterTile
                Destroy(slot.GetChild(0).gameObject);
            }
        }
        // 2) จากนั้นเติมใหม่ทุกช่องว่าง
        RefillEmptySlots();
    }

    /// <summary>
    /// (9/10) ReplaceRandomWithSpecial(count) – 
    /// สุ่มเลือกช่องใน Bench ที่มีตัวอักษรอยู่ แล้วแทนที่ด้วยตัวพิเศษจาก TileBag 
    /// count = จำนวนตัวที่อยากให้เป็นพิเศษ (1 = Glyph Spark, 2 = Twin Sparks)
    /// </summary>
    public void ReplaceRandomWithSpecial(int count)
    {
        // เก็บเฉพาะ slot ที่มีตัวอักษรวางอยู่ (ไม่เอาช่องว่าง)
        var filledSlots = new List<Transform>();
        foreach (var slot in slotTransforms)
        {
            if (slot.childCount > 0)
                filledSlots.Add(slot);
        }
        if (filledSlots.Count == 0) return;

        // เลือกสุ่มและแทนที่ ตามจำนวน count (max = filledSlots.Count หากชนกันจะหยุดก่อน)
        for (int i = 0; i < count && filledSlots.Count > 0; i++)
        {
            int idx = Random.Range(0, filledSlots.Count);
            Transform slot = filledSlots[idx];

            // 1) ทำลายตัวอักษรเดิมใน slot
            Destroy(slot.GetChild(0).gameObject);

            // 2) ดึงตัวอักษรใหม่จาก TileBag จนเจอ isSpecial = true
            LetterData data;
            do
            {
                data = TileBag.Instance.DrawRandomTile();
                if (data == null) break;  // ถ้าถุงหมดก็ออก
            } while (!data.isSpecial);

            if (data != null)
            {
                // สร้างตัว LetterTile ใหม่บน slot เดิม
                var tileGO = Instantiate(letterTilePrefab, slot, false);
                tileGO.transform.localPosition = Vector3.zero;
                tileGO.transform.localScale = Vector3.one;
                tileGO.GetComponent<LetterTile>().Setup(data);
            }

            // เอาช่องนี้ออกจากลิสต์ เพื่อไม่ให้ถูกแทนที่ซ้ำ
            filledSlots.RemoveAt(idx);
        }
    }

    // ================================================================================
}
