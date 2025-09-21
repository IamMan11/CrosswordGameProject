using UnityEngine;

public class StartShopTutorial : MonoBehaviour
{
    [SerializeField] private TutorialConfigSO shopConfig;

    private void Start()
    {
        if (TutorialManager.Instance && shopConfig)
            TutorialManager.Instance.SetConfig(shopConfig, true);
    }
}