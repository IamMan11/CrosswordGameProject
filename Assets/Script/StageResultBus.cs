using UnityEngine;

public static class StageResultBus
{
    // ผลลัพธ์ล่าสุด (ไว้ให้ Shop อ่านโชว์)
    public static StageResult? LastResult;

    // บอกว่าเมื่อกลับมาซีนเกม ให้เริ่มที่เลเวลไหน
    public static int NextLevelIndex = -1;

    // ชื่อซีนเกม (เอาไว้กลับจาก Shop)
    public static string GameplaySceneName = "";

    public static bool HasPendingNextLevel => NextLevelIndex >= 0;

    public static void ClearNextLevelFlag() => NextLevelIndex = -1;
}
