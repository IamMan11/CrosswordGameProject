using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BlankPopup : MonoBehaviour
{
    public static BlankPopup Instance { get; private set; }

    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;

    // callback ส่งตัวอักษรกลับไป
    private Action<char> onConfirm;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        inputField.characterLimit = 1;
        inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
        inputField.pointSize = 36;   // หรือปรับขนาดตัวอักษรตามใจ
        btnConfirm.onClick.AddListener(OnClickConfirm);
        btnCancel.onClick.AddListener(() => Hide());
        gameObject.SetActive(false);
    }

    /// <summary>
    /// เรียกเมื่อจะเปิด Popup
    /// </summary>
    public static void Show(Action<char> callback)
    {
        Instance.inputField.text       = "";
        Instance.inputField.characterLimit = 1;
        Instance.inputField.ActivateInputField();
        Instance.onConfirm             = callback;
        Instance.gameObject.SetActive(true);
    }

    private void OnClickConfirm()
    {
        if (string.IsNullOrEmpty(inputField.text)) return;

        char c = char.ToUpper(inputField.text[0]);
        if (c < 'A' || c > 'Z')
        {
            UIManager.Instance.ShowMessageDictionary("กรุณาใส่ A–Z เท่านั้น");
            return;
        }

        Hide();
        onConfirm?.Invoke(c);
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
