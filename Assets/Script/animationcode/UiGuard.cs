using UnityEngine;

public static class UiGuard
{
    static int busy;
    public static bool IsBusy => busy > 0;
    public static void Push() { busy++; }
    public static void Pop()  { busy = Mathf.Max(0, busy - 1); }
}
