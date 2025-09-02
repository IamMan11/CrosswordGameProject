using UnityEngine;
using System;
using System.Collections.Generic;

public enum SfxId {
    SlotShift,
    TileDrop,
    TileSnap,
    TileTransfer,      // ✅ ย้าย Bench <-> Space

    // ✅ ชุดเสียงนับคะแนน
    ScoreLetterTick,   // ฝั่งตัวอักษร (A)
    ScoreMultTick,     // ฝั่งตัวคูณ (B)
    ScoreJoin,         // ตอนรวม A+B
    ScorePenalty,      // ตอนหัก % Dictionary
    ScoreCommit        // ตอนส่งเข้า Score HUD (รวมเข้าคะแนนรวม)
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

public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer I { get; private set; }

    [Header("Config")]
    public AudioSource source;     // ใส่ AudioSource ผ่าน Inspector
    public SfxEntry[] entries;

    Dictionary<SfxId, SfxEntry> _map = new();
    Dictionary<SfxId, float> _last = new();
    // ===== Burst Gate (กันเสียงซ้อนในช่วงสั้น ๆ ต่อ 1 SFX) =====
    private static readonly System.Collections.Generic.Dictionary<SfxId, (float lastTime, int count)> _burst
        = new System.Collections.Generic.Dictionary<SfxId, (float, int)>();

    private const float BURST_WINDOW = 0.08f;   // วินโดว์ 80ms
    private const int   BURST_MAX_PLAYS = 1;    // อนุญาตเล่นสูงสุดกี่ครั้งในวินโดว์

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
        I.source.PlayOneShot(clip, e.volume);
        I._last[id] = now;
    }
}
