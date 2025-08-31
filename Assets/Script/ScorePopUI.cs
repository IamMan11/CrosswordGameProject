using UnityEngine;
using TMPro;

public class ScorePopUI : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI text;
    public CanvasGroup group;
    public Animator anim;

    [Header("Fallback Bounce (no Animator)")]
    public float bounceDur = 0.12f;
    public float scaleS = 1.06f;
    public float scaleM = 1.12f;
    public float scaleL = 1.22f;

    Vector3 _baseScale;

    void Awake()
    {
        if (!text)  text  = GetComponentInChildren<TextMeshProUGUI>(true);
        if (!group) group = GetComponent<CanvasGroup>();
        if (!anim)  anim  = GetComponent<Animator>();
        _baseScale = transform.localScale;
        if (group) group.alpha = 1f;

        // ✅ ให้แอนิเมชันเล่นได้แม้ timeScale = 0 (เราใช้ตอนล็อกฉากนับคะแนน)
        if (anim)
        {
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }
    public void SetText(string s) {
    if (text) text.text = s;
    }

    public void SetValue(int v) => text.text = v.ToString();

    // เลือกระดับเด้งจาก "ค่าเพิ่มครั้งนี้"
    public void PopByDelta(int delta, int tier2Min, int tier3Min)
    {
        string trig = delta >= tier3Min ? "PopL" : (delta >= tier2Min ? "PopM" : "PopS");
        if (anim && HasTrigger(anim, trig)) { anim.ResetTrigger(trig); anim.SetTrigger(trig); }
        else StartCoroutine(FallbackBounce(trig));
    }

    bool HasTrigger(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
    }

    System.Collections.IEnumerator FallbackBounce(string tier)
    {
        float dur = Mathf.Max(0.06f, bounceDur);
        float target = tier=="PopL" ? scaleL : (tier=="PopM" ? scaleM : scaleS);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float k = t < .5f ? t/.5f : 1f-(t-.5f)/.5f; // up then down
            transform.localScale = Vector3.LerpUnclamped(_baseScale, _baseScale*target, k);
            yield return null;
        }
        transform.localScale = _baseScale;
    }

    // ลอยไปยังปลายทาง (ตำแหน่ง UI) พร้อมย่อและเฟด
    public System.Collections.IEnumerator FlyTo(RectTransform target, float dur)
    {
        var rt = (RectTransform)transform;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = rt.parent.InverseTransformPoint(target.position);
        Vector3 startScale = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.001f, dur);
            float e = EaseOutCubic(t);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, e);
            transform.localScale = Vector3.LerpUnclamped(startScale, startScale*0.2f, e);
            if (group) group.alpha = 1f - e;
            yield return null;
        }
        if (group) group.alpha = 0f;
        Destroy(gameObject);
    }
    float EaseOutCubic(float x) => 1 - Mathf.Pow(1 - x, 3);
}
