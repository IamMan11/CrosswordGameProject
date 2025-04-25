using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ใส่สคริปต์นี้ไว้บน Empty GameObject "BenchManager"
/// </summary>
public class BenchManager : MonoBehaviour
{
    [Header("Prefabs & Pool")]
    public GameObject letterTilePrefab;   // Prefab ของ LetterTile
    [Tooltip("**ไม่ใช้แล้ว** ให้กำหนดผ่าน TileBag")] 
    public List<LetterData> letterPool;   // ยังไม่ลบทิ้งเผื่ออ้างอิงอื่น

    [Header("Slot Positions (10)")]
    public List<Transform> slotTransforms = new();

    public static BenchManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start() => RefillEmptySlots();

    // ---------- เติมช่องว่าง ---------- //
    public void RefillEmptySlots()
    {
        foreach (Transform slot in slotTransforms)
        {
            if (slot.childCount > 0) continue;

            LetterData data = TileBag.Instance.DrawRandomTile();
            if (data == null)        // ถุงหมดแล้ว
            {
                Debug.Log("[Bench] TileBag empty");
                break;
            }

            GameObject tileGO = Instantiate(letterTilePrefab, slot);
            tileGO.transform.localPosition = Vector3.zero;

            LetterTile tile = tileGO.GetComponent<LetterTile>();
            tile.Setup(data);
        }
    }

    /// <summary>ปุ่มทดสอบใน Inspector – เคลียร์แล้วเติมใหม่</summary>
    [ContextMenu("Fill Bench")]
    private void FillBench()
    {
        foreach (Transform slot in slotTransforms)
            if (slot.childCount > 0) DestroyImmediate(slot.GetChild(0).gameObject);

        RefillEmptySlots();
    }

    /// <summary>คืน BenchSlot แรกที่ว่าง (ใช้โดย SpaceManager)</summary>
    public BenchSlot GetFirstEmptySlot()
    {
        foreach (Transform t in slotTransforms)
            if (t.childCount == 0) return t.GetComponent<BenchSlot>();
        return null;
    }
}
