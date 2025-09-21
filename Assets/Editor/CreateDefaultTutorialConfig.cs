#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class CreateDefaultTutorialConfig
{
    [MenuItem("Tools/Crossword/Create Default Tutorial Config")]
    public static void Create()
    {
        var so = ScriptableObject.CreateInstance<TutorialConfigSO>();
        so.runOnFirstLaunch = true;
        so.seenKey = "TUTORIAL_SEEN";

        var steps = new List<TutorialStep>();

        // 1) Intro (ไม่ต้องรออีเวนต์)
        steps.Add(new TutorialStep {
            id="intro", title="ยินดีต้อนรับ!",
            body="มาลองเล่นครอสเวิร์ดแบบสั้น ๆ กันนะ",
            blockInput=true, advanceOnAnyKey=true,
            focus=FocusKey.None,
            style=new TutorialStyle{
                anchor=AnchorMode.ScreenCenter, dimAlpha=0.82f, showSkip=true,
                nextLabel="ต่อไป", backLabel="ย้อนกลับ", skipLabel="ข้าม ›",
                cardLeftVertical="J\nO\nK\nE\nR", cardRightVertical="J\nO\nK\nE\nR"
            }
        });

        // 2) Bench → รอวางตัวแรกลงบอร์ด
        steps.Add(new TutorialStep {
            id="bench", title="แถบตัวอักษร (Bench)",
            body="ลากตัวอักษรจาก Bench ไปวางบนกระดาน",
            blockInput=false, advanceOnAnyKey=false,
            waitForEvent=true, trigger=TutorialEvent.FirstTilePlaced,
            focus=FocusKey.Bench, focusPadding=new Vector2(24,24),
            style=new TutorialStyle{ anchor=AnchorMode.FocusTarget, stackOffset=new Vector2(0,220), dimAlpha=0.75f }
        });

        // 3) Board (แนะนำเฉย ๆ ไม่รออีเวนต์)
        steps.Add(new TutorialStep {
            id="board", title="ประกอบคำบนกระดาน",
            body="พยายามจัดให้เป็นคำจริง 2–3 ตัวอักษรก็ได้",
            blockInput=false, advanceOnAnyKey=true,
            waitForEvent=false,
            focus=FocusKey.Board, focusPadding=new Vector2(32,32),
            style=new TutorialStyle{ anchor=AnchorMode.FocusTarget, stackOffset=new Vector2(0,200), dimAlpha=0.75f }
        });

        // 4) Confirm → รอคำถูกยืนยันเรียบร้อย
        steps.Add(new TutorialStep {
            id="confirm", title="ส่งคำ",
            body="กดปุ่มยืนยันเพื่อนับคะแนน",
            blockInput=false, advanceOnAnyKey=false,
            waitForEvent=true, trigger=TutorialEvent.FirstWordConfirmed,
            focus=FocusKey.ConfirmButton, focusPadding=new Vector2(28,20),
            style=new TutorialStyle{ anchor=AnchorMode.FocusTarget, stackOffset=new Vector2(-40,160), dimAlpha=0.78f }
        });

        // 5) Dictionary → รอเปิดดิกครั้งแรก
        steps.Add(new TutorialStep {
            id="dict", title="พจนานุกรม",
            body="กดดูความหมายคำศัพท์ได้จากปุ่มนี้",
            blockInput=false, advanceOnAnyKey=false,
            waitForEvent=true, trigger=TutorialEvent.DictionaryOpened,
            focus=FocusKey.DictionaryButton, focusPadding=new Vector2(24,18),
            style=new TutorialStyle{ anchor=AnchorMode.FocusTarget, stackOffset=new Vector2(-20,140), dimAlpha=0.78f }
        });

        // 6) Mana UI (แนะนำ) + ใช้อีเวนต์ตอนมานาหมดก็ได้
        steps.Add(new TutorialStep {
            id="mana", title="มานา",
            body="การ์ดบางใบใช้มานา—มานาจะเพิ่มต่อเทิร์นหรือได้จากเอฟเฟกต์",
            blockInput=false, advanceOnAnyKey=false,
            waitForEvent=true, trigger=TutorialEvent.ManaEmpty,   // ถ้ายังไม่ยิง ก็ข้ามด้วยคลิก Next ได้
            focus=FocusKey.ManaUI, focusPadding=new Vector2(22,16),
            style=new TutorialStyle{ anchor=AnchorMode.FocusTarget, stackOffset=new Vector2(0,160), dimAlpha=0.78f }
        });

        // 7) Pick Card (ไม่มีอีเวนต์เฉพาะ ใช้กดต่อไป)
        steps.Add(new TutorialStep {
            id="getcard", title="เลือกการ์ด",
            body="บางจังหวะคุณจะได้เลือกการ์ด 1 จาก 3 ใบ — ลองเลือกดู",
            blockInput=false, advanceOnAnyKey=true,
            waitForEvent=false,
            focus=FocusKey.BagUI, focusPadding=new Vector2(22,16),
            style=new TutorialStyle{ anchor=AnchorMode.ScreenCenter, stackOffset=Vector2.zero, dimAlpha=0.82f }
        });

        // 8) Use Card → รอใช้การ์ดพิเศษครั้งแรก
        steps.Add(new TutorialStep {
            id="usecard", title="ใช้การ์ด",
            body="กดการ์ด → ยืนยัน → ดูเอฟเฟกต์ เช่น เติม Bench / x2 คะแนน ฯลฯ",
            blockInput=false, advanceOnAnyKey=false,
            waitForEvent=true, trigger=TutorialEvent.FirstSpecialUsed,
            focus=FocusKey.BagUI, focusPadding=new Vector2(22,16),
            style=new TutorialStyle{ anchor=AnchorMode.FocusTarget, stackOffset=new Vector2(0,160), dimAlpha=0.78f }
        });

        // 9) DL / DW (แนะนำ)
        steps.Add(new TutorialStep {
            id="mult", title="ช่องพิเศษ",
            body="DL = ตัวอักษรคูณ, DW = คำคูณ — วางตำแหน่งให้ดีเพื่อบูสต์คะแนน",
            blockInput=false, advanceOnAnyKey=true,
            focus=FocusKey.Board, focusPadding=new Vector2(32,32),
            style=new TutorialStyle{ anchor=AnchorMode.FocusTarget, stackOffset=new Vector2(0,200), dimAlpha=0.75f }
        });

        // 10) Finish
        steps.Add(new TutorialStep {
            id="finish", title="พร้อมลุย!",
            body="แค่นี้ก็เข้าใจพื้นฐานแล้ว — ไปเริ่มด่านแรกกันเลย",
            blockInput=true, advanceOnAnyKey=true,
            focus=FocusKey.None,
            style=new TutorialStyle{ anchor=AnchorMode.ScreenCenter, dimAlpha=0.82f }
        });

        so.steps = steps;

        var path = "Assets/ScriptableObjects/TutorialConfig.asset";
        System.IO.Directory.CreateDirectory("Assets/ScriptableObjects");
        AssetDatabase.CreateAsset(so, AssetDatabase.GenerateUniqueAssetPath(path));
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = so;
        Debug.Log($"[Tutorial] Created config at {AssetDatabase.GetAssetPath(so)}");
    }
}
#endif