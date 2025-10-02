using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

using Random = UnityEngine.Random;
/// <summary>
/// Level3Controller
/// - แยกกลไกด่าน 3 (Boss Hydra) ออกจาก LevelManager ให้เป็นคลาสเฉพาะ
/// - รับบทเป็น orchestrator: จัดการ HP/Phase Change/Conveyor/Lock Wave/Field Effects/Random Delete
/// - มี API ให้ LevelManager เรียก: Setup, Tick, OnPlayerDealtWord, StopAllLoops
/// </summary>
public class Level3Controller : MonoBehaviour
{
    public static Level3Controller Instance { get; private set; }

    [Header("Enable")]
    [Tooltip("เปิด/ปิดระบบบอสของด่าน 3")]
    public bool level3_enableBoss = true;

    [Header("L3 – Boss")]
    public int   level3_bossMaxHP = 300;
    [Tooltip("เงื่อนไข critical ตามความยาวคำหลัก")]
    public int   level3_criticalLength = 7;
    [Tooltip("โบนัสความเสียหายเมื่อ critical (เช่น 0.25 = +25%)")]
    [Range(0, 3f)] public float level3_criticalBonus = 0.5f;

    [Header("L3 – Conveyor Shuffle")]
    public float level3_conveyorIntervalSec = 65f;
    [Tooltip("จำนวนตำแหน่งที่จะเลื่อนแบบ conveyor (อย่างน้อย 1)")]
    public int   level3_conveyorShift = 1;

    [Header("L3 – Lock Board Wave")]
    public float level3_lockWaveIntervalSec = 90f;
    // ใช้แบบ “ล็อกเป็นเส้นยาว” แทนสุ่มจุดเดี่ยว
    [Min(1)] public int   level3_lockRunsPerWave = 2;   // จำนวนเส้นต่อคลื่น
    [Min(2)] public int   level3_lockRunLength   = 5;   // ความยาวของเส้นที่ล็อก (ช่องติดต่อกัน)
    public bool  level3_lockAllowHorizontal = true;     // อนุญาตแนวนอน
    public bool  level3_lockAllowVertical   = true;     // อนุญาตแนวตั้ง
    [Tooltip("ระยะเวลาล็อก (วินาที)")] 
    public float level3_lockDurationSec = 25f;

    // Safe Zone กลางบอร์ด (กว้าง x สูง) ห้ามล็อกทับ
    [Header("L3 – Lock Safe Zone (Board Center)")]
    [Min(0)] public int level3_lockSafeZoneWidth  = 3;
    [Min(0)] public int level3_lockSafeZoneHeight = 3;

    [Header("L3 – Field Effects")]
    public float level3_fieldEffectIntervalSec = 75f;
    public float level3_fieldEffectDurationSec = 30f;
    [Tooltip("ขนาดโซน (4x4 ตามสเป็ค)")]
    public int   level3_zoneSize = 4; // 4x4
    [Tooltip("สปอว์นพร้อมกันรอบละกี่โซนต่อประเภท")]
    public int   level3_zonesPerType = 1;

    [Tooltip("สีโซนบัฟ (x2) บนบอร์ดของ Level 3")]
    public Color level3_zoneBuffColor   = new Color(0.25f, 0.9f, 1f, 0.28f);
    [Tooltip("สีโซนดีบัฟ (-25%) บนบอร์ดของ Level 3")]
    public Color level3_zoneDebuffColor = new Color(1f, 0.45f, 0.1f, 0.28f);
    public bool randomDeletionsEnabled = true;
    
    [Header("L3 –Random Deletions")]
    public float level3_deleteActionIntervalSec = 35f;
    public int   level3_deleteBoardCount = 1;
    public float level3_deleteLettersCooldownSec = 0.4f;
    public int   level3_deleteBenchCount = 1;
    public float level3_cardSlotLockDurationSec = 2f;
    public float level3_deleteCardsCooldownSec = 0.6f;
    public float level3_deleteBenchCooldownSec = 0.4f;
    [Header("L3 – Phase Change")]
    [Tooltip("บอสหายตัวเมื่อ HP ≤ 50%")]
    [Range(0f,1f)] public float level3_phaseChangeHPPercent = 0.5f;
    [Tooltip("ช่วง vanish จะ +เวลา 7:30")]
    public float level3_phaseTimeBonusSec = 450f; // 7m30s
    [Tooltip("เมื่อ HP ≤ 25% บีบเวลาเหลือ 3 นาทีทันที")]
    [Range(0f,1f)] public float level3_sprintHPPercent = 0.25f;
    public float level3_sprintRemainingSec = 180f;

    [Header("L3 – UI (optional)")]
    public TMP_Text bossHpText; // ใส่ในอินสเปกเตอร์ถ้ามี
    // ===== Vanish (HP ≤ 50%) =====
    [SerializeField] private Color level3_lockOverlayColor = new Color(0f,0f,0f,0.55f);
    private bool vanishActive;          // กำลังอยู่ในช่วง HP ≤ 50%
    private int  vanishUnlockStage;     // 1=บอร์ด 1/3, 2=2/3, 3=3/3(จบ vanish)
    private int  trianglesCompleted;    // นับจำนวนครั้ง Triangle ครบตั้งแต่เข้า vanish
    public  bool IsVanishPhaseActive => vanishActive;

    // ให้ MoveValidator ใช้ถามว่าช่วงนี้ “วางอิสระ” ไหม
    public  bool IsFreePlacementPhase() => vanishActive;
    public enum VanishBandOrientation { Vertical, Horizontal }

    [Header("L3 – Vanish Lock Bands")]
    [SerializeField] private VanishBandOrientation vanishLockOrientation = VanishBandOrientation.Vertical;
    [SerializeField] private bool vanishExpandRightOrBottomFirst = true; 
    [Header("Damage Pop (ScorePopUI)")]
    public ScorePopUI damagePopPrefab;          // drag prefab เดียวกับ TurnManager.scorePopPrefab ได้
    public RectTransform damageStartAnchor;     // จุดเริ่มดาเมจป็อปของ L3 (เช่น กลางบอร์ด/ใกล้บอส
    [Header("L3 – Boss Visuals")]
    public Image  bossImage;          // Drag รูป UI ของบอส (Image ใต้ Canvas)
    public Sprite bossIdle;           // ท่าปกติ
    public Sprite bossCast;           // ท่าเตรียมใช้เอฟเฟกต์
    public Sprite bossHurt;           // ท่าโดนดาเมจ

    [Tooltip("เวลาค้างท่า Hurt ก่อนกลับ Idle")]
    [SerializeField] float bossHurtHoldSec = 0.5f;

    [Tooltip("เวลาค้างท่า Cast ก่อนเริ่มเอฟเฟกต์")]
    [SerializeField] float bossCastTelegraphSec = 0.6f;

    private enum BossPose { Idle, Casting, Hurt }
    private BossPose _pose = BossPose.Idle;
    private Coroutine _poseCo;

    // --------- internals ---------
    private int hp;
    private bool phaseChangeActive;
    private bool phaseTriggered;
    private bool sprintTriggered;

    private Coroutine coConveyor;
    private Coroutine coLockWave;
    private Coroutine coField;
    private Coroutine coDelete;
    Coroutine coRandomDeletes;

