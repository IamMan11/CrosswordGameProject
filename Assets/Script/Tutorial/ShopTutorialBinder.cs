using UnityEngine;
using UnityEngine.UI;

public class ShopTutorialBinder : MonoBehaviour
{
    [Header("Tutorial")]
    public TutorialManager tm;

    [Header("UI Refs (ลากจาก Hierarchy)")]
    public RectTransform coinsPanel;          // แถบ Coins/ค่าสเตตัสด้านซ้าย
    public Button       rerollButton;         // ปุ่ม REROLL
    public RectTransform firstCardRect;       // การ์ดช่องแรก (รูปสีน้ำตาลในภาพ)
    public Button       firstBuyButton;       // ปุ่ม BUY ของช่องแรก
    public Button       nextButton;           // ปุ่ม NEXT มุมซ้ายบน
    [Header("Optional Dialog Asset")]
    public TutorialDialogAsset dialogAsset;

    void Awake()
    {
        if (!tm) tm = FindObjectOfType<TutorialManager>();
        if (!tm) { Debug.LogError("No TutorialManager in scene."); return; }

        // ตั้งค่าให้ทัวทอเรียลของร้านค้าแยก key คนละอันกับหน้า Mainmenu
        tm.playerPrefsKey   = "TUT_SHOP";
        tm.forceTutorial    = true;           // บังคับเปิดรอบนี้ (ปรับเป็น false หลังทดสอบ)
        // ป้องกันให้ TutorialManager พยายามเริ่มก่อนที่เราจะเซ็ต steps
        tm.startOnAwake     = false;
        tm.skipIfAlreadyDone= true;           // เคยผ่านแล้วจะไม่เล่นอีก

        // ให้ปุ่มยืนยัน = ปุ่ม NEXT (ใช้กับ WaitType.ConfirmPressed)
        tm.confirmButton = nextButton;

        // สร้างสเต็ปทีละขั้น (จาก dialogAsset ถ้ามี) หรือ fallback เป็นสเต็ปที่เขียนไว้
        var steps = new System.Collections.Generic.List<TutorialManager.Step>();

        if (dialogAsset != null && dialogAsset.steps != null && dialogAsset.steps.Length > 0)
        {
            foreach (var sd in dialogAsset.steps)
            {
                var s = new TutorialManager.Step();
                s.id = sd.id;
                s.message = sd.message;
                s.useCharacter = sd.useCharacter;
                s.speakerName = sd.speakerName;
                s.portrait = sd.portrait;
                s.useTypewriter = sd.useTypewriter;
                s.typeSpeed = sd.typeSpeed;
                s.voiceBeep = sd.voiceBeep;
                s.beepInterval = sd.beepInterval;
                s.waitFor = sd.waitFor;
                s.waitTime = sd.waitTime;
                s.blockInput = sd.blockInput;
                s.tapAnywhereToContinue = sd.tapAnywhereToContinue;
                s.pressNextToContinue = sd.pressNextToContinue;
                s.handOffset = sd.handOffset;
                s.allowClickOnHighlight = sd.allowClickOnHighlight;
                s.allowClickPadding = sd.allowClickPadding;
                s.showSpotlightOnAllowed = sd.showSpotlightOnAllowed;

                if (!string.IsNullOrEmpty(sd.highlightTargetName))
                {
                    var go = GameObject.Find(sd.highlightTargetName);
                    if (go) s.highlightTarget = go.GetComponent<RectTransform>();
                }

                if (sd.allowClickExtraNames != null && sd.allowClickExtraNames.Length > 0)
                {
                    var list = new System.Collections.Generic.List<RectTransform>();
                    foreach (var name in sd.allowClickExtraNames)
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        var go = GameObject.Find(name);
                        if (go) list.Add(go.GetComponent<RectTransform>());
                    }
                    if (list.Count > 0) s.allowClickExtraRects = list.ToArray();
                }

                steps.Add(s);
            }
        }
        else
        {
            // STEP 1: อธิบายหน้าร้าน + ชี้แถบ Coins/ค่าสถิติ
            steps.Add(new TutorialManager.Step {
                id = "SHOP_INTRO",
                message = "นี่คือหน้าร้านค้า ใช้เหรียญซื้อการ์ดและอัปเกรดได้",
                useCharacter = true,
                highlightTarget = coinsPanel,
                blockInput = true,
                tapAnywhereToContinue = true,              // แตะเพื่อไปต่อ
                allowClickOnHighlight = false,             // ห้ามกดอะไร
                showSpotlightOnAllowed = false
            });

            // STEP 2: ชี้ปุ่ม REROLL และให้กดจริง รอจน reroll สำเร็จ
            steps.Add(new TutorialManager.Step {
                id = "SHOP_REROLL",
                message = "ลองกด REROLL เพื่อสุ่มรายการการ์ดใหม่",
                useCharacter = true,
                highlightTarget = rerollButton.GetComponent<RectTransform>(),
                blockInput = true,
                tapAnywhereToContinue = false,
                allowClickOnHighlight = true,              // รูโปร่งใสที่ปุ่มนี้เท่านั้น
                allowClickPadding = 18f,
                waitFor = TutorialManager.WaitType.CardRolled, // เรียก NotifyCardRolled() หลังสุ่มเสร็จ
            });

            // STEP 3: ชี้การ์ดช่องแรก + ปุ่ม BUY อนุญาตให้คลิกทั้งสองตำแหน่ง และรอซื้อสำเร็จ
            steps.Add(new TutorialManager.Step {
                id = "SHOP_BUY_FIRST",
                message = "เลือกซื้อการ์ดใบแรกได้เลย",
                useCharacter = true,
                highlightTarget = firstCardRect,
                allowClickOnHighlight = true,
                allowClickExtraRects = new RectTransform[] {
                    firstBuyButton.GetComponent<RectTransform>()
                },
                allowClickPadding = 18f,
                blockInput = true,
                tapAnywhereToContinue = false,
                waitFor = TutorialManager.WaitType.ShopItemPurchased   // เรียก NotifyShopPurchased() เมื่อซื้อสำเร็จ
            });

            // STEP 4: ออกจากร้าน โดยให้กดปุ่ม NEXT จริง (รอ ConfirmPressed)
            steps.Add(new TutorialManager.Step {
                id = "SHOP_LEAVE",
                message = "เสร็จแล้ว กด NEXT เพื่อไปต่อ",
                useCharacter = true,
                highlightTarget = nextButton.GetComponent<RectTransform>(),
                allowClickOnHighlight = true,
                blockInput = true,
                waitFor = TutorialManager.WaitType.ConfirmPressed      // tm.confirmButton = nextButton ไว้แล้ว
            });
        }

        tm.steps = steps.ToArray();

        // เริ่ม tutorial ทันทีหลังเราเซ็ต steps ไว้เรียบร้อย
        tm.StartTutorialNow();
    }
}