using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIMasterDraft : MonoBehaviour
{
    public static UIMasterDraft Instance { get; private set; }

    [Header("Root Panel")]
    [SerializeField] private GameObject rootPanel;

    [Header("Category Buttons (แยกตาม CardCategory)")]
    [SerializeField] private Button btnBuff;
    [SerializeField] private Button btnDispell;
    [SerializeField] private Button btnNeutral;
    // (เพิ่ม Button ของประเภทอื่นๆ ตามต้องการ)

    [Header("Pre-created Card Slots")]
    [Tooltip("ลาก GameObject ของ Slot UI ทั้งหมด (6 slot) มาใส่ใน List นี้ เรียงตามลำดับตำแหน่งที่ต้องการให้แสดง")]
    [SerializeField] private List<GameObject> cardSlotList = new List<GameObject>();

    [Header("Pagination Controls")]
    [SerializeField] private Button btnPrevPage;
    [SerializeField] private Button btnNextPage;
    [SerializeField] private TMP_Text pageIndicatorText;

    // ─── internal data ───
    private List<CardData> filteredCards = new List<CardData>();
    private int currentPage = 0;
    private const int cardsPerPage = 6;

    public delegate void OnCardSelectedDelegate(CardData card);
    private OnCardSelectedDelegate onCardSelectedCallback;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        rootPanel.SetActive(false);
    }

    void Start()
    {
        // ผูกปุ่ม Category และ Pagination
        btnBuff.onClick.AddListener(() => OnCategoryButtonClicked(CardCategory.Buff));
        btnDispell.onClick.AddListener(() => OnCategoryButtonClicked(CardCategory.Dispell));
        btnNeutral.onClick.AddListener(() => OnCategoryButtonClicked(CardCategory.Neutral));
        btnPrevPage.onClick.AddListener(OnPrevPage);
        btnNextPage.onClick.AddListener(OnNextPage);

        // ตรวจสอบว่าใน List cardSlotList มีจำนวนเท่ากับ cardsPerPage (6) หรือไม่
        if (cardSlotList.Count != cardsPerPage)
        {
            Debug.LogWarning($"[UIMasterDraft] ควรมี Slot UI ใน cardSlotList จำนวน {cardsPerPage} รายการ แต่พบ {cardSlotList.Count}");
        }
    }

    /// <summary>
    /// เปิด MasterDraft UI พร้อม callback เมื่อเลือกการ์ดเสร็จ
    /// </summary>
    public void Open(List<CardData> allCards, OnCardSelectedDelegate callback)
    {
        filteredCards = allCards
            .Where(cd => cd.category != CardCategory.Wildcard)
            .OrderBy(cd => cd.category)
            .ToList();

        onCardSelectedCallback = callback;
        currentPage = 0;
        rootPanel.SetActive(true);

        // เริ่มโดยเลือก Category แรก (Buff)
        OnCategoryButtonClicked(CardCategory.Buff);
    }

    /// <summary>
    /// เรียกเมื่อคลิกปุ่มหมวดหมู่การ์ด
    /// </summary>
    private void OnCategoryButtonClicked(CardCategory category)
    {
        filteredCards = CardManager.Instance.allCards
            .Where(cd => cd.category == category && cd.category != CardCategory.Wildcard)
            .ToList();

        currentPage = 0;
        RefreshCardGrid();
    }

    /// <summary>
    /// แสดงการ์ดในหน้า currentPage โดยใช้ cardSlotList ที่สร้างมาแล้ว
    /// </summary>
    private void RefreshCardGrid()
    {
        int totalPages = Mathf.CeilToInt((float)filteredCards.Count / cardsPerPage);
        totalPages = Mathf.Max(totalPages, 1);

        // Clamp currentPage ให้อยู่ใน [0, totalPages-1]
        currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);

        // คำนวณ index เริ่มต้นและจำนวนการ์ดที่จะแสดงในหน้านี้
        int startIdx = currentPage * cardsPerPage;
        int count = Mathf.Min(cardsPerPage, filteredCards.Count - startIdx);

        // วนผ่านแต่ละ slot ที่สร้างไว้ล่วงหน้า
        for (int slotIndex = 0; slotIndex < cardsPerPage; slotIndex++)
        {
            GameObject slotGO = cardSlotList[slotIndex];
            // ค้นหาองค์ประกอบย่อย (Icon, Name, Button) ภายใน Slot นี้
            var iconImg = slotGO.transform.Find("Icon").GetComponent<Image>();
            var nameTxt = slotGO.transform.Find("Name").GetComponent<TMP_Text>();
            var btn = slotGO.GetComponent<Button>();

            // ก่อนอื่น ลบ Listener เก่า
            btn.onClick.RemoveAllListeners();

            if (slotIndex < count)
            {
                // สร้างข้อมูลการ์ดจาก filteredCards
                var cd = filteredCards[startIdx + slotIndex];
                slotGO.SetActive(true);

                // เซ็ตค่า Icon และชื่อ
                iconImg.sprite = cd.icon;
                nameTxt.text = cd.displayName;

                // ผูก Callback ให้ปุ่มคลิกเลือกการ์ด
                btn.onClick.AddListener(() => OnCardSlotClicked(cd));
            }
            else
            {
                // ถ้า slotIndex เกินจำนวนการ์ดที่มี แสดงว่าให้ซ่อน Slot นี้
                slotGO.SetActive(false);
            }
        }

        // อัปเดตข้อความบอกหน้าปัจจุบัน
        pageIndicatorText.text = $"Page {currentPage + 1}/{totalPages}";

        // เปิด/ปิดปุ่ม Prev/Next ตามหน้า
        btnPrevPage.interactable = (currentPage > 0);
        btnNextPage.interactable = (currentPage < totalPages - 1);
    }

    private void OnPrevPage()
    {
        currentPage--;
        RefreshCardGrid();
    }

    private void OnNextPage()
    {
        currentPage++;
        RefreshCardGrid();
    }

    /// <summary>
    /// เรียกเมื่อผู้เล่นคลิกเลือกการ์ดใน slot ใด slot หนึ่ง
    /// </summary>
    private void OnCardSlotClicked(CardData cd)
    {
        rootPanel.SetActive(false);
        onCardSelectedCallback?.Invoke(cd);
    }

    /// <summary>
    /// ปิด MasterDraft UI (ถ้าต้องการเรียกกรณียกเลิก)
    /// </summary>
    public void Close()
    {
        rootPanel.SetActive(false);
    }
}