    private readonly List<BoardSlot> lockedByBoss = new List<BoardSlot>();

    private struct L3Zone { public RectInt rect; public bool isBuff; public float end; }
    private readonly List<L3Zone> activeZones = new List<L3Zone>();

    // puzzle ระหว่าง vanish (1/3 → 2/3 → 3/3)
    private int        puzzleStage;        // 0=off, 1..3 active
    private Vector2Int puzzleA, puzzleB;
    public  float      puzzleCheckPeriod = 0.5f;
    private float      puzzleCheckTimer  = 0f;

    // คูลดาวน์สุ่มลบ
    private float nextLetterDeleteTime = 0f;
    private float nextCardDeleteTime   = 0f;
    private float nextBoardDeleteTime = 0f;
    private float nextBenchDeleteTime = 0f;
    private bool effectsArmed = false;

    // === Events (relay ไป UI อื่น ถ้าเกมคุณมี) ===
    public event Action<int> OnBossDeleteBenchRequest;
    public event Action         OnBossDeleteRandomCardRequest;
    public event Action<float>  OnBossLockCardSlotRequest;

    // =========================================================

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SetBossUIVisible(false);
    }

    // ---------------------------------------------------------
    // Public API for LevelManager
    // ---------------------------------------------------------
    public  void ArmEffects()
    {
        if (effectsArmed) return;
        effectsArmed = true;

        if (!level3_enableBoss) return;
        if (coConveyor == null) coConveyor = StartCoroutine(ConveyorLoop());
        if (coLockWave == null) coLockWave = StartCoroutine(LockWaveLoop());
        if (coField   == null) coField    = StartCoroutine(FieldEffectsLoop());
        if (coDelete  == null) coDelete   = StartCoroutine(DeleteActionLoop());
    }

    /// <summary>เรียกตอนเริ่ม Level 3</summary>
    public void Setup()
    {
        // ธีม/อัปเกรด (เหมือนของเดิมใน LevelManager)
        Debug.Log("[Level3] Apply theme: black-purple background, Hydra behind board.");
        var prog = PlayerProgressSO.Instance?.data;
        if (prog != null) TurnManager.Instance?.UpgradeMaxMana(prog.maxMana);

        SetBossPose(BossPose.Idle, 0f);
        hp = Mathf.Max(1, level3_bossMaxHP);
        phaseChangeActive = false;
        phaseTriggered = false;
        sprintTriggered = false;
        ClearZonesAndLocks();
        UpdateBossUI();

        if (level3_enableBoss)
        {
            effectsArmed = false;
        }
    }

    /// <summary>เรียกทุกเฟรมจาก LevelManager เฉพาะตอนอยู่ด่าน 3</summary>
    public void Tick(float unscaledDeltaTime)
    {
        if (!level3_enableBoss) return;
        if (vanishActive)
        {
            // โพลล์ทุก ๆ puzzleCheckPeriod
            puzzleCheckTimer += unscaledDeltaTime;
            if (puzzleCheckTimer >= Mathf.Max(0.1f, puzzleCheckPeriod))
            {
                puzzleCheckTimer = 0f;

                var l2 = Level2Controller.Instance;
                if (l2 != null)
                {
                    // อัปเดตสีโหนด: จะทาสี "เขียว" ให้โหนดที่แตะตามกติกา (ทิศใดทิศหนึ่งรอบจุด)
                    l2.UpdateTriangleColors(l2.L2_triangleIdleColor, l2.L2_triangleLinkedColor);

                    // นับจำนวนโหนดที่แตะแล้ว (0..3)
                    int touched = l2.GetTouchedNodeCount();

                    // ถ้าครบทั้ง 3 โหนด → ถือว่า "Triangle สำเร็จ 1 ครั้ง"
                    if (touched >= 3)
                    {
                        trianglesCompleted++;

                        // ✅ เคลียร์ตัวอักษรทั้งบอร์ดทุกครั้งที่ปลดล็อกเพิ่ม (ทั้งตอนเป็น 2/3 และ 3/3)
                        BoardManager.Instance?.CleanSlate();

                        if (trianglesCompleted == 1)
                        {
                            vanishUnlockStage = 2;                // 2/3
                            ApplyLockedBoardFraction(2f / 3f);

                            // สุ่มโหนดใหม่เฉพาะโซนที่ไม่ล็อก (หลังเคลียร์บอร์ดแล้ว)
                            RegenerateTriangleForLevel3();

                            UIManager.Instance?.ShowMessage("Unlock +1/3 (2/3 open)", 1.6f);
                        }
                        else if (trianglesCompleted >= 2)
                        {
                            vanishUnlockStage = 3;                // 3/3
                            ApplyLockedBoardFraction(1f);         // เปิดทั้งหมด

                            // จบพัซเซิล: ไม่ต้องมี Triangle ต่อแล้ว
                            TeardownTriangleForLevel3();

                            UIManager.Instance?.ShowMessage("Board fully unlocked! Hydra reappears!", 2.0f);

                            // ออกจาก vanish: บอสกลับมาโดนดาเมจ & เอฟเฟกต์กลับมาทำงาน
                            vanishActive = false;
                            ReArmEffects();
                        }
                    }
                }
            }

            // ระหว่าง vanish ไม่ต้องทำอย่างอื่นของ L3
            return;
        }

    }
    public void SetBossUIVisible(bool on)
    {
        if (bossImage)
            bossImage.gameObject.SetActive(on && bossImage.sprite != null);

        if (bossHpText)
            bossHpText.gameObject.SetActive(on);
    }
    public int GetBossHP() => Mathf.Max(0, hp);
    public bool IsBossDefeated() => level3_enableBoss && GetBossHP() <= 0;
    public void DealBossDamage(int amount)
    {
        if (!level3_enableBoss) return;
        if (vanishActive) return; // อยู่ช่วง puzzle/vanish บอสอมตะ

        hp = Mathf.Max(0, hp - Mathf.Abs(amount));
        UpdateBossUI();

        if (hp <= 0)
        {
            // หยุดเอฟเฟกต์และลูปทั้งหมดของ L3 เพื่อไม่ให้ไปรบกวนตอนเคลียร์
            StopAllLoops();

            // แจ้ง LevelManager ให้รีเช็กเงื่อนไขชนะ (เผื่อจบระหว่างคิดคะแนน)
            LevelManager.Instance?.OnScoreOrWordProgressChanged();
        }
    }
    private void SetBossPose(BossPose p, float hold = 0f)
    {
        _pose = p;
        if (bossImage)
        {
            switch (p)
            {
                case BossPose.Casting: bossImage.sprite = bossCast ? bossCast : bossIdle; break;
                case BossPose.Hurt:    bossImage.sprite = bossHurt ? bossHurt : bossIdle; break;
                default:               bossImage.sprite = bossIdle; break;
            }
            bossImage.enabled = bossImage.sprite != null;
            // ถ้าอยากให้ขนาดพอดีรูป: bossImage.SetNativeSize();
        }

        if (_poseCo != null) { StopCoroutine(_poseCo); _poseCo = null; }
        if (hold > 0f) _poseCo = StartCoroutine(RevertPoseAfter(hold));
    }
    private IEnumerator RevertPoseAfter(float t)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, t));
        _poseCo = null;
        SetBossPose(BossPose.Idle, 0f);
    }
    // Level3Controller.cs (ภายในคลาส)
    public bool IsBossDamageable() => !vanishActive;

    public void ApplyBossDamage(int amount)
    {
        if (!level3_enableBoss) return;
        SetBossPose(BossPose.Hurt, bossHurtHoldSec);
        if (vanishActive) return;                 // ช่วง puzzle: ไม่รับดาเมจ
        amount = Mathf.Max(0, amount);
        if (amount == 0) return;

        hp = Mathf.Max(0, hp - amount);
        UpdateBossUI();

        if (hp <= 0)
        {
            hp = 0;
            OnBossDefeated();
        }
    }

    private void OnBossDefeated()
    {
        SetBossPose(BossPose.Hurt, 999f);
        // หยุดเอฟเฟกต์/ลูประดับด่าน 3 ให้เรียบร้อย
        StopAllLoops();            // ถ้ามีอยู่แล้วในคลาส ให้เรียกตัวนี้
        ClearZonesAndLocks();      // ล้างโซน/ปลดล็อกทั้งหมดให้สะอาด

        // ขอขึ้น Stage Clear “เดี๋ยวนี้” (กันติดอนิเมชันคิดคะแนนค้าง)
        LevelManager.Instance?.TriggerStageClearNow();
    }

    /// <summary>
    /// ปรับสถานะล็อกของบอร์ดแบบ “เป็นแถบทั้งแถว/คอลัมน์” ตามสัดส่วนที่เปิด (1/3, 2/3, 3/3)
    /// - เปิด 1/3  : เปิดเฉพาะ “แถบกลาง”
    /// - เปิด 2/3  : เปิดแถบกลาง + อีกฝั่งหนึ่ง (เลือกขวา/ล่าง หรือ ซ้าย/บน จาก vanishExpandRightOrBottomFirst)
    /// - เปิด 3/3  : เปิดทั้งหมด
    /// </summary>
    private void ApplyLockedBoardFraction(float unlockedFraction)
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return;

        // map 0..1 → 1..3 ส่วนที่ "เปิด"
        int openThirds = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(unlockedFraction) * 3f), 1, 3);

        int rows = bm.rows, cols = bm.cols;

        // helper: หั่นแกน (ตาม orientation) ออกเป็น 3 ส่วนใกล้เคียงกัน
        // คืนค่าเป็นช่วง [inclusiveStart, inclusiveEnd] ของดัชนีในแกนที่เลือก
        (Vector2Int mid, Vector2Int leftOrTop, Vector2Int rightOrBottom) = SliceIntoThirds(
            total: (vanishLockOrientation == VanishBandOrientation.Vertical) ? cols : rows
        );

        bool OpenBandIndex(int index)
        {
            // index: 0 = left/top, 1 = mid, 2 = right/bottom
            if (openThirds >= 3) return true;          // เปิดหมด
            if (openThirds == 1) return index == 1;    // เปิดเฉพาะกลาง

            // openThirds == 2 → กลาง + อีกด้านหนึ่ง
            if (index == 1) return true;
            if (vanishExpandRightOrBottomFirst) return index == 2; // กลาง + ขวา/ล่าง
            else return index == 0; // กลาง + ซ้าย/บน
        }

        // เคลียร์สถานะเดิมทั้งหมดด้วยการตั้งค่าล็อกใหม่
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var s = bm.grid[r, c];
                if (!s) continue;

                int bandIndex; // 0/1/2
                if (vanishLockOrientation == VanishBandOrientation.Vertical)
                {
                    bandIndex = (c <= leftOrTop.y) ? 0 : (c < mid.x ? 0 : (c <= mid.y ? 1 : (c <= rightOrBottom.y ? 2 : 2)));
                    // อธิบาย: ใช้ช่วง left/top, mid, right/bottom ที่ SliceIntoThirds คืนมา
                    if (c >= mid.x && c <= mid.y) bandIndex = 1;
                    else if (c >= rightOrBottom.x && c <= rightOrBottom.y) bandIndex = 2;
                    else bandIndex = 0;
                }
                else
                {
                    bandIndex = (r <= leftOrTop.y) ? 0 : (r < mid.x ? 0 : (r <= mid.y ? 1 : (r <= rightOrBottom.y ? 2 : 2)));
                    if (r >= mid.x && r <= mid.y) bandIndex = 1;
                    else if (r >= rightOrBottom.x && r <= rightOrBottom.y) bandIndex = 2;
                    else bandIndex = 0;
                }

                bool open = OpenBandIndex(bandIndex);
                s.SetLockedVisual(!open, level3_lockOverlayColor);
            }
    }

    /// <summary>ตัดแกนจำนวน total ออกเป็น 3 ส่วน: ซ้าย/บน, กลาง, ขวา/ล่าง (คืนช่วง index แบบ inclusive)</summary>
    private static (Vector2Int mid, Vector2Int leftOrTop, Vector2Int rightOrBottom) SliceIntoThirds(int total)
    {
        total = Mathf.Max(1, total);
        int baseSize = total / 3;
        int rem = total % 3;

        // กระจายเศษให้ส่วนกลางก่อน จากนั้นค่อยขวา/ล่าง เพื่อให้แถบกลาง “กว้างกว่านิดหน่อย” ถ้าหารไม่ลงตัว
        int sizeLeftTop = baseSize;
        int sizeMid = baseSize + (rem > 0 ? 1 : 0);
        int sizeRightBottom = baseSize + (rem > 1 ? 1 : 0);

        int a0 = 0;
        int a1 = sizeLeftTop - 1;

        int b0 = a1 + 1;
        int b1 = b0 + sizeMid - 1;

        int c0 = b1 + 1;
        int c1 = total - 1;

        // คืนเป็น (mid, left/top, right/bottom)
        return (new Vector2Int(b0, b1), new Vector2Int(a0, a1), new Vector2Int(c0, c1));
    }

    // Fisher–Yates
    private static void Shuffle<T>(IList<T> a)
    {
        for (int i = a.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
    }

    /// <summary>เปิดลูป/เอฟเฟกต์ของด่าน 3 กลับมาหลังจบ vanish</summary>
    private void ReArmEffects()
    {
        if (!level3_enableBoss) return;
        if (coConveyor == null) coConveyor = StartCoroutine(ConveyorLoop());
        if (coLockWave == null) coLockWave = StartCoroutine(LockWaveLoop());
        if (coField   == null) coField    = StartCoroutine(FieldEffectsLoop());
        if (coDelete  == null) coDelete   = StartCoroutine(DeleteActionLoop());
    }

    /// <summary>เตรียม Triangle สำหรับ Level 3: โหนดต้องอยู่ใน “ส่วนที่ไม่ถูกล็อก”</summary>
    private void SetupTriangleForLevel3()
    {
        var l2 = Level2Controller.Instance;
        var bm = BoardManager.Instance;
        if (l2 == null || bm == null || bm.grid == null) return;

        // ใช้สี/ขนาดเดียวกับ L2
        l2.L2_useTriangleObjective = true;

        // สร้างโหนดใหม่ให้สุ่มใน “ช่องที่ไม่ล็อก”
        // เราเรียกเมธอดสาธารณะ (คุณอาจต้องเปิด public ให้ฟังก์ชัน generate/paint ถ้าเป็น private)
        l2.GenerateTriangleNodesForExternalUse(
            nodeSize: l2.L2_triangleNodeSize,
            minManhattan: l2.L2_triangleMinManhattan,
            filter: (r,c) => { var s = bm.grid[r,c]; return s != null && !s.IsLocked; });

        l2.PaintTriangleNodesIdle(l2.L2_triangleIdleColor);
        l2.UpdateTriangleColors(l2.L2_triangleIdleColor, l2.L2_triangleLinkedColor);
    }

    /// <summary>หลัง Triangle ครบ ให้สุ่มโหนดใหม่ (ต้องยังอยู่ในส่วนที่ไม่ล็อก)</summary>
    private void RegenerateTriangleForLevel3()
    {
        var l2 = Level2Controller.Instance;
        var bm = BoardManager.Instance;
        if (l2 == null || bm == null || bm.grid == null) return;

        l2.GenerateTriangleNodesForExternalUse(
            nodeSize: l2.L2_triangleNodeSize,
            minManhattan: l2.L2_triangleMinManhattan,
            filter: (r,c) => { var s = bm.grid[r,c]; return s != null && !s.IsLocked; });

        l2.PaintTriangleNodesIdle(l2.L2_triangleIdleColor);
        l2.UpdateTriangleColors(l2.L2_triangleIdleColor, l2.L2_triangleLinkedColor);
    }

    private void TeardownTriangleForLevel3()
    {
        var l2 = Level2Controller.Instance;
        if (l2 == null) return;
        l2.ClearTriangleForExternalUse();
    }

    // ===== ลบจาก Bench เท่านั้น แล้ว REFILL ทันทีตามจำนวนที่ลบ =====
    private int DeleteFromHandAndRefill(int wantCount)
    {
        int removed = 0;
        var bm = BenchManager.Instance;
        if (bm == null) return 0;

        // เลือกเฉพาะไทล์บน Bench
        var candidates = new List<LetterTile>();
        foreach (var t in bm.GetAllBenchTiles())
            if (t) candidates.Add(t);

        if (candidates.Count == 0) return 0;

        // ลบสูงสุดเท่าที่มีและไม่เกิน wantCount
        int toDelete = Mathf.Min(wantCount, candidates.Count);

        for (int i = 0; i < toDelete; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            var tile = candidates[idx];
            candidates.RemoveAt(idx);
            if (!tile) continue;

            // เอฟเฟกต์แล้วลบ
            var anim = tile.GetComponentInChildren<Animator>() ?? tile.GetComponent<Animator>();
            if (anim)
            {
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                TileAnimatorBinder.Trigger(anim, "Discard");
            }

            // ✅ ทำให้ช่องว่างทันที เพื่อไม่ให้ถูกนับว่า "มือยังเต็ม"
            BenchSlot preferSlot = null;
            if (tile && tile.transform && tile.transform.parent)
            {
                var parent = tile.transform.parent;
                preferSlot = parent.GetComponent<BenchSlot>(); // แปลงเป็น BenchSlot ให้ถูกชนิด
                tile.transform.SetParent(null, false);         // ปลดออกจากช่อง → ช่องนี้ว่างแล้วเดี๋ยวนี้
            }

            // ทำลาย tile หน่วงสั้นๆให้เห็นอนิเมชัน
            Destroy(tile.gameObject, 0.1f);
            removed++;

            // เติมกลับ 1 ตัวต่อ 1 ที่ลบ โดย "พยายามลงช่องเดิม"
            bm.RefillOneSlot(prefer: preferSlot, forceImmediate: false);
        }

        if (removed > 0)
            UIManager.Instance?.ShowMessage($"Hydra deletes {removed} from bench!", 1.2f);

        return removed;
    }
    private bool IsInSafeZone(int r, int c)
    {
        var bm = BoardManager.Instance; if (bm == null) return false;
        int rows = bm.rows, cols = bm.cols;
        int w = Mathf.Clamp(level3_lockSafeZoneWidth, 0, cols);
        int h = Mathf.Clamp(level3_lockSafeZoneHeight, 0, rows);
        if (w == 0 || h == 0) return false; // ไม่มี safe zone

        // คำนวณสี่เหลี่ยมตรงกลางบอร์ด
        int x0 = (cols - w) / 2;
        int y0 = (rows - h) / 2;
        int x1 = x0 + w - 1;
        int y1 = y0 + h - 1;

        return (c >= x0 && c <= x1 && r >= y0 && r <= y1);
    }

    /// <summary>
    /// พยายามสุ่ม “เส้นยาว” ตามแนวที่กำหนด (แนวนอน/แนวตั้ง)
    /// โดยทุกช่องในเส้นต้อง: ไม่ล็อก, ไม่มีตัวอักษร, ไม่อยู่ใน safe zone, ไม่ใช่ triangle node
    /// </summary>
    private List<BoardSlot> TryPickLockRun(bool horizontal, int length, HashSet<Vector2Int> banned, int maxAttempts = 60)
    {
        var bm = BoardManager.Instance;
        var picked = new List<BoardSlot>();
        if (bm == null || bm.grid == null || length < 2) return picked;

        int rows = bm.rows, cols = bm.cols;
        int tries = 0;

        while (tries++ < maxAttempts)
        {
            if (horizontal)
            {
                int r = UnityEngine.Random.Range(0, rows);
                int c0 = UnityEngine.Random.Range(0, Mathf.Max(1, cols - length + 1));
                picked.Clear();
                bool ok = true;

                for (int k = 0; k < length; k++)
                {
                    int c = c0 + k;
                    var s = bm.grid[r, c];
                    if (!s) { ok = false; break; }
                    if (s.IsLocked || s.HasLetterTile() || IsInSafeZone(r,c) || Level2Controller.IsTriangleCell(r,c)) { ok = false; break; }
                    if (banned != null && banned.Contains(new Vector2Int(r,c))) { ok = false; break; }
                    picked.Add(s);
                }
                if (ok) return new List<BoardSlot>(picked);
            }
            else
            {
                int c = UnityEngine.Random.Range(0, cols);
                int r0 = UnityEngine.Random.Range(0, Mathf.Max(1, rows - length + 1));
                picked.Clear();
                bool ok = true;

                for (int k = 0; k < length; k++)
                {
                    int r = r0 + k;
                    var s = bm.grid[r, c];
                    if (!s) { ok = false; break; }
                    if (s.IsLocked || s.HasLetterTile() || IsInSafeZone(r,c) || Level2Controller.IsTriangleCell(r,c)) { ok = false; break; }
                    if (banned != null && banned.Contains(new Vector2Int(r,c))) { ok = false; break; }
                    picked.Add(s);
                }
                if (ok) return new List<BoardSlot>(picked);
            }
        }
        return new List<BoardSlot>();
    }

    private IEnumerable<Vector2Int> Neighbors8(int r, int c, int rows, int cols)
    {
        for (int dr = -1; dr <= 1; dr++)
        for (int dc = -1; dc <= 1; dc++)
        {
            if (dr == 0 && dc == 0) continue;
            int nr = r + dr, nc = c + dc;
            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                yield return new Vector2Int(nr, nc);
        }
    }

    private Vector2Int FindCoords(BoardSlot s)
    {
        var bm = BoardManager.Instance; if (bm == null || bm.grid == null) return new Vector2Int(-1,-1);
        for (int r = 0; r < bm.rows; r++)
            for (int c = 0; c < bm.cols; c++)
                if (bm.grid[r,c] == s) return new Vector2Int(r,c);
        return new Vector2Int(-1,-1);
    }

    private HashSet<Vector2Int> BuildBannedSetFromBoardAndPicked(List<BoardSlot> pickedSoFar)
    {
        var bm = BoardManager.Instance; var banned = new HashSet<Vector2Int>();
        if (bm == null || bm.grid == null) return banned;

        // ช่องที่ล็อกอยู่แล้ว + ฮาโล 8 ทิศ
        for (int r = 0; r < bm.rows; r++)
            for (int c = 0; c < bm.cols; c++)
            {
                var s = bm.grid[r,c];
                if (s != null && s.IsLocked)
                {
                    banned.Add(new Vector2Int(r,c));
                    foreach (var nb in Neighbors8(r,c,bm.rows,bm.cols)) banned.Add(nb);
                }
            }

        // ช่องที่เลือกล็อกไปแล้วในคลื่นนี้ + ฮาโล 8 ทิศ
        foreach (var s in pickedSoFar)
        {
            var p = FindCoords(s);
            if (p.x < 0) continue;
            banned.Add(p);
            foreach (var nb in Neighbors8(p.x,p.y,bm.rows,bm.cols)) banned.Add(nb);
        }

        return banned;
    }
    private static RectInt Inflate(RectInt r, int pad)
    {
        return new RectInt(r.x - pad, r.y - pad, r.width + pad * 2, r.height + pad * 2);
    }
    private static bool RectOverlap(RectInt a, RectInt b)
    {
        // overlap/แตะกันเมื่อช่วงแกน X,Y ซ้อนหรือสัมผัสกัน
        bool x = a.xMin <= b.xMax && a.xMax >= b.xMin;
        bool y = a.yMin <= b.yMax && a.yMax >= b.yMin;
        return x && y;
    }
    /// <summary>
    /// ให้ LevelManager เรียกเมื่อผู้เล่นยืนยันคำ (ย้ายมาจาก LevelManager.Level3_OnPlayerDealtWord)
    /// </summary>
    public void OnPlayerDealtWord(int placedCount, int placedLettersDamageSum, int mainWordLen, List<Vector2Int> placedCoords)
    {
        if (!level3_enableBoss) return;
        var cfg = LevelManager.Instance?.currentLevelConfig;
        if (cfg == null || cfg.levelIndex != 3) return;
        if (LevelManager.Instance.phase != LevelManager.GamePhase.Running) return;
        // ⛔ กันดาเมจถ้าอยู่ในช่วง Puzzle/Vanish
        if (vanishActive || phaseChangeActive) 
        {
            // (ถ้าต้องการแจ้งเตือน UI เพิ่มบรรทัดนี้)
            // UIManager.Instance?.ShowMessage("Hydra is invulnerable during the puzzle!", 1.2f);
            return;
        }

        int sum = Mathf.Max(0, placedLettersDamageSum);
        if (sum <= 0 || placedCount <= 0) return;

        // best-of-N roll
        int draws = Mathf.Max(1, placedCount);
        int best = 0;
        for (int i = 0; i < draws; i++)
        {
            int roll = UnityEngine.Random.Range(1, sum + 1); // inclusive
            if (roll > best) best = roll;
        }
        float dmg = best;

        // Field Effects
        bool hitBuff = false, hitDebuff = false;
        if (placedCoords != null && placedCoords.Count > 0 && activeZones.Count > 0)
        {
            foreach (var z in activeZones)
            {
                if (Time.unscaledTime > z.end) continue;
                foreach (var p in placedCoords)
                {
                    if (z.rect.Contains(new Vector2Int(p.x, p.y)))
                    {
                        if (z.isBuff) hitBuff = true; else hitDebuff = true;
                    }
                }
            }
        }
        if (hitBuff) dmg *= 2f;
        if (hitDebuff) dmg *= 0.75f;

        // Critical
        if (mainWordLen >= level3_criticalLength)
            dmg *= (1f + Mathf.Max(0f, level3_criticalBonus));

        // ... คำนวณ final เสร็จแล้ว ...
        int final = Mathf.Max(0, Mathf.RoundToInt(dmg));
        if (final <= 0) return;

        // ==== ใช้ ScorePopUI ====
        if (damagePopPrefab != null)
        {
            // สร้างใกล้ anchor (ถ้าไม่ตั้ง ให้พยายามใช้ของ TurnManager เป็น fallback)
            var start = damageStartAnchor 
                ?? TurnManager.Instance?.anchorTotal 
                ?? (TurnManager.Instance?.scoreHud);

            if (start != null)
            {
                var pop = Instantiate(damagePopPrefab, start);
                pop.transform.localScale = Vector3.one;
                pop.SetText("-" + final);
                pop.SetColor(pop.colorTotal);      // จะสลับเป็นแดงเองได้ถ้าต้องการ
                pop.PopByDelta(final, 
                    TurnManager.Instance ? TurnManager.Instance.tier2Min : 3,
                    TurnManager.Instance ? TurnManager.Instance.tier3Min : 6);

                RectTransform hpTarget = bossHpText ? bossHpText.rectTransform : (TurnManager.Instance?.scoreHud);
                if (hpTarget != null)
                    StartCoroutine(pop.FlyTo(hpTarget, TurnManager.Instance ? TurnManager.Instance.flyDur : 0.6f));
            }
        }
        else
        {
            // ไม่มี prefab → ใช้ข้อความเดิมเป็น fallback
            UIManager.Instance?.ShowMessage($"🗡 Hydra -{final}", 1.5f);
        }

        // หักเลือดจริง
        ApplyBossDamage(final);

        // ถ้าตายแล้วก็จบ
        if (GetBossHP() <= 0) return;

        // ยังไม่ตาย → เช็กเฟส/สปรินต์ต่อได้ตามเดิม
        int hp50 = Mathf.CeilToInt(level3_bossMaxHP * level3_phaseChangeHPPercent);
        int hp25 = Mathf.CeilToInt(level3_bossMaxHP * level3_sprintHPPercent);

        if (!phaseTriggered && GetBossHP() <= hp50)
        {
            phaseTriggered = true;
            StartCoroutine(StartVanishPhase());
        }
        if (!sprintTriggered && GetBossHP() <= hp25)
        {
            sprintTriggered = true;
            LevelManager.Instance.levelTimeLimit =
                LevelManager.Instance.levelTimeElapsed + Mathf.Max(0f, level3_sprintRemainingSec);
            LevelManager.Instance.UpdateLevelTimerText(level3_sprintRemainingSec);
            UIManager.Instance?.ShowMessage("⏱ Hydra enraged: time set to 3:00!", 2.5f);
        }
    }
    private IEnumerator StartVanishPhase()
    {
        if (vanishActive) yield break;

        // + เวลา 7:30 ตามสเปค
        if (level3_phaseTimeBonusSec > 0f)
        {
            LevelManager.Instance.levelTimeLimit += level3_phaseTimeBonusSec;
            LevelManager.Instance.UpdateLevelTimerText(
                Mathf.Max(0f, LevelManager.Instance.levelTimeLimit - LevelManager.Instance.levelTimeElapsed));
        }

        // ปิดระบบเอฟเฟกต์ทั้งหมดของด่าน 3 (หยุด loop) ระหว่าง vanish
        StopAllLoops();

        // บล็อกการลบสุ่ม/ล็อกเวฟ/โซน ฯลฯ
        vanishActive       = true;
        vanishUnlockStage  = 1;
        trianglesCompleted = 0;

        UIManager.Instance?.ShowMessage("Hydra vanished! Unlock the board by completing Triangles.", 2.5f);

        // เคลียร์ตัวอักษรบนบอร์ดทั้งหมด
        BoardManager.Instance?.CleanSlate();

        // ล็อกบอร์ดให้เหลือ “ปลดล็อกได้” เพียง 1/3
        ApplyLockedBoardFraction(unlockedFraction: 1f/3f);

        // เตรียม Triangle แบบ Level 2 (ใช้โหนดในส่วนที่ “ไม่โดนล็อก”)
        SetupTriangleForLevel3();

        yield break;
    }

    public void StopAllLoops()
    {
        SetBossPose(BossPose.Idle, 0f);
        if (coConveyor != null) { StopCoroutine(coConveyor); coConveyor = null; }
        if (coLockWave != null) { StopCoroutine(coLockWave); coLockWave = null; }
        if (coField   != null) { StopCoroutine(coField);     coField    = null; }
        if (coDelete  != null) { StopCoroutine(coDelete);    coDelete   = null; }
        ClearZonesAndLocks();
    }

    // ---------------------------------------------------------
    // Internals (เดิมย้ายออกมาจาก LevelManager)
    // ---------------------------------------------------------

    private void UpdateBossUI()
    {
        if (bossHpText)
        bossHpText.text = $"Hydra HP: {Mathf.Max(0, hp)}/{level3_bossMaxHP}";
    }

    private IEnumerator StartPhaseChange()
    {
        if (phaseChangeActive) yield break;

        // หยุดคลื่น/โซน/ลบต่าง ๆ ชั่วคราว
        StopAllLoops();
        ClearZonesAndLocks();

        phaseChangeActive = true;
        puzzleStage = 1;

        // + เวลา 7:30 (เพราะ levelTimeLimit = เวลาสูงสุดนับจากเริ่ม)
        LevelManager.Instance.levelTimeLimit += Mathf.Max(0f, level3_phaseTimeBonusSec);

        // reset board → เหลือเติม 1/3
        ResetBoardToFill(1f/3f);
        PickPuzzlePoints();

        UIManager.Instance?.ShowMessage("Hydra vanished! Connect the two points (1/3 → 3/3).", 3f);
        yield break;
    }

    private void ResetBoardToFill(float fraction)
    {
        var bm = BoardManager.Instance; if (bm == null) return;

        bm.GenerateBoard();

        int rows = bm.rows, cols = bm.cols;
        var filled = new List<BoardSlot>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var s = bm.grid[r, c];
                if (s != null && s.HasLetterTile()) filled.Add(s);
            }

        int wantKeep = Mathf.Clamp(Mathf.RoundToInt(rows * cols * Mathf.Clamp01(fraction)), 0, filled.Count);
        int toRemove = Mathf.Max(0, filled.Count - wantKeep);

        for (int i = 0; i < toRemove; i++)
        {
            int idx = UnityEngine.Random.Range(0, filled.Count);
            var s = filled[idx]; filled.RemoveAt(idx);
            var t = s.RemoveLetter();
            if (t != null) SpaceManager.Instance.RemoveTile(t); // ทิ้ง
        }
    }

    private void PickPuzzlePoints()
    {
        var bm = BoardManager.Instance; if (bm == null) return;

        var candidates = new List<Vector2Int>();
        for (int r = 0; r < bm.rows; r++)
            for (int c = 0; c < bm.cols; c++)
                if (bm.grid[r, c] != null && bm.grid[r, c].HasLetterTile())
                    candidates.Add(new Vector2Int(r, c));

        if (candidates.Count < 2) { puzzleA = Vector2Int.zero; puzzleB = Vector2Int.zero; return; }

        puzzleA = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        puzzleB = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        UIManager.Instance?.UpdateTriangleHint(false); // reuse indicator ถ้าต้องการ
    }

    private bool CheckPuzzleConnected(Vector2Int a, Vector2Int b)
    {
        var bm = BoardManager.Instance; if (bm == null || bm.grid == null) return false;
        if (a == b) return true;

        bool[,] vis = new bool[bm.rows, bm.cols];
        var q = new Queue<Vector2Int>();
        if (a.x < 0 || a.x >= bm.rows || a.y < 0 || a.y >= bm.cols) return false;
        if (b.x < 0 || b.x >= bm.rows || b.y < 0 || b.y >= bm.cols) return false;
        if (bm.grid[a.x, a.y] == null || !bm.grid[a.x, a.y].HasLetterTile()) return false;
        if (bm.grid[b.x, b.y] == null || !bm.grid[b.x, b.y].HasLetterTile()) return false;

        q.Enqueue(a); vis[a.x, a.y] = true;
        int[] dr = {-1,1,0,0}; int[] dc = {0,0,-1,1};

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nr = cur.x + dr[k], nc = cur.y + dc[k];
                if (nr < 0 || nr >= bm.rows || nc < 0 || nc >= bm.cols) continue;
                if (vis[nr,nc]) continue;
                var s = bm.grid[nr, nc];
                if (s == null || !s.HasLetterTile()) continue;

                vis[nr, nc] = true;
                if (nr == b.x && nc == b.y) return true;
                q.Enqueue(new Vector2Int(nr, nc));
            }
        }
        return false;
    }

    private IEnumerator ConveyorLoop()
    {
        while (!LevelManager.Instance.isGameOver &&
               LevelManager.Instance.currentLevelConfig?.levelIndex == 3 &&
               !phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(5f, level3_conveyorIntervalSec));
            SetBossPose(BossPose.Casting, bossCastTelegraphSec);
            yield return new WaitForSecondsRealtime(bossCastTelegraphSec);
            var bm = BoardManager.Instance;
            if (bm == null || bm.grid == null) continue;

            // 1) รวบรวม "สลอตบนบอร์ดที่มีไทล์ และไทล์นั้น isLocked == true"
            var slots = new List<BoardSlot>();
            for (int r = 0; r < bm.rows; r++)
            for (int c = 0; c < bm.cols; c++)
            {
                var s = bm.grid[r,c];
                if (s == null) continue;
                var t = s.GetLetterTile();
                if (t == null) continue;

                // ✅ ต้องเป็นไทล์ที่ล็อกอยู่เท่านั้น
                if (t.isLocked)
                    slots.Add(s);
            }

            if (slots.Count < 2) continue;

            // 2) ถอนไทล์ออกมาเก็บไว้
            var tiles = new List<LetterTile>(slots.Count);
            foreach (var s in slots)
            {
                var t = s.RemoveLetter();     // จะถูก de-parent ออกมา
                if (t) tiles.Add(t);
            }

            // 3) สุ่มสลับลำดับแบบ Fisher–Yates
            for (int i = tiles.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
            }

            // 4) ใส่กลับลงสลอตเดิมตามลำดับใหม่ โดย “ไม่ยืดขนาดตาม BoardSlot”
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var tile = tiles[i];
                if (!slot || !tile) continue;
                tile.transform.SetParent(slot.transform, false);
                tile.transform.SetAsLastSibling();
                tile.transform.localPosition = Vector3.zero;
                // ฟิตให้พอดี BoardSlot เสมอ
                tile.AdjustSizeToParent();   // << ใช้อันนี้เพื่อยืดเต็มสลอต

                slot.Flash(new Color(0.7f,0.7f,1f,1f), 1, 0.06f);
            }

            UIManager.Instance?.ShowMessage("Conveyor Shuffle (locked tiles)!", 1.5f);
        }
        coConveyor = null;
    }

    private IEnumerator LockWaveLoop()
    {
        while (!LevelManager.Instance.isGameOver &&
            LevelManager.Instance.currentLevelConfig?.levelIndex == 3 &&
            !phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(10f, level3_lockWaveIntervalSec));
            SetBossPose(BossPose.Casting, bossCastTelegraphSec);
            yield return new WaitForSecondsRealtime(bossCastTelegraphSec);
            var bm = BoardManager.Instance; if (bm == null || bm.grid == null) continue;

            var allPicked = new List<BoardSlot>();
            int runs = Mathf.Max(1, level3_lockRunsPerWave);
            int len  = Mathf.Max(2, level3_lockRunLength);

            // สีเหมือน Level 2
            var l2 = Level2Controller.Instance;
            var color = (l2 != null) ? l2.L2_lockedOverlayColor : new Color(0f, 0f, 0f, 0.55f);

            // ❗ บิลด์เขตต้องห้ามเริ่มต้นจาก “สิ่งที่ล็อกอยู่แล้ว”
            var banned = BuildBannedSetFromBoardAndPicked(allPicked);

            for (int i = 0; i < runs; i++)
            {
                // เลือกแนวตามที่อนุญาต
                List<bool> allowed = new List<bool>();
                if (level3_lockAllowHorizontal) allowed.Add(true);
                if (level3_lockAllowVertical)   allowed.Add(false);
                if (allowed.Count == 0) allowed.Add(true);

                bool horizontal = allowed[UnityEngine.Random.Range(0, allowed.Count)];

                // ✅ เรียกสุ่มโดยส่ง banned เข้าไป
                var run = TryPickLockRun(horizontal, len, banned, 60);
                if (run.Count == 0)
                    run = TryPickLockRun(!horizontal, len, banned, 60);

                if (run.Count > 0)
                {
                    foreach (var s in run)
                    {
                        s.IsLocked = true;
                        s.SetLockedVisual(true, color);
                        allPicked.Add(s);
                    }

                    // อัปเดตเขตต้องห้ามด้วยเส้นที่เพิ่งได้ + ฮาโล 8 ทิศ
                    banned = BuildBannedSetFromBoardAndPicked(allPicked);
                }
            }

            if (allPicked.Count > 0)
            {
                UIManager.Instance?.ShowMessage($"Hydra locks {allPicked.Count} cells in lines!", 1.4f);
                // รอหมดเวลาแล้วปลดล็อก
                yield return new WaitForSecondsRealtime(Mathf.Max(1f, level3_lockDurationSec));

                foreach (var s in allPicked)
                {
                    if (!s) continue;
                    s.IsLocked = false;
                    s.SetLockedVisual(false);
                }
                lockedByBoss.RemoveAll(x => x == null || !x.IsLocked);
            }
            // ถ้าไม่ได้ล็อกอะไร (บอร์ดแน่น/เงื่อนไขไม่พอ) ก็ปล่อยผ่านรอบนี้
        }
        coLockWave = null;
    }

    private IEnumerator FieldEffectsLoop()
    {
        while (!LevelManager.Instance.isGameOver &&
            LevelManager.Instance.currentLevelConfig?.levelIndex == 3 &&
            !phaseChangeActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(10f, level3_fieldEffectIntervalSec));
            SetBossPose(BossPose.Casting, bossCastTelegraphSec);
            yield return new WaitForSecondsRealtime(bossCastTelegraphSec);
            var bm = BoardManager.Instance; if (bm == null || bm.grid == null) continue;

            int rows = bm.rows, cols = bm.cols;
            int size = Mathf.Max(2, level3_zoneSize);

            // พยายามสุ่มตำแหน่ง rect ที่ไม่ทับโซนใด ๆ และไม่ "แตะ" โซนต่างประเภท
            RectInt? TrySpawnRect(bool isBuff, int maxTry = 80)
            {
                for (int t = 0; t < maxTry; t++)
                {
                    int r = UnityEngine.Random.Range(0, Mathf.Max(1, rows - size + 1));
                    int c = UnityEngine.Random.Range(0, Mathf.Max(1, cols - size + 1));
                    var rect = new RectInt(c, r, size, size);

                    bool ok = true;
                    foreach (var z in activeZones)
                    {
                        var haloAll = Inflate(z.rect, 1);
                        // ไม่ให้ทับโซนใด ๆ
                        if (RectOverlap(rect, z.rect)) { ok = false; break; }

                        // ถ้าคนละประเภท ให้มี buffer 1 ช่องรอบโซน (ไม่ให้ "ติดกัน" แม้เฉียง)
                        if (z.isBuff != isBuff)
                        {
                            var halo = Inflate(z.rect, 1);
                            if (RectOverlap(rect, halo)) { ok = false; break; }
                        }
                    }
                    if (!ok) continue;

                    return rect;
                }
                return null;
            }

            // ลงสี overlay บนบอร์ด (ใช้ overlay เดียวกันทั้งสองประเภท – โทนสีต่างกัน)
            void PaintRect(RectInt rect, bool isBuff, float duration)
            {
                Color col = isBuff ? level3_zoneBuffColor : level3_zoneDebuffColor;
                for (int rr = rect.y; rr < rect.y + rect.height; rr++)
                for (int cc = rect.x; cc < rect.x + rect.width; cc++)
                {
                    var s = bm.grid[rr, cc];
                    if (!s) continue;
                    // ใช้ overlay เดียวกันให้ “ลุค” เหมือน zone ของ L2 แต่คนละสีใน L3
                    s.SetZoneOverlayTop(col);
                }
                activeZones.Add(new L3Zone { rect = rect, isBuff = isBuff, end = Time.unscaledTime + Mathf.Max(3f, level3_fieldEffectDurationSec) });
            }

            int perType = Mathf.Max(1, level3_zonesPerType);

            // สร้าง Buff ก่อน แล้วค่อย Debuff (ลำดับนี้ช่วยให้ Debuff เลี่ยง Buff ง่ายขึ้น)
            for (int i = 0; i < perType; i++)
            {
                var rect = TrySpawnRect(true);
                if (rect.HasValue) PaintRect(rect.Value, true, level3_fieldEffectDurationSec);
            }
            for (int i = 0; i < perType; i++)
            {
                var rect = TrySpawnRect(false);
                if (rect.HasValue) PaintRect(rect.Value, false, level3_fieldEffectDurationSec);
            }

            // รอหมดอายุแล้วเคลียร์ overlay ที่หมดเวลา
            yield return new WaitForSecondsRealtime(Mathf.Max(3f, level3_fieldEffectDurationSec));

            var keep = new List<L3Zone>();
            foreach (var z in activeZones)
            {
                if (Time.unscaledTime <= z.end) { keep.Add(z); continue; }
                for (int rr = z.rect.y; rr < z.rect.y + z.rect.height; rr++)
                for (int cc = z.rect.x; cc < z.rect.x + z.rect.width; cc++)
                {
                    var s = bm.grid[rr, cc];
                    if (s) s.ClearZoneOverlay();
                }
            }
            activeZones.Clear();
            activeZones.AddRange(keep);
        }
        coField = null;
    }

    private IEnumerator DeleteActionLoop()
    {
        while (!LevelManager.Instance.isGameOver &&
            LevelManager.Instance.currentLevelConfig?.levelIndex == 3 &&
            !phaseChangeActive)
        {
            // ถึงรอบสุ่ม
            yield return new WaitForSecondsRealtime(Mathf.Max(5f, level3_deleteActionIntervalSec));
            SetBossPose(BossPose.Casting, bossCastTelegraphSec);
            yield return new WaitForSecondsRealtime(bossCastTelegraphSec);

            // โอกาสสุ่มเลือกแหล่งที่จะลบ: 0=Cards, 1=Bench/Space, 2=Board(locked)
            // โอกาสสุ่ม: 0=Cards, 1=Bench/Space, 2=Board(locked)
            int[] buckets = new int[] { 0, 1, 2 };
            // สลับลำดับก่อนลอง
            for (int i = buckets.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (buckets[i], buckets[j]) = (buckets[j], buckets[i]);
            }

            bool didSomething = false;

            foreach (int b in buckets)
            {
                switch (b)
                {
                    case 0: // CARDSLOT
                        if (Time.unscaledTime < nextCardDeleteTime) break;

                        if (HasAnyCardInHand())
                        {
                            yield return StartCoroutine(DeleteRandomCardCo()); // ✅ ลบเท่านั้น
                            nextCardDeleteTime = Time.unscaledTime + Mathf.Max(0f, level3_deleteCardsCooldownSec);
                            didSomething = true;
                        }
                        // ถ้าไม่มีการ์ด → ไม่ทำอะไร, ไม่ตั้งคูลดาวน์
                        break;

                    case 1: // BENCH/SPACE
                        if (Time.unscaledTime < nextBenchDeleteTime) break;
                        {
                            int removed = DeleteFromHandAndRefill(Mathf.Max(1, level3_deleteBenchCount));
                            if (removed > 0)
                            {
                                nextBenchDeleteTime = Time.unscaledTime + Mathf.Max(0f, level3_deleteBenchCooldownSec);
                                didSomething = true;
                            }
                        }
                        break;

                    default: // BOARD (locked only)
                        if (Time.unscaledTime < nextBoardDeleteTime) break;
                        if (DeleteLockedLettersFromBoard(Mathf.Max(1, level3_deleteBoardCount)))
                        {
                            nextBoardDeleteTime = Time.unscaledTime + Mathf.Max(0f, level3_deleteLettersCooldownSec);
                            didSomething = true;
                        }
                        break;
                }
                if (didSomething) break; // ทำสำเร็จแล้วออกจากรอบนี้
            }
            // ถ้า 3 แบบทำไม่สำเร็จเลย (พื้นที่ไม่พร้อม) ก็จบรอบนี้ไป
        }
        coDelete = null;
    }
    // ลบการ์ดจาก CardSlot ถ้ามีอย่างน้อย 1 ใบ (ไม่มี → ไม่ทำอะไร)
    private IEnumerator DeleteRandomCardCo()
    {
        var cm = CardManager.Instance;
        if (cm == null || cm.heldCards == null || cm.heldCards.Count == 0)
            yield break;

        // หา index ที่มีการ์ดอยู่จริง
        var occupied = new List<int>();
        for (int i = 0; i < cm.heldCards.Count; i++)
            if (cm.heldCards[i] != null) occupied.Add(i);

        if (occupied.Count == 0)
            yield break; // ไม่มีการ์ดให้ลบ

        // ลบแบบสุ่ม 1 ใบ
        int slotIndex = Random.Range(0, occupied.Count);
        slotIndex = occupied[slotIndex];

        // หา CardSlotUI เพื่อเล่นอนิเมชัน (ถ้ามี)
        CardSlotUI targetUI = null;
    #if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        foreach (var s in UnityEngine.Object.FindObjectsByType<CardSlotUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        { if (s.slotIndex == slotIndex) { targetUI = s; break; } }
    #else
        foreach (var s in GameObject.FindObjectsOfType<CardSlotUI>(true))
        { if (s.slotIndex == slotIndex) { targetUI = s; break; } }
    #endif

        if (targetUI != null)
            yield return targetUI.PlayUseThen(null); // เล่นเอฟเฟกต์ก่อนลบ

        // ลบออกจากมือ + อัปเดต UI
        cm.heldCards[slotIndex] = null;
        UIManager.Instance?.UpdateCardSlots(cm.heldCards);
        UIManager.Instance?.ShowMessage("Hydra deletes a card!", 1.2f);
    }
    private bool HasAnyCardInHand()
    {
        var cm = CardManager.Instance;
        if (cm == null || cm.heldCards == null) return false;
        foreach (var c in cm.heldCards) if (c != null) return true;
        return false;
    }

    // ===== ลบจาก Bench + Space แล้ว REFILL ทันทีตามจำนวนที่ลบ =====
    private bool DeleteLockedLettersFromBoard(int count)
    {
        var board = BoardManager.Instance; if (board == null || board.grid == null) return false;

        var candidates = new List<BoardSlot>();
        for (int r = 0; r < board.rows; r++)
            for (int c = 0; c < board.cols; c++)
            {
                var s = board.grid[r, c];
                if (s == null || s.transform.childCount == 0) continue;
                var t = s.GetLetterTile();
                if (t != null && t.isLocked) candidates.Add(s);
            }

        if (candidates.Count == 0) return false;

        int k = Mathf.Clamp(count, 1, candidates.Count);
        for (int i = 0; i < k; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            var slot = candidates[idx]; candidates.RemoveAt(idx);
            if (!slot) continue;

            var tile = slot.GetLetterTile();
            if (!tile) continue;

            slot.Flash(new Color(1f, .35f, .35f, 1f), 1, 0.07f);
            Destroy(tile.gameObject, 0.1f);
        }

        UIManager.Instance?.ShowMessage($"Hydra deletes {k} locked letters!", 1.4f);
        return true;
    }

    private void ClearZonesAndLocks()
    {
        activeZones.Clear();
        foreach (var s in lockedByBoss)
        {
            if (s) { s.IsLocked = false; s.ApplyVisual(); }
        }
        lockedByBoss.Clear();
    }
}
