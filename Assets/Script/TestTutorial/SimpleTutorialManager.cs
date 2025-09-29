using System.Collections.Generic;
using UnityEngine;

public class SimpleTutorialManager : MonoBehaviour
{
    [Header("Config")]
    public SimpleTutorialSequenceSO sequence;

    [Header("Anchors")]
    public Canvas targetCanvas;
    public RectTransform benchAnchor; // ลากกรอบ UI ของ Bench มาใส่

    [Header("UI")]
    public SimpleTutorialUI ui;

    int index = -1;
    bool running = false;

    void Start()
    {
        if (sequence == null) return;

        if (!sequence.runOnFirstLaunch || PlayerPrefs.GetInt(sequence.seenPlayerPrefKey, 0) == 0)
        {
            StartSequence();
        }
    }

    public void StartSequence()
    {
        if (running) return;
        running = true;
        index = -1;
        Next();
    }

    public void Next()
    {
        index++;
        if (sequence == null || index >= sequence.steps.Count)
        {
            EndSequence();
            return;
        }

        var step = sequence.steps[index];

        // เตรียม Overlay + click-anywhere
        ui.ConfigureOverlay(step.clickAnywhereToAdvance ? (System.Action)Next : null,
                            enableDim: step.type == SimpleTutStepType.SubtitleAndHighlight && step.dimBackground);

        // แสดง Subtitle
        ui.ShowSubtitle(step.text, MapAnchor(step.subtitleAnchor), step.subtitleOffset);

        // ไฮไลต์ (ถ้าสเต็ปนี้ต้องการ)
        if (step.type == SimpleTutStepType.SubtitleAndHighlight)
        {
            ui.ShowHighlight(MapAnchor(step.highlightTarget), step.highlightPadding);
        }
        else
        {
            // ไม่มีไฮไลต์
            ui.ShowHighlight(null, 0);
        }
    }

    public void EndSequence()
    {
        running = false;
        ui.HideAll();
        if (sequence && sequence.runOnFirstLaunch)
            PlayerPrefs.SetInt(sequence.seenPlayerPrefKey, 1);
    }

    RectTransform MapAnchor(SimpleTutAnchor a)
    {
        switch (a)
        {
            case SimpleTutAnchor.Bench:       return benchAnchor;
            case SimpleTutAnchor.ScreenCenter:return (RectTransform)targetCanvas.transform; // กลางจอ
            default:                          return null;
        }
    }
    // เรียกจากปุ่มเพื่อรีเซ็ตสถานะว่าเคยดู แล้วเริ่มทิวทอเรียลใหม่ทันที
    public void ResetTutorialAndRestart()
    {
        if (sequence == null) return;
        PlayerPrefs.DeleteKey(sequence.seenPlayerPrefKey); // ลบ flag ว่าเคยดู
        PlayerPrefs.Save();
        ui.HideAll();
        running = false;
        StartSequence();
    }

    // ถ้าอยากแค่ล้างสถานะ แต่ยังไม่เริ่ม ให้เรียกอันนี้แทน
    public void ResetTutorialFlagOnly()
    {
        if (sequence == null) return;
        PlayerPrefs.DeleteKey(sequence.seenPlayerPrefKey);
        PlayerPrefs.Save();
    }
}
