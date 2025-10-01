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

    // === NEW: จัดการเลเยอร์/บล็อกเกอร์ ===
    Canvas ownCanvas;                      // canvas ของป๊อปอัพเอง (ให้เด้งขึ้นบนสุด)
    GraphicRaycaster ownRaycaster;         // ให้รับคลิกแน่นอน
    bool prevBlockerActive = false;        // จำสถานะ inputBlocker เดิมไว้

    private Action<char> onConfirm;

    private void Awake()
    {
        if (Instance == null) Instance = this; else { Destroy(gameObject); return; }

        // --- ตั้งค่า Input ---
        inputField.characterLimit = 1;
        inputField.contentType    = TMP_InputField.ContentType.Alphanumeric;
        inputField.pointSize      = 36;

        btnConfirm.onClick.AddListener(OnClickConfirm);
        btnCancel .onClick.AddListener(Hide);

        // === NEW: สร้าง/ตั้งค่า Canvas ของตัวเองให้ “อยู่บนสุดและรับคลิก” ===
        ownCanvas = GetComponent<Canvas>();
        if (!ownCanvas) ownCanvas = gameObject.AddComponent<Canvas>();
        ownCanvas.overrideSorting = true;
        ownCanvas.sortingOrder    = 5000;            // สูงกว่า UI อื่นทั่วไป

        ownRaycaster = GetComponent<GraphicRaycaster>();
        if (!ownRaycaster) ownRaycaster = gameObject.AddComponent<GraphicRaycaster>();

        gameObject.SetActive(false);
    }

    /// <summary>เปิด Popup</summary>
    public static void Show(Action<char> callback)
    {
        // ดันป๊อปอัพไว้บนสุดของ Canvas เดียวกัน (เผื่อ hierarchy มีพี่น้องหลายตัว)
        Instance.transform.SetAsLastSibling();

        // === NEW: ถ้ามี inputBlocker ของ TurnManager เปิดอยู่ ให้ปิดชั่วคราว ===
        var tm = TurnManager.Instance;
        if (tm && tm.inputBlocker)
        {
            Instance.prevBlockerActive = tm.inputBlocker.activeSelf;
            tm.inputBlocker.SetActive(false);   // กันเคสบล็อกเกอร์กินเรคาสท์ทับป๊อปอัพ
        }

        UiGuard.Push();                         // กันอินพุตอื่นชั่วคราว (คุณมีคลาสนี้อยู่แล้ว) :contentReference[oaicite:0]{index=0}

        Instance.onConfirm = callback;
        Instance.gameObject.SetActive(true);

        // โฟกัสช่องกรอก
        Instance.inputField.text = "";
        Instance.inputField.ActivateInputField();
        Instance.inputField.Select();
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

        // === NEW: คืนสถานะ inputBlocker กลับ ===
        var tm = TurnManager.Instance;
        if (tm && tm.inputBlocker) tm.inputBlocker.SetActive(prevBlockerActive);

        UiGuard.Pop();
    }
}
