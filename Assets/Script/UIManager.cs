using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject gameWinPanel;
    public GameObject levelFailPanel;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ShowGameWin()
    {
        if (gameWinPanel != null)
            gameWinPanel.SetActive(true);
    }

    public void ShowLevelFail()
    {
        if (levelFailPanel != null)
            levelFailPanel.SetActive(true);
    }
}
