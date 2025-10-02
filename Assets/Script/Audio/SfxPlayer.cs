
using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;
using System.Collections;

public enum SfxId
{
    SlotShift,
    TileDrop,
    TileSnap,
    TileTransfer,

    // ===== ชุดคะแนน =====
    ScoreLetterTick,
    ScoreMultTick,
    ScoreJoin,
    ScorePenalty,
    ScoreCommit,
    StreakBreak,

    // ===== เพิ่มใหม่: ชุด UI =====
    UI_Hover,   // ชี้เมาส์เข้า
    UI_Click    // กดปุ่ม (ตอน PointerDown)
}

[Serializable]
public class SfxEntry
{
    public SfxId id;
    public AudioClip[] clips;
    [Range(0f, 2f)] public float volume = 1f;
    public Vector2 pitch = new Vector2(0.98f, 1.02f);
    [Tooltip("กันสแปมเสียง: วินาทีขั้นต่ำระหว่างครั้งถัดไป")]
    public float cooldown = 0.035f;
}

/// <summary>
/// SfxPlayer
/// - ใช้ AudioSource เดียวเล่น OneShot ทั้งหมด
/// - ต่อออก AudioMixerGroup ของ SFX เพื่อคุมผ่าน AudioMixer
/// </summary>
public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer I { get; private set; }

    [Header("Config")]
    public AudioSource source;     // ใส่ AudioSource ผ่าน Inspector
    [Tooltip("Mixer Group สำหรับ SFX")]
    public AudioMixerGroup outputGroup;
    public SfxEntry[] entries;
    // fields สำหรับ clamp pitch
    [Header("Pitch Clamp")]
    [Range(0.1f, 3f)] public float pitchMin = 0.5f;
    [Range(0.1f, 3f)] public float pitchMax = 2.0f;

    Dictionary<SfxId, SfxEntry> _map = new();
    Dictionary<SfxId, float> _last = new();

    // ===== Burst Gate (กันเสียงซ้อนในช่วงสั้น ๆ ต่อ 1 SFX) =====
    private static readonly Dictionary<SfxId, (float lastTime, int count)> _burst
        = new Dictionary<SfxId, (float, int)>();

    private const float BURST_WINDOW = 0.08f;   // วินโดว์ 80ms
    private const int BURST_MAX_PLAYS = 1;    // อนุญาตเล่นสูงสุดกี่ครั้งในวินโดว์

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        foreach (var e in entries) if (e != null) _map[e.id] = e;

        if (!source) source = GetComponent<AudioSource>();
        if (!source) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 1f; // ความดังหลักคุมด้วย Mixer
        if (outputGroup) source.outputAudioMixerGroup = outputGroup;

        DontDestroyOnLoad(gameObject);
    }
    public static void PlayIfNotPaused(SfxId id) {
    if (PauseManager.IsPaused) return;
    Play(id);
    }
    public static void PlayPitchIfNotPaused(SfxId id, float pitch) {
        if (PauseManager.IsPaused) return;
        PlayPitch(id, pitch);
    }
    public static void PlayVolPitchIfNotPaused(SfxId id, float vol, float pitch) {
        if (PauseManager.IsPaused) return;
        PlayVolPitch(id, vol, pitch);
    }
    public static void PlayForDurationIfNotPaused(SfxId id, float dur, bool stretchPitch=false, float volumeMul=1f) {
        if (PauseManager.IsPaused) return;
        PlayForDuration(id, dur, stretchPitch, volumeMul);
    }
    public void StopAllAndClearBank()
    {
        // หยุด src หลัก
        if (source) source.Stop();

        // หยุดแหล่งเสียงชั่วคราวที่อาจถูกสร้าง (เช่น PlayForDuration)
        #if UNITY_2023_1_OR_NEWER
        var all = UnityEngine.Object.FindObjectsByType<AudioSource>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        #else
        var all = GameObject.FindObjectsOfType<AudioSource>();
        #endif
        foreach (var s in all)
        {
            if (s == source) continue;
            if (outputGroup && s.outputAudioMixerGroup == outputGroup)
                s.Stop();
        }

        // ล้าง mapping sfx ปัจจุบัน (ให้ซีนใหม่โหลดแบงก์ของตัวเอง)
        _map.Clear();
        _burst.Clear();
    }
    public static void Play(SfxId id)
    {
        if (!I || I.source == null) return;
        if (!I._map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) return;

        // ==== Burst Gate ต่อ SFX id ====
        float now = Time.unscaledTime;
        if (_burst.TryGetValue(id, out var b) && now - b.lastTime < BURST_WINDOW)
        {
            if (b.count >= BURST_MAX_PLAYS)
                return; // ข้ามการเล่นรอบนี้ เพื่อกันซ้อน
            _burst[id] = (b.lastTime, b.count + 1);
        }
        else
        {
            _burst[id] = (now, 1);
        }

        var clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
        I.source.pitch = UnityEngine.Random.Range(e.pitch.x, e.pitch.y);
        I.source.PlayOneShot(clip, e.volume); // Volume เฉพาะรายการนี้ (Mixer ยังคุมหลัก)
        I._last[id] = now;
    }
    public static void PlayPitch(SfxId id, float fixedPitch)
    {
        if (!I || I.source == null) return;
        if (!I._map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) return;

        // ==== Burst Gate ต่อ SFX เหมือน Play() ปกติ ====
        float now = Time.unscaledTime;
        if (_burst.TryGetValue(id, out var b) && now - b.lastTime < BURST_WINDOW)
        {
            if (b.count >= BURST_MAX_PLAYS) return;
            _burst[id] = (b.lastTime, b.count + 1);
        }
        else
        {
            _burst[id] = (now, 1);
        }

        // route ออก Mixer group เดิม (ถ้าตั้งไว้ใน Inspector)
        if (I.outputGroup) I.source.outputAudioMixerGroup = I.outputGroup;

        // ใช้ pitch ที่กำหนด (ไม่สุ่ม)
        I.source.pitch = Mathf.Clamp(fixedPitch, I.pitchMin, I.pitchMax);
        I.source.volume = 1f;

        var clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
        I.source.PlayOneShot(clip, e.volume);
    }
    public static void PlayVolPitch(SfxId id, float volumeMul, float fixedPitch)
    {
        if (!I || I.source == null) return;
        if (!I._map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) return;

        // กันสแปมแบบเดียวกับ Play()
        float now = Time.unscaledTime;
        if (_burst.TryGetValue(id, out var b) && now - b.lastTime < BURST_WINDOW)
        {
            if (b.count >= BURST_MAX_PLAYS) return;
            _burst[id] = (b.lastTime, b.count + 1);
        }
        else _burst[id] = (now, 1);

        if (I.outputGroup) I.source.outputAudioMixerGroup = I.outputGroup;

        I.source.pitch = Mathf.Clamp(fixedPitch, I.pitchMin, I.pitchMax);

        // ปลอดภัย: จำกัดความดังไม่ให้เกิน 1.75 เท่าของ entry
        float vol = Mathf.Clamp(volumeMul, 0f, 1.75f);

        var clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
        I.source.PlayOneShot(clip, e.volume * vol);
    }
    public static Coroutine PlayForDuration(SfxId id, float duration, bool stretchPitch = true, float volumeMul = 1f)
    {
        if (!I) return null;
        return I.StartCoroutine(I.PlayForDurationCo(id, duration, stretchPitch, volumeMul));
    }

    IEnumerator PlayForDurationCo(SfxId id, float duration, bool stretchPitch, float volumeMul)
    {
        if (!_map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) yield break;

        var clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
        var go = new GameObject($"SFX_{id}_temp");
        var src = go.AddComponent<AudioSource>();

        // route ไป Mixer Group SFX เพื่อให้สไลเดอร์ SFX คุมได้
        if (outputGroup) src.outputAudioMixerGroup = outputGroup;

        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.loop = false;

        float pitch = 1f;
        if (stretchPitch && clip && clip.length > 0.001f)
            pitch = Mathf.Clamp(clip.length / Mathf.Max(0.001f, duration), pitchMin, pitchMax);

        src.pitch = pitch;
        src.volume = e.volume * Mathf.Clamp(volumeMul, 0f, 1.75f);
        src.clip = clip;

        // ถ้าคลิป (หลังยืด/เร่ง) สั้นกว่า duration มาก ให้ loop เติมเวลา
        float effectiveLen = (clip ? clip.length : 0f) / Mathf.Max(0.001f, pitch);
        if (effectiveLen + 0.02f < duration) src.loop = true;

        src.Play();
        yield return new WaitForSecondsRealtime(Mathf.Max(0.001f, duration));
        if (src) src.Stop();
        Destroy(go);
    }
    public void LoadBank(SfxEntry[] bank, bool replaceAll = true)
    {
        if (replaceAll) _map.Clear();
        if (bank == null) return;
        foreach (var e in bank)
        {
            if (e == null) continue;
            _map[e.id] = e; // override ถ้ามี id เดิม
        }
    }
}
