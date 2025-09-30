// ==== BgmPlayer.cs ====  (แทนไฟล์เดิมหรือปรับเพิ่มเฉพาะส่วนที่ต่าง)
using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public enum BgmTier { Base, Mid, High }

public class BgmPlayer : MonoBehaviour
{
    public static BgmPlayer I { get; private set; }

    [Header("Mixer Routing")]
    public AudioMixerGroup outputGroup;

    [Header("Clips by Tier")]
    public AudioClip baseClip;
    public AudioClip midClip;
    public AudioClip highClip;

    [Header("Local Gain (under Mixer)")]
    [Range(0f, 1f)] public float localGain = 1f;
    [Range(0f, 5f)] public float crossfade = 1.5f;
    public bool playOnStart = true;

    // internal
    AudioSource a, b;   // dual sources for crossfade
    bool aActive = true;
    public BgmTier CurrentTier { get; private set; } = BgmTier.Base;
    Coroutine _xfadeCo = null;
    Coroutine _duckCo = null;
    AudioClip _xfadeTarget = null;
    float _lastSwitchAt = -999f;
    [SerializeField] float _minSwitchGap = 0.10f; // กันสั่งสลับถี่เกิน
    [SerializeField] bool  preloadClips       = true;   // โหลด/ดีคอมเพรสล่วงหน้า
    [SerializeField] float prepareTimeoutSec  = 0.35f;  // รอโหลดสูงสุด
    [SerializeField] bool  usePlayScheduled   = true;   // ใช้ DSP schedule
    [SerializeField] double scheduleLeadSec   = 0.05;   // เวลานำหน้าเริ่มเล่น
    [Header("Time Low / Panic")]
    public AudioClip panicClip;    // BGM ตอนเวลาใกล้หมด
    bool panicMode = false;
    BgmTier requestedTier = BgmTier.Base; // จำ tier ล่าสุดจาก streak

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        a = gameObject.AddComponent<AudioSource>();
        b = gameObject.AddComponent<AudioSource>();
        SetupSrc(a);
        SetupSrc(b);

