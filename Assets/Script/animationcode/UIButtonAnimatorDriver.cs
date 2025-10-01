using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Animator))]
public class UIButtonAnimatorDriver : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Options")]
    public bool ignoreWhenNotInteractable = true;
    public bool useUnscaledTime = true;

    Animator anim;
    Selectable selectable;
    bool hovering, pressing, lastInteractable = true;

    static readonly int HashHover        = Animator.StringToHash("isHover");
    static readonly int HashPressed      = Animator.StringToHash("isPressed");
    static readonly int HashInteractable = Animator.StringToHash("Interactable");

    void Awake()
    {
        anim = GetComponent<Animator>();
        selectable = GetComponent<Selectable>();
        if (useUnscaledTime && anim) anim.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    void OnEnable()
    {
        hovering = pressing = false;
        SetBool(HashHover, false);
        SetBool(HashPressed, false);
        SyncInteractable(selectable ? selectable.interactable : true);
    }

    void Update()
    {
        if (selectable && lastInteractable != selectable.interactable)
            SyncInteractable(selectable.interactable);
    }

    void SyncInteractable(bool value)
    {
        lastInteractable = value;
        SetBool(HashInteractable, value);
        if (ignoreWhenNotInteractable && !value)
        {
            hovering = pressing = false;
            SetBool(HashHover, false);
            SetBool(HashPressed, false);
        }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        hovering = true;
        if (ignoreWhenNotInteractable && selectable && !selectable.interactable) return;
        SetBool(HashHover, true);
        if (!pressing) SetBool(HashPressed, false);
    }

    public void OnPointerExit(PointerEventData e)
    {
        hovering = false;
        if (ignoreWhenNotInteractable && selectable && !selectable.interactable) return;
        if (!pressing) SetBool(HashHover, false);
    }

    public void OnPointerDown(PointerEventData e)
    {
        pressing = true;
        if (ignoreWhenNotInteractable && selectable && !selectable.interactable) return;
        SetBool(HashPressed, true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        pressing = false;
        if (ignoreWhenNotInteractable && selectable && !selectable.interactable) return;
        SetBool(HashPressed, false);
        SetBool(HashHover, hovering);
    }

    void SetBool(int id, bool v) { if (anim) anim.SetBool(id, v); }
}
