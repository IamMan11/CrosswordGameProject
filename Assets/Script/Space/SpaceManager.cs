using System.Collections.Generic;
using UnityEngine;

public class SpaceManager : MonoBehaviour
{
    public static SpaceManager Instance { get; private set; }

    [Header("Slot Positions (10)")]
    public List<Transform> spaceSlots = new();

    [Header("Bench Slots")]
    public List<Transform> benchSlots = new();  // ลาก slotTransforms จาก BenchManager มาที่นี่

    [Header("Debug")]
    public bool debug = true;        // เปิด‑ปิด Console log ที่ Inspector

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
    }

    /// <summary>
    /// เพิ่ม LetterTile เข้า Space (ซ้ายสุดที่ว่าง) — คืน true ถ้าสำเร็จ
    /// </summary>
    public bool AddTile(LetterTile tile)
    {
        for (int i = 0; i < spaceSlots.Count; i++)
        {
            Transform slot = spaceSlots[i];
            if (slot.childCount == 0)
            {
                tile.transform.SetParent(slot);
                tile.transform.localPosition = Vector3.zero;
                tile.IsInSpace = true;

                if (debug) Debug.Log($"[Space] add '{tile.GetData().letter}' to slot {i}");
                if (debug) Debug.Log($"[Space] Prepared count = {GetPreparedTiles().Count}");
                return true;
            }
        }

        if (debug) Debug.Log("❌ [Space] full, cannot add");
        return false;
    }
    

    /// <summary>
    /// นำ LetterTile ออกจาก Space แล้วคืนให้ Bench
    /// </summary>
    public void RemoveTile(LetterTile tile)
    {
        if (tile.isLocked) return;
        BenchSlot targetBench = BenchManager.Instance.GetFirstEmptySlot();
        if (targetBench == null)
        {
            if (debug) Debug.Log("❌ [Space] Bench full, cannot remove");
            return;
        }

        tile.transform.SetParent(targetBench.transform);
        tile.transform.localPosition = Vector3.zero;
        tile.IsInSpace = false;

        tile.AdjustSizeToParent(); 

        if (debug) Debug.Log($"[Space] return '{tile.GetData().letter}' to Bench");
    }

    /// <summary>
    /// ดึงรายการ LetterTile ที่เตรียมอยู่ใน Space ตามลำดับซ้าย→ขวา
    /// </summary>
    public List<LetterTile> GetPreparedTiles()
    {
        var list = new List<LetterTile>();
        foreach (var slot in spaceSlots)
            if (slot.childCount > 0)
                list.Add(slot.GetChild(0).GetComponent<LetterTile>());
        return list;
    }
    public List<LetterTile> GetAllBenchTiles()
    {
        var list = new List<LetterTile>();
        foreach (var slot in benchSlots)
            if (slot.childCount > 0)
                list.Add(slot.GetChild(0).GetComponent<LetterTile>());
        return list;
    }
}
