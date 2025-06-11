using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIConfirmPopup : MonoBehaviour
{
    public static UIConfirmPopup Instance;      // singleton

    [SerializeField] private TMP_Text msgText;
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;

    private Action onConfirm;

    private void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }
        gameObject.SetActive(false);

        btnConfirm.onClick.AddListener(() => { onConfirm?.Invoke(); Hide(); });
        btnCancel. onClick.AddListener(Hide);
    }

    public static void Show(string msg, Action confirm, Action cancel = null)
    {
            Instance.onConfirm = confirm;
        Instance.gameObject.SetActive(true);
        Instance.msgText.text = msg;

        // กำหนด callback Cancel ใหม่ทุกครั้ง
        Instance.btnCancel.onClick.RemoveAllListeners();
        Instance.btnCancel.onClick.AddListener(() =>
        {
            cancel?.Invoke();
            Instance.Hide();
        });
    }

    private void Hide() => gameObject.SetActive(false);
}
