#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TutorialDevUtils
{
    [MenuItem("Tools/Tutorial/Clear Seen Flag (TUTORIAL_SEEN)")]
    public static void ClearSeenFlag()
    {
        PlayerPrefs.DeleteKey("TUTORIAL_SEEN"); // ถ้าคุณเปลี่ยนคีย์ ให้แก้ตรงนี้
        PlayerPrefs.Save();
        Debug.Log("✅ Deleted PlayerPrefs key: TUTORIAL_SEEN");
    }

    [MenuItem("Tools/Tutorial/Clear ALL PlayerPrefs (Danger)")]
    public static void ClearAll()
    {
        if (EditorUtility.DisplayDialog("Clear ALL PlayerPrefs?",
            "ลบคีย์ทั้งหมดของโปรเจกต์นี้เลยนะ แน่ใจหรือไม่?", "Yes, clear all", "Cancel"))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("✅ Deleted ALL PlayerPrefs keys.");
        }
    }
}
#endif
