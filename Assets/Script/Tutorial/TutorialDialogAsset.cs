using UnityEngine;

[CreateAssetMenu(fileName = "TutorialDialog", menuName = "Tutorial/Dialogue Asset", order = 200)]
public class TutorialDialogAsset : ScriptableObject
{
    public StepData[] steps;

    [System.Serializable]
    public class StepData
    {
        public string id;
        [TextArea] public string message;
        public bool useCharacter = true;
        public string speakerName = "โค้ชครอส";
        public Sprite portrait;

        [Header("Typewriter")]
        public bool useTypewriter = true;
        public float typeSpeed = 45f;
        public AudioClip voiceBeep;
        public float beepInterval = 0.03f;

        [Header("Wait")]
        public TutorialManager.WaitType waitFor = TutorialManager.WaitType.None;
        public float waitTime = 0f;

        [Header("UI Targets (ชื่อ GameObject ใน Scene)")]
        public string highlightTargetName;
        public bool blockInput = true;
        public bool tapAnywhereToContinue = false;
        public bool pressNextToContinue = false;
        public Vector2 handOffset = new Vector2(0, -80);

        [Header("Allow Click Extras (ชื่อ GameObject)")]
        public string[] allowClickExtraNames;
        public bool allowClickOnHighlight = false;
        public float allowClickPadding = 16f;

        [Header("Spotlight")]
        public bool showSpotlightOnAllowed = true;
    }
}


