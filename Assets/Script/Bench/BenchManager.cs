// BenchManager.cs
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

    /// <summary>เติมทุกช่องว่าง (initial)</summary>
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

}
