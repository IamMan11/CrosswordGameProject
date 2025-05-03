using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// ป็อปอัพยืนยันสองปุ่ม (Confirm / Cancel) ใช้งานผ่านเมทอดสาธารณะ
///     UIConfirmPopup.Show("ข้อความ", onConfirm, onCancel);
/// </summary>
public class UIConfirmPopup : MonoBehaviour
{
    public static UIConfirmPopup Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] GameObject panel;     // Root Panel (เปิด/ปิดที่นี่)
    [SerializeField] TMP_Text   message;
    [SerializeField] Button     btnConfirm;
    [SerializeField] Button     btnCancel;

    private Action cbConfirm;
    private Action cbCancel;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (panel != null) panel.SetActive(false);  // ซ่อนเริ่มต้น

        if (btnConfirm) btnConfirm.onClick.AddListener(OnConfirm);
        if (btnCancel)  btnCancel.onClick.AddListener(OnCancel);
    }

    // ---------------- STATIC API ----------------
    public static void Show(string msg, Action onConfirm = null, Action onCancel = null)
    {
        if (Instance == null)
        {
            Debug.LogError("[UIConfirmPopup] No instance in scene");
            onCancel?.Invoke();
            return;
        }
        Instance.ShowInternal(msg, onConfirm, onCancel);
    }

    // ---------------- INTERNAL ------------------
    void ShowInternal(string msg, Action onConfirm, Action onCancel)
    {
        if (message) message.text = msg;
        cbConfirm = onConfirm;
        cbCancel  = onCancel;
        if (panel) panel.SetActive(true);
    }

    void OnConfirm()
    {
        if (panel) panel.SetActive(false);
        cbConfirm?.Invoke();
    }

    void OnCancel()
    {
        if (panel) panel.SetActive(false);
        cbCancel?.Invoke();
    }
}
