using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialToolsEditor : EditorWindow
{
    [MenuItem("Tools/Tutorial Tools")]
    public static void ShowWindow()
    {
        GetWindow<TutorialToolsEditor>("Tutorial Tools");
    }

    void OnGUI()
    {
        GUILayout.Label("FTU / Tutorial Utilities", EditorStyles.boldLabel);

        // Jump / Next step controls (Play mode only)
        GUILayout.Space(8);
        GUILayout.Label("Runtime Tutorial Controls", EditorStyles.boldLabel);
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use runtime tutorial controls.", MessageType.Info);
        }
        else
        {
            if (GUILayout.Button("Find TutorialManager Instance"))
            {
                var tm = TutorialManager.Instance;
                if (tm != null) Selection.activeObject = tm.gameObject;
                else Debug.LogWarning("No running TutorialManager instance.");
            }

            // show current step info if possible
            var inst = TutorialManager.Instance;
            int stepCount = 0;
            int currentStep = -1;
            if (inst != null)
            {
                stepCount = inst.steps != null ? inst.steps.Length : 0;
                // use reflection to read private field stepIndex? It's internal field - try via property if available
            }

            EditorGUILayout.LabelField($"Steps: {stepCount}");

            int jumpIndex = EditorGUILayout.IntField("Jump to step index", 0);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Jump to Step"))
            {
                if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play Mode to jump steps."); }
                else
                {
                    var tm = TutorialManager.Instance;
                    if (tm != null) tm.StartTutorialAt(jumpIndex);
                    else Debug.LogWarning("No TutorialManager instance found.");
                }
            }
            if (GUILayout.Button("Next Step"))
            {
                if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play Mode to advance steps."); }
                else
                {
                    var tm = TutorialManager.Instance;
                    if (tm != null) tm.NextStep();
                    else Debug.LogWarning("No TutorialManager instance found.");
                }
            }
            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Reset FTU (PlayerPrefs)"))
        {
            if (EditorUtility.DisplayDialog("Reset FTU", "ยืนยันการรีเซ็ต FTU ทั้งหมด?", "รีเซ็ต", "ยกเลิก"))
            {
                // call static reset
                FtuFlowController.ResetFTUAndTutorials();
                Debug.Log("[TutorialTools] FTU reset (registered tutorial keys cleared).");
            }
        }

        if (GUILayout.Button("Mark current scene tutorial as done"))
        {
            if (EditorUtility.DisplayDialog("Mark Done", "ทำเครื่องหมายว่าฉากปัจจุบันผ่าน tutorial แล้ว?", "ทำ", "ยกเลิก"))
            {
                string key = "TUT_START_" + SceneManager.GetActiveScene().name + "_v1";
                PlayerPrefs.SetInt(key, 1);
                // register so reset can find it
                var tm = FindObjectOfType<TutorialManager>();
                if (tm != null) tm.RegisterPrefsKey(key);
                PlayerPrefs.Save();
                Debug.Log($"[TutorialTools] Marked {key} as done.");
            }
        }

        if (GUILayout.Button("Open TutorialManager in Scene"))
        {
            var tm = FindObjectOfType<TutorialManager>();
            if (tm != null) Selection.activeObject = tm.gameObject;
            else Debug.LogWarning("No TutorialManager found in scene.");
        }
    }
}


