using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ป๊อปอัพยืนยัน (ข้อความ + ปุ่ม Confirm/Cancel)
/// </summary>
[DisallowMultipleComponent]
public class UIConfirmPopup : MonoBehaviour
{
    public static UIConfirmPopup Instance;   // singleton

    [SerializeField] private TMP_Text msgText;
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;

    private Action onConfirm;

    void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }
        gameObject.SetActive(false);

        if (btnConfirm != null)
            btnConfirm.onClick.AddListener(() => { onConfirm?.Invoke(); Hide(); });

        if (btnCancel != null)
            btnCancel.onClick.AddListener(Hide);
    }

    /// <summary>แสดงป๊อปอัพข้อความ พร้อม callback ยืนยัน/ยกเลิก</summary>
    public static void Show(string msg, Action confirm, Action cancel = null)
    {
        if (Instance == null) return;

        Instance.onConfirm = confirm;
        Instance.gameObject.SetActive(true);

        if (Instance.msgText) Instance.msgText.text = msg;

        if (Instance.btnCancel != null)
        {
            Instance.btnCancel.onClick.RemoveAllListeners();
            Instance.btnCancel.onClick.AddListener(() =>
            {
                cancel?.Invoke();
                Instance.Hide();
            });
        }

        if (Instance.btnConfirm != null)
        {
            Instance.btnConfirm.onClick.RemoveAllListeners();
            Instance.btnConfirm.onClick.AddListener(() =>
            {
                Instance.onConfirm?.Invoke();
                Instance.Hide();
            });
        }
    }

    private void Hide() => gameObject.SetActive(false);
}
