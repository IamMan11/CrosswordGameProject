using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tutorial/Simple Sequence", fileName = "SimpleTutorialSequence")]
public class SimpleTutorialSequenceSO : ScriptableObject
{
    [Header("Run one-time")]
    public bool runOnFirstLaunch = true;
    public string seenPlayerPrefKey = "SIMPLE_TUTORIAL_SEEN_V1";

    public List<SimpleTutorialStep> steps = new List<SimpleTutorialStep>();
}

public enum SimpleTutStepType { Subtitle, SubtitleAndHighlight }
public enum SubtitleAnchorSlot { Default, AltA, AltB, ScreenCenter }
public enum SimpleTutAnchor
{
    None, ScreenCenter,
    // (ที่มีอยู่)
    Bench, Space, TilePack, Dictionary, Mana, CardSlots, SpecialTileFirst,
    Task, Confirm, Clear, Discard, Time, Score, Level,
    // ==== SHOP ====
    Coin,
    MaxManaUpgrade, MaxTilepackUpgrade, MaxCardslotUpgrade,
    Price_Mana, Price_Tilepack, Price_Cardslot,
    Progress_Mana, Progress_Tilepack, Progress_Cardslot,
    BuyCard_1, BuyCard_2, BuyCard_3,
    Reroll, Next, ManaInfo, CardSlotInfo, TilePackInfo,
    Price_All,
    Progress_All,
    BuyCard_All
}
public enum StepSpecialScope { Any, Bench, Space } // หาไทล์พิเศษจากที่ไหน


[Serializable]
public class SimpleTutorialStep
{
    public string id = "step";
    [TextArea] public string text;
    public SimpleTutStepType type = SimpleTutStepType.Subtitle;

    [Header("Subtitle placement")]
    public bool subtitleUseSlot = true;                 // ← ใช้สลอตคงที่เป็นค่าเริ่มต้น
    public SubtitleAnchorSlot subtitleSlot = SubtitleAnchorSlot.Default;
    public Vector2 subtitleOffset = new Vector2(0, -260);

    // (ยังเก็บฟิลด์เดิมไว้เพื่อความเข้ากันได้ หากอยากให้ซับไตเติลเกาะอ็อบเจ็กต์จริง ๆ)
    public SimpleTutAnchor subtitleAnchor = SimpleTutAnchor.ScreenCenter;

    [Header("Advance")]
    public bool clickAnywhereToAdvance = true;

    [Header("Highlight (used when type = SubtitleAndHighlight)")]
    public SimpleTutAnchor highlightTarget = SimpleTutAnchor.Bench;
    public float highlightPadding = 24f;
    public bool dimBackground = false;
    [Header("Special tile guard (optional)")]
    public bool waitForSpecialTile = false;                 // เปิดใช้เมื่อต้องรอให้มีไทล์พิเศษก่อนกดต่อ
    public StepSpecialScope specialWhere = StepSpecialScope.Any;  // หาในไหน
    // ==== NEW: คุมการแสดง Discard ใน step นี้ ====
    [Header("Discard (optional)")]
    public bool forceShowDiscardDuringStep = false;   // บังคับให้โชว์ Discard ระหว่าง step นี้
    public bool revertDiscardStateOnExit = true;      // ออกจาก step แล้วให้กลับสภาพเดิมไหม
}
