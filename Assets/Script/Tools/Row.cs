#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class Row : EditorWindow
{
    float spacing = 1.2f;

    [MenuItem("Tools/Align In Row %#r")]
    public static void ShowWindow()
    {
        GetWindow<Row>("Align In Row");
    }

    void OnGUI()
    {
        GUILayout.Label("จัดเรียง Object ในแนวนอน", EditorStyles.boldLabel);
        spacing = EditorGUILayout.FloatField("ระยะห่าง (Spacing)", spacing);

        if (GUILayout.Button("จัดเรียง (Align Selected)"))
        {
            AlignSelected();
        }
    }

    void AlignSelected()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length == 0)
        {
            Debug.LogWarning("กรุณาเลือก Object ก่อนจัดเรียง");
            return;
        }

        // เรียงจากซ้ายไปขวา
        System.Array.Sort(selected, (a, b) => a.name.CompareTo(b.name));

        Vector3 startPos = selected[0].transform.position;

        for (int i = 0; i < selected.Length; i++)
        {
            Vector3 newPos = startPos + new Vector3(i * spacing, 0, 0);
            Undo.RecordObject(selected[i].transform, "Align In Row");
            selected[i].transform.position = newPos;
        }

        Debug.Log("จัดเรียงเรียบร้อยแล้ว!");
    }
}
#endif
