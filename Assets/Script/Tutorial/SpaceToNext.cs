using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// กด Spacebar เพื่อกดปุ่ม Next (เฉพาะตอนที่ Next โชว์)
public class SpaceToNext : MonoBehaviour
{
    public TutorialManager tutorial;
    public TextMeshProUGUI promptTMP;

    void Update()
    {
        if (tutorial == null) return;
        var f = typeof(TutorialManager).GetField("nextButton",
                 System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var btn = f?.GetValue(tutorial) as Button;
        if (btn == null) return;

        if (promptTMP) promptTMP.enabled = btn.gameObject.activeInHierarchy;
        if (btn.gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.Space))
            btn.onClick?.Invoke();
    }
}