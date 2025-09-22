using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialConfig", menuName = "Tutorial/BalatroLikeConfig")]
public class TutorialConfigSO : ScriptableObject
{
    [Header("Run / Save")]
    [Tooltip("เล่นอัตโนมัติเมื่อเปิดเกมครั้งแรก (เซฟด้วย PlayerPrefs)")]
    public bool runOnFirstLaunch = true;

    [Tooltip("คีย์ PlayerPrefs สำหรับบันทึกว่าเคยดูทิวทอเรียลนี้แล้ว\n(ปล่อยว่าง = ให้ TutorialManager สร้างคีย์อัตโนมัติจากชื่อ asset)")]
    public string seenKey = "";   // ← เปลี่ยนจาก "TUTORIAL_SEEN" เป็นค่าว่าง

    [Header("ขั้นตอนทั้งหมด")]
    public List<TutorialStep> steps = new();
}

[Serializable]
public class TutorialStep
{
    [Header("หน้าการ์ด/ข้อความ")]
    public string id;
    public string title;
    [TextArea(2, 6)] public string body;
    public Sprite icon;

    [Header("พฤติกรรม")]
    [Tooltip("ให้การ์ดบังอินพุตทั้งหมดไหม (พื้นมืดกิน Raycast)")]
    public bool blockInput = true;

    [Tooltip("ให้ไปต่อเมื่อกดอะไรก็ได้ (หรือคลิก Next)")]
    public bool advanceOnAnyKey = true;

    [Tooltip("ถ้าติ๊ก จะไม่แสดงการ์ดทันที แต่รอ Event ที่กำหนด")]
    public bool waitForEvent = false;

    [Tooltip("Event ที่จะทำให้ขั้นนี้เริ่ม/ผ่าน")]
    public TutorialEvent trigger = TutorialEvent.None;

    [Header("โฟกัสเป้า")]
    [Tooltip("เลือกจุดที่จะเน้น (แผง/ปุ่มต่าง ๆ)")]
    public FocusKey focus = FocusKey.None;

    [Tooltip("ขอบรอบโฟกัส (พิกเซล)")]
    public Vector2 focusPadding = new Vector2(12, 12);

    [Header("สไตล์ภาพรวม (แบบ Balatro)")]
    public TutorialStyle style = new TutorialStyle();   // ← มีแค่ตัวเดียว

    [Header("Highlight (extra)")]
    [Tooltip("ไฮไลท์ ‘ช่องบนกระดาน’ 1 ช่อง")]
    public bool highlightOneCell = false;

    [Tooltip("ใช้พิกัด row/col แทนการเลือกตามชนิดช่องพิเศษ")]
    public bool highlightByCoordinate = false;

    [Tooltip("พิกัด row ของช่องที่จะไฮไลท์ (0 เริ่มบนซ้าย)")]
    public int highlightRow;

    [Tooltip("พิกัด col ของช่องที่จะไฮไลท์ (0 เริ่มบนซ้าย)")]
    public int highlightCol;

    [Tooltip("ชนิดช่องที่จะไฮไลท์ (ถ้าไม่ได้ใช้พิกัด)")]
    public SlotType highlightType = SlotType.DoubleWord;

    [Tooltip("ไฮไลท์ช่องการ์ดในมือ (CardSlot)")]
    public bool highlightCardSlot = false;

    [Tooltip("index ของ CardSlot ที่จะไฮไลท์ (0 คือช่องแรก)")]
    [Range(0, 7)] public int cardSlotIndex = 0;
}

[Serializable]
public class TutorialStyle
{
    [Tooltip("วางสแต็ก (Bubble→Card→Buttons) ที่กลางจอ หรือแปะกับจุดโฟกัส")]
    public AnchorMode anchor = AnchorMode.ScreenCenter;

    [Tooltip("เลื่อนตำแหน่งสแต็ก (พิกเซล)")]
    public Vector2 stackOffset = Vector2.zero;

    [Tooltip("ความทึบของพื้นมืดด้านหลัง (0–1)")]
    [Range(0f, 1f)] public float dimAlpha = 0.8f;

    [Tooltip("แสดงปุ่ม Skip (ขวากลางจอ)")]
    public bool showSkip = true;

    [Header("ข้อความปุ่ม (ปล่อยว่างเพื่อใช้ค่าเริ่มต้น)")]
    public string nextLabel = "Next";
    public string backLabel = "Back";
    public string skipLabel = "Skip  ›";

    [Header("การ์ด (ตรงกลางสแต็ก)")]
    [Tooltip("ข้อความแนวตั้งซ้ายของการ์ด")]
    public string cardLeftVertical = "J\nO\nK\nE\nR";

    [Tooltip("ข้อความแนวตั้งขวาของการ์ด")]
    public string cardRightVertical = "J\nO\nK\nE\nR";

    [Tooltip("เอียงการ์ดเล็กน้อย (องศา) เพื่อความมีชีวิตชีวา")]
    public float cardTiltDeg = 0f;
}

public enum AnchorMode { ScreenCenter, FocusTarget }

public enum FocusKey
{
    None = 0,
    Board,
    Bench,
    ConfirmButton,
    DictionaryButton,
    ManaUI,
    BagUI,
    CardSlot,

    // --------- Shop ---------
    ShopPanel,         // กรอบรวมของ panel ขาวกลางจอ
    ShopCoin,          // coin/wallet
    ShopBuyMana,       // ปุ่ม/บล็อค Max Mana
    ShopBuyTilePack,   // ปุ่ม/บล็อค Tile Back
    ShopBuyMaxCard,    // ปุ่ม/บล็อค Max Card
    ShopItemsRow,      // แถวไอเทมสุ่ม 3 ช่อง
    ShopItem1,         // ช่องไอเทม 1
    ShopItem2,         // ช่องไอเทม 2
    ShopItem3,         // ช่องไอเทม 3
    ShopReroll,        // ปุ่ม Reroll
    ShopNext           // ปุ่ม Next/ไปหน้าต่อไป
}
