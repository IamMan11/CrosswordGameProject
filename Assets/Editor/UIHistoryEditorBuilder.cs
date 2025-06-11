
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

public class UIHistoryBuilder
{
    [MenuItem("Tools/Create UIHistory Layout")]
    public static void BuildLayout()
    {
        GameObject canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject panel = CreateUI("UIHistoryPanel", canvas.transform);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Graph Panel
        GameObject graphPanel = CreateUI("GraphPanel", panel.transform);
        var graphRT = graphPanel.GetComponent<RectTransform>();
        graphRT.anchorMin = new Vector2(0.1f, 0.7f);
        graphRT.anchorMax = new Vector2(0.9f, 0.9f);
        graphRT.offsetMin = graphRT.offsetMax = Vector2.zero;

        // Stats Panel
        GameObject statsPanel = CreateUI("StatsPanel", panel.transform);
        var statsRT = statsPanel.GetComponent<RectTransform>();
        statsRT.anchorMin = new Vector2(0.1f, 0.55f);
        statsRT.anchorMax = new Vector2(0.9f, 0.68f);
        statsRT.offsetMin = statsRT.offsetMax = Vector2.zero;

        GameObject statsTextObj = new GameObject("StatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statsTextObj.transform.SetParent(statsPanel.transform, false);
        var statsText = statsTextObj.GetComponent<TextMeshProUGUI>();
        statsText.text = "สรุปสถิติของผู้เล่น";
        statsText.fontSize = 20;
        statsText.alignment = TextAlignmentOptions.TopLeft;
        statsText.rectTransform.anchorMin = Vector2.zero;
        statsText.rectTransform.anchorMax = Vector2.one;
        statsText.rectTransform.offsetMin = statsText.rectTransform.offsetMax = Vector2.zero;

        // History Panel
        GameObject historyPanel = CreateUI("HistoryPanel", panel.transform);
        var histRT = historyPanel.GetComponent<RectTransform>();
        histRT.anchorMin = new Vector2(0.1f, 0.1f);
        histRT.anchorMax = new Vector2(0.9f, 0.5f);
        histRT.offsetMin = histRT.offsetMax = Vector2.zero;

        Debug.Log("✔ สร้าง UIHistory Layout สำเร็จแล้วใน Scene ปัจจุบัน");
    }

    static GameObject CreateUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.05f); // บางๆ

        return go;
    }
}
#endif
