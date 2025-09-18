using UnityEngine;
using UnityEngine.UI;

public class GameplayTutorialBinder : MonoBehaviour
{
    public TutorialManager tm;
    public TutorialDialogAsset dialogAsset;

    void Start()
    {
        if (!tm) tm = FindObjectOfType<TutorialManager>();
        if (!tm) { Debug.LogError("No TutorialManager in scene."); return; }

        tm.playerPrefsKey = "TUT_GAMEPLAY";
        tm.forceTutorial = true;
        tm.startOnAwake = false;

        var steps = new System.Collections.Generic.List<TutorialManager.Step>();
        if (dialogAsset != null && dialogAsset.steps != null && dialogAsset.steps.Length > 0)
        {
            foreach (var sd in dialogAsset.steps)
            {
                var s = new TutorialManager.Step();
                s.id = sd.id; s.message = sd.message; s.useCharacter = sd.useCharacter; s.speakerName = sd.speakerName; s.portrait = sd.portrait;
                s.useTypewriter = sd.useTypewriter; s.typeSpeed = sd.typeSpeed; s.voiceBeep = sd.voiceBeep; s.beepInterval = sd.beepInterval;
                s.waitFor = sd.waitFor; s.waitTime = sd.waitTime; s.blockInput = sd.blockInput; s.tapAnywhereToContinue = sd.tapAnywhereToContinue;
                s.pressNextToContinue = sd.pressNextToContinue; s.handOffset = sd.handOffset; s.allowClickOnHighlight = sd.allowClickOnHighlight;
                s.allowClickPadding = sd.allowClickPadding; s.showSpotlightOnAllowed = sd.showSpotlightOnAllowed;

                if (!string.IsNullOrEmpty(sd.highlightTargetName))
                {
                    var go = GameObject.Find(sd.highlightTargetName);
                    if (go) s.highlightTarget = go.GetComponent<RectTransform>();
                }

                if (sd.allowClickExtraNames != null && sd.allowClickExtraNames.Length > 0)
                {
                    var list = new System.Collections.Generic.List<RectTransform>();
                    foreach (var n in sd.allowClickExtraNames)
                    {
                        var go = GameObject.Find(n);
                        if (go) list.Add(go.GetComponent<RectTransform>());
                    }
                    if (list.Count > 0) s.allowClickExtraRects = list.ToArray();
                }

                steps.Add(s);
            }
        }

        tm.steps = steps.ToArray();
        tm.StartTutorialNow();
    }
}


