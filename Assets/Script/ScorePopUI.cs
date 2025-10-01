using UnityEngine;
using TMPro;

public class ScorePopUI : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI text;
    public CanvasGroup group;
    public Animator anim;
    [Header("Animator Params")]
    public string popSParam = "PopS";
    public string popMParam = "PopM";
    public string popLParam = "PopL";

    [Header("Colors")]
    public Color colorLetters = new Color(1f, 0.92f, 0.35f, 1f); // A: เหลืองทอง
    public Color colorMults   = new Color(0.40f, 0.85f, 1f, 1f);  // B: ฟ้า
    public Color colorTotal   = new Color(0.58f, 1f, 0.65f, 1f);  // Total: เขียว
    [Header("Fallback Bounce (no Animator)")]
    public float bounceDur = 0.12f;
    public float scaleS = 1.06f;
    public float scaleM = 1.12f;
    public float scaleL = 1.22f;

    Vector3 _baseScale;

    void Awake()
    {
        if (anim) anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (!text) text = GetComponentInChildren<TextMeshProUGUI>(true);
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
    
    public void SetColor(Color c)
    {
        if (text) text.color = c;
    }

    public void SetValue(int v) => text.text = v.ToString();

    // เลือกระดับเด้งจาก "ค่าเพิ่มครั้งนี้"
    public void PopByDelta(int delta, int tier2Min, int tier3Min)
    {
        // ตัดสิน tier ตามขนาด delta
        string param = popSParam;
        if (Mathf.Abs(delta) >= tier3Min) param = popLParam;
        else if (Mathf.Abs(delta) >= tier2Min) param = popMParam;

        TriggerPop(param);
    }

    bool HasTrigger(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
    }
    public void TriggerPop(string param)
    {
        if (anim)
        {
            // สุ่มเอียงซ้าย/ขวาเล็กน้อยก่อนเล่นคลิป เพื่อให้หลากหลาย (คลิปยังคุม scale ได้ตามปกติ)
            var rt = transform as RectTransform;
            float tilt = Random.value < 0.5f ? -6f : 6f;     // +-6 องศา
            rt.localRotation = Quaternion.Euler(0, 0, tilt);

            // เคลียร์ Trigger อื่นกันสะสม
            if (!string.IsNullOrEmpty(popSParam)) anim.ResetTrigger(popSParam);
            if (!string.IsNullOrEmpty(popMParam)) anim.ResetTrigger(popMParam);
            if (!string.IsNullOrEmpty(popLParam)) anim.ResetTrigger(popLParam);

            anim.SetTrigger(param);
        }
        else
        {
            // ถ้าไม่มี Animator → ใช้เด้งแบบโค้ดที่มีอยู่ (เช่น StartCoroutine(FallbackBounce(...)))
            // เรียกวิธีเดิมของคุณที่ใช้เด้ง fallback (คงมีอยู่แล้วในไฟล์นี้)
            // ตัวอย่าง: StartCoroutine(FallbackBounce(param == popLParam ? "PopL" : param == popMParam ? "PopM" : "PopS"));
        }
    }

    System.Collections.IEnumerator FallbackBounce(string tier)
    {
        float dur = Mathf.Max(0.06f, bounceDur);
        float target = tier=="PopL" ? scaleL : (tier=="PopM" ? scaleM : scaleS);
        float t = 0f;
        while (t < 1f)
        {
            while (PauseManager.IsPaused) { yield return null; }
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
            while (PauseManager.IsPaused) { yield return null; }
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
    // ===== Total Wave FX (TMP sine-per-digit) =====
    public System.Collections.IEnumerator WaveDigits(
        float amplitude = 24f,     // ระยะแกว่งแนวตั้ง (พิกเซล)
        float charPhase = 0.55f,   // ระยะเฟสระหว่างตัวอักษรถัดไป (เรเดียน)
        float speed     = 7f,      // ความเร็วเดินคลื่น (เรเดียน/วินาที)
        float holdTail  = 0.15f,   // หน่วงให้เห็นหลังจบคลื่น
        float settleDur = 0.20f    // เวลาเก็บกลับให้นิ่ง
    )
    {
        if (text == null) yield break;
        text.ForceMeshUpdate();
        var ti = text.textInfo;
        int charCount = ti.characterCount;
        if (charCount <= 0) yield break;

        // เก็บตำแหน่ง vertex ต้นฉบับไว้เป็นฐาน
        var cached = new Vector3[ti.meshInfo.Length][];
        for (int m = 0; m < ti.meshInfo.Length; m++)
            cached[m] = (Vector3[])ti.meshInfo[m].vertices.Clone();

        // ให้คลื่นเริ่มจากตัวซ้ายสุด เคลื่อนไปจนขวาสุดครบหนึ่งคาบ
        float phase = 0f;
        float endPhase = Mathf.Max(0f, (charCount - 1) * charPhase) + (Mathf.PI * 2f);

        while (phase < endPhase)
        {
            // เริ่มจากฐานทุกเฟรม
            for (int m = 0; m < ti.meshInfo.Length; m++)
                System.Array.Copy(cached[m], ti.meshInfo[m].vertices, ti.meshInfo[m].vertices.Length);

            // ขยับทีละตัว
            for (int i = 0; i < charCount; i++)
            {
                var ci = ti.characterInfo[i];
                if (!ci.isVisible) continue;

                int mi = ci.materialReferenceIndex;
                int vi = ci.vertexIndex;
                var verts = ti.meshInfo[mi].vertices;

                float y = Mathf.Sin(phase - i * charPhase) * amplitude;
                verts[vi + 0].y += y;
                verts[vi + 1].y += y;
                verts[vi + 2].y += y;
                verts[vi + 3].y += y;
            }

            // อัปเดตเมช
            for (int m = 0; m < ti.meshInfo.Length; m++)
            {
                var mi = ti.meshInfo[m];
                mi.mesh.vertices = mi.vertices;
                text.UpdateGeometry(mi.mesh, m);
            }

            phase += Time.unscaledDeltaTime * speed;
            yield return null;
        }

        // หน่วงให้เห็นค่าชัด ๆ
        if (holdTail > 0f) yield return new WaitForSecondsRealtime(holdTail);

        // เก็บกลับให้นิ่ง (ease สั้น ๆ)
        float t = 0f;
        while (t < 1f)
        {
            while (PauseManager.IsPaused) { yield return null; }
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, settleDur);
            float k = 1f - t; // 1 -> 0

            // ไล่ลดความแกว่งลงสู่ฐาน
            for (int m = 0; m < ti.meshInfo.Length; m++)
            {
                var verts = ti.meshInfo[m].vertices;
                for (int v = 0; v < verts.Length; v++)
                    verts[v] = Vector3.Lerp(verts[v], cached[m][v], 1f);
            }
            for (int m = 0; m < ti.meshInfo.Length; m++)
            {
                var mi = ti.meshInfo[m];
                mi.mesh.vertices = mi.vertices;
                text.UpdateGeometry(mi.mesh, m);
            }
            yield return null;
        }

        // รีเซ็ตเป็นฐาน 100%
        for (int m = 0; m < ti.meshInfo.Length; m++)
        {
            var verts = ti.meshInfo[m].vertices;
            System.Array.Copy(cached[m], verts, verts.Length);
            ti.meshInfo[m].mesh.vertices = verts;
            text.UpdateGeometry(ti.meshInfo[m].mesh, m);
        }
    }
    float EaseOutCubic(float x) => 1 - Mathf.Pow(1 - x, 3);
}
