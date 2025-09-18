using System.Collections.Generic;
using UnityEngine;

public static class TutorialAnalytics
{
    public static void Log(string eventName, Dictionary<string, object> data = null)
    {
        // Stub: replace with analytics service call
        string s = eventName;
        if (data != null)
        {
            foreach (var kv in data) s += $" {kv.Key}={kv.Value};";
        }
        Debug.Log($"[TutorialAnalytics] {s}");
    }
}


