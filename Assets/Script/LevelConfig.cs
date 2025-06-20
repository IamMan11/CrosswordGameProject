[System.Serializable]
public class LevelConfig
{
    public int levelIndex;
    public int requiredScore;
    public int requiredWords;

    public float timeLimit; // 🕒 เวลารวมของด่าน
    public bool enableAutoRemove = true; // ✅ ให้ลบอักษรอัตโนมัติ
    public float autoRemoveInterval = 10f; // ⏱ ลบทุกกี่วินาที
}
