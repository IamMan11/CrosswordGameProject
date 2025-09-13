
using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public class BgmPlayer : MonoBehaviour {
    public AudioSource source;
    public AudioClip clip;

    [Header("Mixer Routing")]
    [Tooltip("ต่อออก Mixer Group ของ Music เพื่อคุมผ่าน AudioMixer")]
    public AudioMixerGroup outputGroup;

    [Header("Local Gain (ซ้อนทับบน Mixer)")]
    [Range(0f,1f)] public float localGain = 1f;
    public bool playOnStart = true;

    void Awake() {
        if (!source) {
            source = gameObject.GetComponent<AudioSource>();
            if (!source) source = gameObject.AddComponent<AudioSource>();
        }
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f; // 2D
        source.volume = localGain;
        if (outputGroup) source.outputAudioMixerGroup = outputGroup;
    }

    void Start() {
        if (playOnStart && clip) Play(clip);
    }

    public void Play(AudioClip c) {
        clip = c;
        source.clip = c;
        source.volume = localGain; // ความดังเฉพาะ BGM (Mixer ยังคุมหลัก)
        source.Play();
    }

    public void Stop() => source?.Stop();

    /// <summary>เฟดเฉพาะ localGain (ยังอยู่ใต้การคุมของ AudioMixer)</summary>
    public IEnumerator FadeTo(float target, float dur) {
        float start = source.volume;
        float t = 0f;
        dur = Mathf.Max(0.0001f, dur);
        while (t < 1f) {
            t += Time.unscaledDeltaTime / dur;
            source.volume = Mathf.Lerp(start, target, t);
            yield return null;
        }
        source.volume = target;
        localGain = target;
    }
}
