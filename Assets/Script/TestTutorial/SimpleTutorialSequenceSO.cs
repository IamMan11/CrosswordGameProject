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
public enum SimpleTutAnchor { None, ScreenCenter, Bench }  // เพิ่มได้ภายหลัง เช่น Board, Buttons…

[Serializable]
public class SimpleTutorialStep
{
    public string id = "step";
    [TextArea] public string text;
    public SimpleTutStepType type = SimpleTutStepType.Subtitle;

    [Header("Subtitle placement")]
    public SimpleTutAnchor subtitleAnchor = SimpleTutAnchor.ScreenCenter;
    public Vector2 subtitleOffset = new Vector2(0, -260);

    [Header("Advance")]
    public bool clickAnywhereToAdvance = true;

    [Header("Highlight (used when type = SubtitleAndHighlight)")]
    public SimpleTutAnchor highlightTarget = SimpleTutAnchor.Bench;
    public float highlightPadding = 24f;
    public bool dimBackground = false;
}
