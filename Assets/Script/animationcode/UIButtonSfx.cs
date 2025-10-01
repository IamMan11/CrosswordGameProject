// UIButtonSfx.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIButtonSfx : MonoBehaviour,
    IPointerEnterHandler, IPointerDownHandler
{
    [Header("SFX")]
    public SfxId hoverSfx = SfxId.UI_Hover;
    public SfxId clickSfx = SfxId.UI_Click;

    [Header("Options")]
    [Tooltip("ข้ามการเล่นเสียงถ้าปุ่มไม่ Interactable")]
    public bool ignoreWhenNotInteractable = true;
    [Tooltip("เล่นเสียงคลิกตอนกดลง (PointerDown) ครั้งเดียว")]
    public bool playClickOnDown = true;

    Selectable selectable;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    bool Usable()
    {
        return !(ignoreWhenNotInteractable && selectable && !selectable.interactable);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (!Usable()) return;
        SfxPlayer.Play(hoverSfx);   // ✅ ครั้งเดียวตอนชี้เข้า
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!Usable()) return;
        if (playClickOnDown)
            SfxPlayer.Play(clickSfx); // ✅ ครั้งเดียวตอนกดลง ไม่สแปม
    }
}
