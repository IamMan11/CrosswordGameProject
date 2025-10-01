using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverSfx : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public AudioSource audioSource;   // ตั้งเป็น AudioSource กลาง
    public AudioClip hoverClip;
    public AudioClip clickClip;       // ไม่ใช้ก็เว้นได้
    [Range(0f,1f)] public float volume = 1f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (audioSource != null && hoverClip != null)
            audioSource.PlayOneShot(hoverClip, volume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (audioSource != null && clickClip != null)
            audioSource.PlayOneShot(clickClip, volume);
    }
}
