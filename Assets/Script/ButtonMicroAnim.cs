using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonMicroAnim : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Targets")]
    [SerializeField] RectTransform target;   // ไม่ตั้ง = ใช้ตัวเอง
    [SerializeField] Image targetImage;      // ถ้าอยากทินต์สี

    [Header("Scales")]
    [SerializeField] float normalScale = 1f;
    [SerializeField] float hoverScale = 1.05f;
    [SerializeField] float pressedScale = 0.97f;

    [Header("Durations")]
    [SerializeField] float hoverDuration = 0.12f;
    [SerializeField] float pressDuration = 0.06f;

    [Header("Optional Color Tint")]
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color hoverColor = Color.white;
    [SerializeField] Color pressedColor = Color.white;

    Coroutine scaleCo, colorCo;
    float currentTargetScale;

    void Reset() {
        target = GetComponent<RectTransform>();
        targetImage = GetComponent<Image>();
    }

    void Awake() {
        if (!target) target = GetComponent<RectTransform>();
        SetScaleImmediate(normalScale);
        if (targetImage) targetImage.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData e)  => ToState(hoverScale, hoverColor, hoverDuration);
    public void OnPointerExit(PointerEventData e)   => ToState(normalScale, normalColor, hoverDuration * 0.9f);
    public void OnPointerDown(PointerEventData e)   => ToState(pressedScale, pressedColor, pressDuration);
    public void OnPointerUp(PointerEventData e)     => ToState(hoverScale, hoverColor, pressDuration * 1.2f);

    void ToState(float scale, Color tint, float dur) {
        currentTargetScale = scale;
        if (scaleCo != null) StopCoroutine(scaleCo);
        scaleCo = StartCoroutine(AnimateScale(scale, dur));
        if (targetImage) {
            if (colorCo != null) StopCoroutine(colorCo);
            colorCo = StartCoroutine(AnimateColor(tint, dur));
        }
    }

    IEnumerator AnimateScale(float to, float dur) {
        float t = 0f;
        Vector3 from = target.localScale;
        Vector3 toV = Vector3.one * to;
        while (t < dur) {
            t += Time.unscaledDeltaTime;
            float k = EaseOutQuad(Mathf.Clamp01(t / dur));
            target.localScale = Vector3.LerpUnclamped(from, toV, k);
            yield return null;
        }
        target.localScale = toV;
    }
    IEnumerator AnimateColor(Color to, float dur) {
        float t = 0f;
        Color from = targetImage.color;
        while (t < dur) {
            t += Time.unscaledDeltaTime;
            float k = EaseOutQuad(Mathf.Clamp01(t / dur));
            targetImage.color = Color.LerpUnclamped(from, to, k);
            yield return null;
        }
        targetImage.color = to;
    }
    float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
    void SetScaleImmediate(float s) => target.localScale = Vector3.one * s;
}
