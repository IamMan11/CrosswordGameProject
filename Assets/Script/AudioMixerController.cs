
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AudioMixerController
/// - คุมเสียงทั้งหมดผ่าน AudioMixer เดียว (Master/Music/SFX)
/// - ใช้ค่า Linear (0..1) จาก UI แล้วแปลงเป็น dB เพื่อใส่ใน Mixer
/// - บันทึก/โหลดค่าด้วย PlayerPrefs
/// </summary>
public class AudioMixerController : MonoBehaviour
{
    public static AudioMixerController I { get; private set; }

    [Header("Mixer Asset + Exposed Params")]
    public AudioMixer mixer;
    [Tooltip("ชื่อพารามิเตอร์ที่ expose ไว้บน Mixer Group Master")]
    public string masterParam = "MasterVol";
    [Tooltip("ชื่อพารามิเตอร์ที่ expose ไว้บน Mixer Group Music")]
    public string musicParam  = "MusicVol";
    [Tooltip("ชื่อพารามิเตอร์ที่ expose ไว้บน Mixer Group SFX")]
    public string sfxParam    = "SfxVol";

    [Header("Defaults (0..1)")]
    [Range(0f,1f)] public float defaultMaster = 1f;
    [Range(0f,1f)] public float defaultMusic  = 0.9f;
    [Range(0f,1f)] public float defaultSfx    = 1f;

    [Header("Options")]
    public bool dontDestroyOnLoad = true;

    const string KEY_MASTER = "vol_master";
    const string KEY_MUSIC  = "vol_music";
    const string KEY_SFX    = "vol_sfx";

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        ApplyAll( Load(KEY_MASTER, defaultMaster),
                  Load(KEY_MUSIC,  defaultMusic),
                  Load(KEY_SFX,    defaultSfx) );
    }

    // ===== Public API (เรียกจาก UI) =====
    public void SetMaster(float v) { SetLinear(masterParam, v); Save(KEY_MASTER, v); }
    public void SetMusic (float v) { SetLinear(musicParam,  v); Save(KEY_MUSIC,  v); }
    public void SetSfx   (float v) { SetLinear(sfxParam,    v); Save(KEY_SFX,    v); }

    public float GetMaster() => Load(KEY_MASTER, defaultMaster);
    public float GetMusic () => Load(KEY_MUSIC,  defaultMusic);
    public float GetSfx   () => Load(KEY_SFX,    defaultSfx);

    public void ApplyAll(float master, float music, float sfx)
    {
        SetLinear(masterParam, master);
        SetLinear(musicParam,  music);
        SetLinear(sfxParam,    sfx);
        Save(KEY_MASTER, master);
        Save(KEY_MUSIC,  music);
        Save(KEY_SFX,    sfx);
    }

    // ===== Helpers =====
    static float LinearToDb(float v)
    {
        // 0 → -80dB (เงียบ), 1 → 0dB
        if (v <= 0.0001f) return -80f;
        return Mathf.Log10(Mathf.Clamp01(v)) * 20f;
    }

    void SetLinear(string exposedParam, float v)
    {
        if (!mixer || string.IsNullOrEmpty(exposedParam)) return;
        mixer.SetFloat(exposedParam, LinearToDb(v));
    }

    static void Save(string key, float v) => PlayerPrefs.SetFloat(key, v);
    static float Load(string key, float defV) => PlayerPrefs.GetFloat(key, defV);

#if UNITY_EDITOR
    void OnValidate()
    {
        // แสดงผลทันทีใน Editor
        if (mixer)
        {
            mixer.SetFloat(masterParam, LinearToDb(GetMaster()));
            mixer.SetFloat(musicParam,  LinearToDb(GetMusic()));
            mixer.SetFloat(sfxParam,    LinearToDb(GetSfx()));
        }
    }
#endif
}
