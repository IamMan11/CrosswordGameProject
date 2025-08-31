using UnityEngine;
using System;
using System.Collections.Generic;

public enum SfxId { SlotShift, TilePickup, TileDrop, TileSnap }

[Serializable]
public class SfxEntry
{
    public SfxId id;
    public AudioClip[] clips;
    [Range(0f,2f)] public float volume = 1f;
    public Vector2 pitch = new Vector2(0.98f, 1.02f);
    [Tooltip("กันสแปมเสียง: วินาทีขั้นต่ำระหว่างครั้งถัดไป")]
    public float cooldown = 0.035f;
}

public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer I { get; private set; }

    [Header("Config")]
    public AudioSource source;     // ใส่ AudioSource ผ่าน Inspector
    public SfxEntry[] entries;

    Dictionary<SfxId, SfxEntry> _map = new();
    Dictionary<SfxId, float> _last = new();

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        foreach (var e in entries) if (e != null) _map[e.id] = e;
        if (!source) source = GetComponent<AudioSource>();
        if (source) source.playOnAwake = false;
    }

    public static void Play(SfxId id)
    {
        if (!I || I.source == null) return;
        if (!I._map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) return;

        float now = Time.unscaledTime;                 // ไม่ผูกกับ timeScale
        if (I._last.TryGetValue(id, out var t) && now - t < e.cooldown) return;

        var clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
        I.source.pitch = UnityEngine.Random.Range(e.pitch.x, e.pitch.y);
        I.source.PlayOneShot(clip, e.volume);
        I._last[id] = now;
    }
}
