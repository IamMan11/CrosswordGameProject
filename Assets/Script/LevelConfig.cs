using UnityEngine;


[System.Serializable]
public class LevelConfig
{
[Header("Display / Index")]
[Tooltip("Show number for this level (1-based).")]
public int levelIndex = 1;


[Header("Win Conditions")]
[Min(0)] public int requiredScore = 100;
[Min(0)] public int requiredWords = 5;


[Header("Level Timer (0 = no limit)")]
[Tooltip("Seconds allowed for this level. Use 0 for no time limit.")]
[Min(0f)] public float timeLimit = 120f;


// (Optional / future) ถ้าจะเปิดระบบล็อคบอร์ดค่อยเติมฟิลด์เพิ่มภายหลัง
#if UNITY_EDITOR
void OnValidate()
{
if (levelIndex < 1) levelIndex = 1;
if (requiredScore < 0) requiredScore = 0;
if (requiredWords < 0) requiredWords = 0;
if (timeLimit < 0f) timeLimit = 0f;
}
#endif
}