// GameplayTutorialConfig.cs
using UnityEngine;
using UnityEngine.UI;

public class GameplayTutorialConfig : MonoBehaviour
{
    [Header("Targets (ลากของจริงในซีนมาใส่)")]
    public RectTransform rackFirstLetter;   // ตัวอักษรช่องแรกในแถบถือ (หรือคอนเทนเนอร์ของมัน)
    public RectTransform boardArea;         // RectTransform ของกระดาน (พื้นที่ที่ปล่อยได้)
    public Button confirmButton;            // ปุ่มยืนยัน/คอนเฟิร์ม

    [Header("Run options")]
    public bool runOnlyOnFirstPlay = true;  // true = เฉพาะครั้งแรก

    void Awake()
    {
        // เงื่อนไขเข้าครั้งแรก
        if (runOnlyOnFirstPlay && PlayerPrefs.GetInt("TUT_GAMEPLAY_DONE", 0) == 1) return;

        var tm = TutorialManager.Instance;
        if (tm == null)
        {
            Debug.LogError("[GameplayTutorialConfig] ไม่พบ TutorialManager ในซีน");
            return;
        }

        // bind confirm ให้ชัวร์
        if (confirmButton) tm.confirmButton = confirmButton;

        // สร้างสเต็ปแบบโปรแกรมมิ่ง (ไม่ต้องไปไล่กรอกใน Inspector)
        var intro = new TutorialManager.Step
        {
            id = "gp_intro",
            message = "มาลองวางตัวอักษรลงบนกระดานกัน!\nแตะเพื่อไปต่อ",
            useCharacter = true,
            tapAnywhereToContinue = true,
            blockInput = true,                  // บล็อกเมนูทั้งหมด
            showSpotlightOnAllowed = false      // ยังไม่ส่องจุดไหน
        };

        var place = new TutorialManager.Step
        {
            id = "gp_place_letter",
            message = "ลากตัวอักษรจากแถวด้านล่าง\nไปวางบนช่องกระดาน (บริเวณสว่าง)",
            useCharacter = true,
            blockInput = true,                  // บล็อกทั้งจอ
            tapAnywhereToContinue = false,      // ห้ามแตะพื้นดำเพื่อข้าม
            waitFor = TutorialManager.WaitType.TilePlacedOnBoard, // รอวางสำเร็จ
            highlightTarget = rackFirstLetter,  // เน้นที่ตัวอักษรตัวแรก
            allowClickOnHighlight = true,       // อนุญาตเริ่มลากจากตรงนี้
            allowClickExtraRects = new RectTransform[] { boardArea }, // และอนุญาตปล่อย/คลิกบนกระดาน
            allowClickPadding = 24f,
            showSpotlightOnAllowed = true       // สว่างเฉพาะ rackFirstLetter + boardArea
        };

        var confirm = new TutorialManager.Step
        {
            id = "gp_confirm",
            message = "เยี่ยมมาก! ตอนนี้กดปุ่มยืนยันเพื่อส่งคำตอบ",
            useCharacter = true,
            blockInput = true,
            tapAnywhereToContinue = false,
            waitFor = TutorialManager.WaitType.ConfirmPressed,     // รอให้กดปุ่ม
            highlightTarget = confirmButton ? confirmButton.GetComponent<RectTransform>() : null,
            allowClickOnHighlight = true,       // กดได้เฉพาะปุ่มยืนยัน
            allowClickExtraRects = null,
            showSpotlightOnAllowed = true
        };

        var done = new TutorialManager.Step
        {
            id = "gp_done",
            message = "เสร็จขั้นพื้นฐานแล้ว! ลุยต่อได้เลย 🎉",
            useCharacter = true,
            blockInput = false,                 // เปิดอินพุตกลับเป็นปกติ
            tapAnywhereToContinue = true,
            showSpotlightOnAllowed = false
        };

        tm.steps = new TutorialManager.Step[] { intro, place, confirm, done };

        // ปรับค่า overlay ให้เป็น “ดำอ่อน” และบล็อกเมนูแน่นอน
        tm.forceTutorial = true;
        tm.startOnAwake = true;
        tm.skipIfAlreadyDone = false;
        // ถ้าใน TutorialManager ของคุณเปิด public ไว้: tm.overlayTargetAlpha = 0.78f;

        // จบแล้ว mark ว่าทำไปแล้ว (กันวนซ้ำ) — จะทำตอน Finish ก็ได้
        TutorialManager.OnTutorialFinished += () =>
        {
            PlayerPrefs.SetInt("TUT_GAMEPLAY_DONE", 1);
            PlayerPrefs.Save();
        };
    }
}
