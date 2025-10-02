using UnityEngine;
using TMPro;

public class LevelTaskUI : MonoBehaviour
{
    public static LevelTaskUI I { get; private set; }

    [Header("Rows")]
    public TMP_Text garbledText;   // "Garbled: a/b"
    public TMP_Text itWordText;    // "ITWord: a/b"
    public TMP_Text requestText;   // "WordRequest: a/b"
    public TMP_Text triangleText;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    void OnEnable() => Refresh();

    public void Refresh()
    {
        var lm = LevelManager.Instance;
        var cfg = lm?.currentLevelConfig;

        // --- Garbled ---
        var gi = Level1GarbledIT.Instance;
        if (garbledText)
        {
            if (gi != null && gi.IsActive)
            {
                garbledText.gameObject.SetActive(true);
                garbledText.text = $"Garbled: {gi.SolvedSets}/{gi.TotalSets}";
            }
            else garbledText.gameObject.SetActive(false);
        }

        // --- IT Word (ด่าน 1) ---
        if (itWordText)
        {
            bool active = lm != null && cfg != null && cfg.levelIndex == 1 && lm.GetITWordsTargetLevel1() > 0;
            itWordText.gameObject.SetActive(active);
            if (active)
                itWordText.text = $"ITWord: {lm.GetITWordsFoundCount()}/{lm.GetITWordsTargetLevel1()}";
        }

        // --- Word Request (ถ้ามีใช้) ---
        if (requestText)
        {
            bool active = lm != null && lm.IsWordRequestObjectiveActive();
            requestText.gameObject.SetActive(active);
            if (active)
            {
                var (done, target) = lm.GetWordRequestProgress();
                requestText.text = $"WordRequest: {done}/{target}";
            }
        }
        if (triangleText)
        {
            bool active = lm != null && cfg != null && cfg.levelIndex == 2 &&
                        (Level2Controller.Instance?.L2_useTriangleObjective ?? false);
            triangleText.gameObject.SetActive(active);
            if (active)
            {
                var (linked, total) = lm.GetTriangleLinkProgress();
                triangleText.text = $"จุดที่เชื่อม: {linked}/{total}";
            }
        }
    }
}