        DontDestroyOnLoad(gameObject);   // ✅ อยู่ข้ามซีน
    }

    void SetupSrc(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;
        s.volume = 0f; // เราจะคุมผ่าน crossfade
        if (outputGroup) s.outputAudioMixerGroup = outputGroup;
    }

    void Start()
    {
        if (preloadClips) StartCoroutine(PreloadAllCo());
        if (playOnStart && baseClip)
            SetTier(BgmTier.Base, immediate: true);
    }

    AudioSource Active() => aActive ? a : b;
    AudioSource Inactive() => aActive ? b : a;

    AudioClip ClipOf(BgmTier t)
        => t == BgmTier.High ? highClip : (t == BgmTier.Mid ? midClip : baseClip);

    public void StopImmediateAndClear()
    {
        StopAllCoroutines();
        if (a) { a.Stop(); a.clip = null; a.volume = 0f; }
        if (b) { b.Stop(); b.clip = null; b.volume = 0f; }
        CurrentTier = BgmTier.Base;
    }
    IEnumerator EnsureClipReady(AudioClip clip)
    {
        if (!clip) yield break;

    #if UNITY_2020_1_OR_NEWER
        // ถ้าคลิปยังไม่ได้โหลด ให้สั่งโหลดแล้วรอ (กันกระตุกตอน Play)
        if (!clip.preloadAudioData || clip.loadState != AudioDataLoadState.Loaded)
        {
            clip.LoadAudioData();
            double deadline = AudioSettings.dspTime + prepareTimeoutSec;
            while (clip.loadState == AudioDataLoadState.Loading && AudioSettings.dspTime < deadline)
                yield return null;
        }
    #endif
    }

    IEnumerator PreloadAllCo()
    {
        yield return EnsureClipReady(baseClip);
        yield return EnsureClipReady(midClip);
        yield return EnsureClipReady(highClip);
        yield return EnsureClipReady(panicClip);
    }
    public void SetPanicMode(bool on)
    {
        if (panicMode == on) return;
        panicMode = on;

        if (on)
        {
            if (panicClip != null)
            {
                if (_xfadeCo != null) { StopCoroutine(_xfadeCo); _xfadeCo = null; }
                _xfadeTarget = panicClip;
                _xfadeCo = StartCoroutine(CrossfadeTo(panicClip, 0.5f));
            }
            else
            {
                DuckAndStop(0.12f); // ไม่มีคลิป panic ก็เงียบไว้
            }
        }
        else
        {
            // กลับไปเล่นตาม tier ล่าสุดที่สั่งไว้
            SetTier(requestedTier, immediate:false);
        }
    }


    public void SetTier(BgmTier tier, bool immediate = false)
    {
        requestedTier = tier;
        if (panicMode) return; // ขณะ panic ให้เมินการสลับจาก streak
        var next = ClipOf(tier);
        if (next == null) return;

        var act = Active();
        var ina = Inactive();

        // กันสั่งถี่เกิน
        if (Time.unscaledTime - _lastSwitchAt < _minSwitchGap && _xfadeTarget == next) return;
        _lastSwitchAt = Time.unscaledTime;

        // ถ้าเพลงเป้าหมายกำลังเล่นอยู่แล้ว → ไม่ต้องทำอะไร
        if (tier == CurrentTier && act && act.clip == next && act.isPlaying)
        {
            act.volume = localGain;
            if (ina) ina.volume = 0f;
            return;
        }
        // ถ้ากำลัง crossfade ไปหาเป้าหมายเดียวกันอยู่ → ไม่ต้องเริ่มใหม่
        if (_xfadeTarget == next && _xfadeCo != null) return;

        // ถ้ากำลัง duck อยู่ ให้ยกเลิก (จะเริ่มเล่นเพลงใหม่)
        if (_duckCo != null) { StopCoroutine(_duckCo); _duckCo = null; }

        if (immediate || act == null || !act.isPlaying)
        {
            if (act) { act.Stop(); }
            if (ina) { ina.Stop(); }
            if (act)
            {
                act.clip = next;
                act.volume = localGain;
                act.Play();
                aActive = (act == a);
            }
        }
        else
        {
            if (_xfadeCo != null) { StopCoroutine(_xfadeCo); _xfadeCo = null; }
            _xfadeTarget = next;
            _xfadeCo = StartCoroutine(CrossfadeTo(next, crossfade));
        }
        CurrentTier = tier;
    }


    IEnumerator CrossfadeTo(AudioClip nextClip, float dur)
    {
        var from = Active();
        var to   = Inactive();

        // 1) ให้แน่ใจว่า nextClip โหลดพร้อม
        yield return EnsureClipReady(nextClip);

        // 2) เตรียมแหล่งเสียง
        to.clip = nextClip;
        to.volume = 0f;

        // 3) สตาร์ทแบบ DSP schedule เพื่อลื่นกริบ (หรือ fallback เล่นทันที)
        if (usePlayScheduled)
        {
            double startDsp = AudioSettings.dspTime + scheduleLeadSec;
            to.PlayScheduled(startDsp);

            // รอจนถึงเวลาเริ่มจริงก่อนค่อยทำ crossfade เพื่อลด dead-air/แอบกระชาก
            while (AudioSettings.dspTime < startDsp)
                yield return null;
        }
        else
        {
            to.Play();
            // กัน 1 เฟรมให้บัฟเฟอร์เริ่มเติม
            yield return null;
        }

        // 4) ค่อย ๆ crossfade
        float t = 0f;
        dur = Mathf.Max(0.0001f, dur);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float k = Mathf.Clamp01(t);
            if (from) from.volume = Mathf.Lerp(localGain, 0f, k);
            if (to)   to.volume   = Mathf.Lerp(0f, localGain, k);
            yield return null;
        }

        if (from) { from.volume = 0f; from.Stop(); }
        if (to)   { to.volume   = localGain; }
        aActive = (to == a);

        _xfadeCo = null;
        _xfadeTarget = null;
    }


    // เผื่อปรับความดังเฉพาะ BGM runtime
    public void SetLocalGain(float v)
    {
        localGain = Mathf.Clamp01(v);
        var act = Active();
        if (act && act.isPlaying) act.volume = localGain;
    }
    public void DuckAndStop(float duckDur = 0.18f, float holdSilence = 0f)
    {
        if (_duckCo != null) { StopCoroutine(_duckCo); _duckCo = null; }
        _duckCo = StartCoroutine(DuckAndStopCo(Mathf.Max(0.05f, duckDur), Mathf.Max(0f, holdSilence)));
    }

    IEnumerator DuckAndStopCo(float duckDur, float hold)
    {
        // ถ้ากำลัง crossfade อยู่ ให้ยกเลิกเฉพาะ crossfade (ไม่ StopAll ทั้งคลาส)
        if (_xfadeCo != null) { StopCoroutine(_xfadeCo); _xfadeCo = null; _xfadeTarget = null; }

        var sA = Active();
        var sB = Inactive();
        float vA0 = sA ? sA.volume : 0f;
        float vB0 = sB ? sB.volume : 0f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duckDur;
            float k = Mathf.Clamp01(t);
            if (sA) sA.volume = Mathf.Lerp(vA0, 0f, k);
            if (sB) sB.volume = Mathf.Lerp(vB0, 0f, k);
            yield return null;
        }

        if (sA) { sA.volume = 0f; sA.Stop(); }
        if (sB) { sB.volume = 0f; sB.Stop(); }
        _duckCo = null;

        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);
    }
}
