using UnityEngine;

public class StartPlayTutorial : MonoBehaviour
{
    public TutorialConfigSO playConfig;

    void Start()
    {
        TutorialManager.Instance?.SetConfig(playConfig, startNow: true);
    }
}
