#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TutorialQuickFix
{
    [MenuItem("Tools/Tutorial/Fix Step0 (Next + BlockInput)")]
    public static void FixStep0()
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tm == null || tm.steps == null || tm.steps.Length == 0)
        {
            Debug.LogWarning("ไม่พบ TutorialManager หรือยังไม่มี Steps"); return;
        }
        // บังคับค่าที่จำเป็นของ Step 0
        tm.steps[0].pressNextToContinue = true;
        tm.steps[0].tapAnywhereToContinue = false;
        tm.steps[0].blockInput = true;

        // ย้าย TutorialUI ไปซ้อนบนสุด
        tm.transform.SetAsLastSibling();

        EditorUtility.SetDirty(tm);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Tutorial] Fixed Step0: pressNext=true, blockInput=true, tapAnywhere=false");
    }
}
#endif
