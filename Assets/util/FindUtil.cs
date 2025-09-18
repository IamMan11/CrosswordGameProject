// Assets/Scripts/Utils/FindUtil.cs
using UnityEngine;

public static class FindUtil
{
    public static T First<T>(bool includeInactive = false) where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return includeInactive
            ? Object.FindFirstObjectByType<T>(FindObjectsInactive.Include)
            : Object.FindFirstObjectByType<T>(FindObjectsInactive.Exclude);
#else
        return Object.FindObjectOfType<T>(includeInactive); // fallback
#endif
    }

    public static T Any<T>(bool includeInactive = false) where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return includeInactive
            ? Object.FindAnyObjectByType<T>(FindObjectsInactive.Include)
            : Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude);
#else
        return Object.FindObjectOfType<T>(includeInactive); // fallback
#endif
    }
}
