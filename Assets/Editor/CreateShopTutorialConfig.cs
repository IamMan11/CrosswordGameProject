// Assets/Editor/CreateShopTutorialConfig.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class CreateShopTutorialConfig
{
    [MenuItem("Tools/Tutorial/Create Shop Tutorial Config", priority = 1000)]
    public static void CreateAsset()
    {
        // สร้าง ScriptableObject
        var cfg = ScriptableObject.CreateInstance<TutorialConfigSO>();
        cfg.runOnFirstLaunch = true;                 // ให้รันอัตโนมัติเมื่อเข้าหน้า Shop ครั้งแรก
        cfg.seenKey = "SHOP_TUTORIAL_SEEN";          // คีย์จำว่าเคยเห็นแล้ว

        // -------------------- สเต็ปของทัวทอเรียล --------------------
        var steps = new List<TutorialStep>();

        // 0) แนะนำหน้าร้าน
        steps.Add(new TutorialStep {
            id = "shop_intro",
            title = "ร้านค้า (Shop)",
            body = "ซื้ออัปเกรดและการ์ดจากที่นี่\nเหรียญที่มุมซ้ายคือยอดคงเหลือของคุณ",
            blockInput = true,
            advanceOnAnyKey = true,
            waitForEvent = false,
            trigger = TutorialEvent.None,
            focus = FocusKey.ShopPanel,
            focusPadding = new Vector2(8, 8),
            style = new TutorialStyle {
                anchor = AnchorMode.FocusTarget,
                stackOffset = new Vector2(0, 120),
                dimAlpha = 0.75f, showSkip = true,
                nextLabel = "Next", backLabel = "Back", skipLabel = "Skip ›",
                cardLeftVertical = " ", cardRightVertical = " "
            }
        });

        // 1) ชี้ยอดเหรียญ
        steps.Add(new TutorialStep {
            id = "shop_coin",
            title = "เหรียญ (Coin)",
            body = "จำนวนเหรียญที่ใช้ซื้อของในร้าน",
            blockInput = true,
            focus = FocusKey.ShopCoin,
            focusPadding = new Vector2(8, 8),
            style = new TutorialStyle {
                anchor = AnchorMode.FocusTarget,
                stackOffset = new Vector2(0, 110),
                dimAlpha = 0.75f, showSkip = true
            }
        });

        // 2) แถวรายการการ์ด/ไอเทมขาย
        steps.Add(new TutorialStep {
            id = "shop_items",
            title = "รายการไอเทม",
            body = "รายการการ์ดที่สุ่มมาในรอบนี้\nสามารถกด REROLL เพื่อสุ่มใหม่ได้",
            blockInput = true,
            focus = FocusKey.ShopItemsRow,
            focusPadding = new Vector2(10, 10),
            style = new TutorialStyle {
                anchor = AnchorMode.FocusTarget,
                stackOffset = new Vector2(0, 140),
                dimAlpha = 0.75f, showSkip = true
            }
        });

        // 3) สาธิตการซื้ออัปเกรด (ตัวอย่าง: เพิ่มช่องการ์ด)
        steps.Add(new TutorialStep {
            id = "shop_buy_slot",
            title = "ขยายช่องการ์ด",
            body = "กดซื้อเพื่อเพิ่มจำนวนช่องการ์ดที่ถือได้",
            blockInput = true,
            advanceOnAnyKey = false,
            waitForEvent = true,                 // รอให้ผู้เล่นกดซื้อ
            trigger = TutorialEvent.ShopBuy,     // ← ยิงจาก ShopManager เมื่อซื้อสำเร็จ
            focus = FocusKey.ShopBuyMaxCard,
            focusPadding = new Vector2(12, 12),
            style = new TutorialStyle {
                anchor = AnchorMode.FocusTarget,
                stackOffset = new Vector2(0, 140),
                dimAlpha = 0.75f, showSkip = true
            }
        });

        // 4) สาธิตปุ่ม REROLL
        steps.Add(new TutorialStep {
            id = "shop_reroll",
            title = "สุ่มรายการใหม่ (REROLL)",
            body = "กด REROLL เพื่อสุ่มรายการการ์ดใหม่ (มีค่าใช้จ่าย)",
            blockInput = true,
            advanceOnAnyKey = false,
            waitForEvent = true,                 // รอให้กด reroll จนจบแอนิเมชัน
            trigger = TutorialEvent.ShopReroll,  // ← ยิงจาก ShopManager หลัง reroll เสร็จ
            focus = FocusKey.ShopReroll,
            focusPadding = new Vector2(12, 12),
            style = new TutorialStyle {
                anchor = AnchorMode.FocusTarget,
                stackOffset = new Vector2(0, 120),
                dimAlpha = 0.75f, showSkip = true
            }
        });

        cfg.steps = steps;

        // บันทึกไฟล์
        Directory.CreateDirectory("Assets/ScriptableObjects");
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/ScriptableObjects/TutorialConfig_Shop.asset");
        AssetDatabase.CreateAsset(cfg, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = cfg;
        Debug.Log("[Tutorial] Created: " + path);
    }
}
#endif
