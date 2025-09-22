using UnityEngine;

public class StartShopTutorial : MonoBehaviour
{
    public TutorialConfigSO shopConfig;

    void Start()
    {
        TutorialManager.Instance?.SetConfig(shopConfig, startNow: true);
        TutorialManager.Instance?.Fire(TutorialEvent.ShopOpen);
    }
}
