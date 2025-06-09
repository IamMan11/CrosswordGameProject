using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class SpaceManager : MonoBehaviour
{
    public static SpaceManager Instance { get; private set; }

    [Header("Slot Positions (10)")]
    public List<Transform> spaceSlots = new();

    [Header("Bench Slots")]
    public List<Transform> benchSlots = new();  // ลาก slotTransforms จาก BenchManager มาที่นี่

    [Header("Debug")]
    public bool debug = true;        // เปิด‑ปิด Console log ที่ Inspector
    [Header("Discard Button (ผูกใน Inspector)")]
    public Button discardButton;     // ← ปุ่ม Discard ที่จะลากมาผูก

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
    }
    private void Start()
    {
        // เริ่มต้นซ่อนปุ่ม
        if (discardButton != null)
        {
            discardButton.onClick.AddListener(DiscardAll);
            discardButton.gameObject.SetActive(false);
        }
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
                tile.AdjustSizeToParent();
                tile.IsInSpace = true;

                if (debug) Debug.Log($"[Space] add '{tile.GetData().letter}' to slot {i}");
                if (debug) Debug.Log($"[Space] Prepared count = {GetPreparedTiles().Count}");
                UpdateDiscardButton();
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
        tile.AdjustSizeToParent();
        tile.IsInSpace = false;

        tile.AdjustSizeToParent();

        if (debug) Debug.Log($"[Space] return '{tile.GetData().letter}' to Bench");
        UpdateDiscardButton();
    }
    public void RefreshDiscardButton()
    {
        discardButton?.gameObject.SetActive( GetPreparedTiles().Count > 0 );
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
    /// <summary>
    /// ฟังก์ชัน Discard: ลบตัวอักษรทั้งหมดใน Space และหักคะแนน 25%
    /// </summary>
    public void DiscardAll()
    {
        var tiles = GetPreparedTiles();
        if (tiles.Count == 0) return;

        int sumScores = 0;
        foreach (var tile in tiles)
            sumScores += tile.GetData().score;

        // หัก 25% ของคะแนนรวม (ปัดขึ้น)
        int penalty = Mathf.CeilToInt(sumScores * 0.25f);
        TurnManager.Instance.AddScore(-penalty);
        if (debug) Debug.Log($"[Space] Discard all – penalty: {penalty}");

        // ลบตัวอักษร: ทำลาย GameObject หรือคืน Bench ตามต้องการ
        foreach (var tile in tiles)
        {
            // 1. ถอดจาก parent (slot) ทันที
            tile.transform.SetParent(null);
            // 2. ทำลาย gameObject
            Destroy(tile.gameObject);
        }
        BenchManager.Instance.RefillEmptySlots();
        UpdateDiscardButton();
    }
    /// <summary>
    /// อัปเดตสถานะปุ่ม Discard: แสดงถ้ามีตัวอักษร ≥1 ใน Space, ซ่อนถ้าไม่มี
    /// </summary>
    private void UpdateDiscardButton()
    {
        if (discardButton == null) return;
        bool hasAny = GetPreparedTiles().Count > 0;
        discardButton.gameObject.SetActive(hasAny);
    }
}
