// ==== BgmPlayer.cs ====  (แทนไฟล์เดิมหรือปรับเพิ่มเฉพาะส่วนที่ต่าง)
using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public enum BgmTier { Base, Mid, High }

public class BgmPlayer : MonoBehaviour {
    public static BgmPlayer I { get; private set; }

    [Header("Mixer Routing")]
    public AudioMixerGroup outputGroup;

    [Header("Clips by Tier")]
    public AudioClip baseClip;
    public AudioClip midClip;
    public AudioClip highClip;

    [Header("Local Gain (under Mixer)")]
    [Range(0f,1f)] public float localGain = 1f;
    [Range(0f,5f)] public float crossfade = 1.5f;
    public bool playOnStart = true;

    // internal
    AudioSource a, b;   // dual sources for crossfade
    bool aActive = true;
    public BgmTier CurrentTier { get; private set; } = BgmTier.Base;

    void Awake() {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        a = gameObject.AddComponent<AudioSource>();
        b = gameObject.AddComponent<AudioSource>();
        SetupSrc(a);
        SetupSrc(b);

        DontDestroyOnLoad(gameObject);   // ✅ อยู่ข้ามซีน
    }

    void SetupSrc(AudioSource s) {
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;
        s.volume = 0f; // เราจะคุมผ่าน crossfade
        if (outputGroup) s.outputAudioMixerGroup = outputGroup;
    }

    void Start() {
        if (playOnStart && baseClip) {
            SetTier(BgmTier.Base, immediate:true);
        }
    }

    AudioSource Active()   => aActive ? a : b;
    AudioSource Inactive() => aActive ? b : a;

    AudioClip ClipOf(BgmTier t)
        => t == BgmTier.High ? highClip : (t == BgmTier.Mid ? midClip : baseClip);

    public void SetTier(BgmTier tier, bool immediate = false) {
        var next = ClipOf(tier);
        if (next == null) return;

        var act = Active();
        var ina = Inactive();

        // ✅ ถ้าอยู่ tier นี้และ clip เดิมกำลังเล่นอยู่แล้ว → ไม่ต้องครอสเฟดซ้ำ
        if (tier == CurrentTier && act != null && act.clip == next && act.isPlaying) {
            act.volume = localGain;     // เผื่อเคยลดไว้
            if (ina) ina.volume = 0f;
            return;
        }
        // เผื่อกำลัง crossfade ไป clip นี้อยู่แล้ว
        if (tier == CurrentTier && ina != null && ina.clip == next && ina.isPlaying) {
            return;
        }

        if (immediate || !act.isPlaying) {
            act.Stop(); ina.Stop();
            act.clip = next;
            act.volume = localGain;
            act.Play();
            aActive = (act == a);
        } else {
            StopAllCoroutines();
            StartCoroutine(CrossfadeTo(next, crossfade));
        }
        CurrentTier = tier;
    }

    IEnumerator CrossfadeTo(AudioClip nextClip, float dur) {
        var from = Active();
        var to   = Inactive();

        // prepare "to"
        to.clip = nextClip;
        to.volume = 0f;
        to.Play();

        float t = 0f;
        dur = Mathf.Max(0.0001f, dur);
        while (t < 1f) {
            t += Time.unscaledDeltaTime / dur;
            float k = Mathf.Clamp01(t);
            from.volume = Mathf.Lerp(localGain, 0f, k);
            to.volume   = Mathf.Lerp(0f, localGain, k);
            yield return null;
        }
        from.volume = 0f; from.Stop();
        to.volume   = localGain;
        aActive = (to == a);
    }

    // เผื่อปรับความดังเฉพาะ BGM runtime
    public void SetLocalGain(float v) {
        localGain = Mathf.Clamp01(v);
        var act = Active();
        if (act && act.isPlaying) act.volume = localGain;
    }
}
