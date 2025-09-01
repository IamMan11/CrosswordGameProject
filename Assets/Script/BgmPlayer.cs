using UnityEngine;
using System.Collections;

public class BgmPlayer : MonoBehaviour {
    public AudioSource source;
    public AudioClip clip;
    [Range(0f,1f)] public float volume = 0.5f;
    public bool playOnStart = true;

    void Awake() {
        if (!source) {
            source = gameObject.GetComponent<AudioSource>();
            if (!source) source = gameObject.AddComponent<AudioSource>();
        }
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f; // 2D
    }

    void Start() {
        if (playOnStart && clip) Play(clip);
    }

    public void Play(AudioClip c) {
        clip = c;
        source.clip = c;
        source.volume = volume;
        source.Play();
    }

    public void Stop() => source?.Stop();

    public IEnumerator FadeTo(float target, float dur) {
        float start = source.volume;
        float t = 0f;
        while (t < 1f) {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, dur);
            source.volume = Mathf.Lerp(start, target, t);
            yield return null;
        }
        source.volume = target;
    }
}
