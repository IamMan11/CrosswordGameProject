using System;
using UnityEngine;

public class AlphabetPicker : MonoBehaviour
{
    public static AlphabetPicker Instance;

    [Header("UI")]
    public GameObject panel;     // พาเนลเลือกตัวอักษร (ซ่อนตอนเริ่ม)
    public GameObject blocker;   // ถ้าไม่ตั้งค่า จะพยายามใช้ TurnManager.Instance.inputBlocker

    private Action<char> onPicked;
    private bool prevBlockerActive;

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
    }

    public static void Show(Action<char> cb)
    {
        if (Instance == null || Instance.panel == null) return;

        Instance.onPicked = cb;

        // ใช้ inputBlocker ของ TurnManager ถ้าไม่ได้เซ็ตใน Inspector
        if (Instance.blocker == null && TurnManager.Instance != null)
            Instance.blocker = TurnManager.Instance.inputBlocker;

        // เปิดตัวกันคลิก (จำสถานะเดิมไว้ เพื่อคืนค่าทีหลัง)
        if (Instance.blocker != null)
        {
            Instance.prevBlockerActive = Instance.blocker.activeSelf;
            Instance.blocker.SetActive(true);
        }

        Instance.panel.SetActive(true);
    }

    // ผูกกับปุ่ม A..Z (ส่ง "A","B",...)
    public void PickLetter(string s)
    {
        char ch = string.IsNullOrEmpty(s) ? 'A' : char.ToUpperInvariant(s[0]);
        onPicked?.Invoke(ch);
        Close();
    }

    public void Close()
    {
        if (panel) panel.SetActive(false);

        // คืนค่าตัวกันคลิกให้กลับสภาพเดิม (ถ้าก่อนหน้าไม่ได้เปิดไว้ เราจะปิดให้)
        if (blocker != null) blocker.SetActive(prevBlockerActive);

        onPicked = null;
    }
}
