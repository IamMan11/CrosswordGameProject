
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIHistoryEnhancer
{
    [MenuItem("Tools/Enhance UIHistory Layout")]
    public static void EnhanceLayout()
    {
        GameObject viewer = GameObject.Find("UIHistoryPanel");
        if (viewer == null)
        {
            Debug.LogError("❌ ไม่พบ GameObject 'UIHistoryPanel'");
            return;
        }

        // === สร้าง ScrollView สำหรับ historyContainer ===
        GameObject historyPanel = GameObject.Find("HistoryPanel");
        if (historyPanel != null && historyPanel.GetComponentInChildren<ScrollRect>() == null)
        {
            GameObject scrollGO = new GameObject("HistoryScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGO.transform.SetParent(historyPanel.transform, false);
            RectTransform rt = scrollGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewport.transform.SetParent(scrollGO.transform, false);
            var vpRT = viewport.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.05f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("HistoryContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;

            ScrollRect sr = scrollGO.GetComponent<ScrollRect>();
            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.content = content.GetComponent<RectTransform>();
            sr.horizontal = false;
            sr.vertical = true;

            Debug.Log("✅ เพิ่ม ScrollView เรียบร้อย (HistoryContent)");
        }

        // === เพิ่ม LineRenderer ใน GraphPanel ===
        GameObject graphPanel = GameObject.Find("GraphPanel");
        if (graphPanel != null && graphPanel.GetComponentInChildren<LineRenderer>() == null)
        {
            GameObject lineGO = new GameObject("LineGraph", typeof(LineRenderer));
            lineGO.transform.SetParent(graphPanel.transform, false);
            LineRenderer lr = lineGO.GetComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = new Material(Shader.Find("UI/Default"));
            lr.widthMultiplier = 4f;
            lr.positionCount = 0;
            lr.alignment = LineAlignment.TransformZ;

            Debug.Log("✅ เพิ่ม LineRenderer สำหรับกราฟคะแนนแล้ว");
        }

        Debug.Log("✔ เสร็จสิ้นการเสริม UI Layout");
    }
}
#endif
