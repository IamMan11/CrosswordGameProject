using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioSceneRouter : MonoBehaviour
{
    public SceneAudioProfile[] profiles;
    public bool dontDestroyOnLoad = true;

    [Header("Bank load mode")]
    public bool replaceAllSfx = true; // false = merge/override เฉพาะที่มี

    void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyFor(SceneManager.GetActiveScene().name, immediate: true);
    }
    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void OnSceneLoaded(Scene s, LoadSceneMode m) => ApplyFor(s.name, immediate: false);

    void ApplyFor(string sceneName, bool immediate)
    {
        if (profiles == null) return;
        var p = profiles.FirstOrDefault(x => string.Equals(x.sceneName, sceneName, StringComparison.OrdinalIgnoreCase));
        if (p == null) return;

        // ----- BGM -----
        if (BgmPlayer.I)
        {
            // ใส่คลิปตามโปรไฟล์ (ใช้ระบบ crossfade/SetTier เดิม)
            BgmPlayer.I.baseClip = p.bgmBase;
            BgmPlayer.I.midClip  = p.bgmMid  ? p.bgmMid  : p.bgmBase;
            BgmPlayer.I.highClip = p.bgmHigh ? p.bgmHigh : (p.bgmMid ? p.bgmMid : p.bgmBase);
            BgmPlayer.I.SetLocalGain(p.bgmLocalGain);
            BgmPlayer.I.SetTier(BgmTier.Base, immediate: !Application.isPlaying || immediate);
        }

        // ----- SFX -----
        if (SfxPlayer.I) SfxPlayer.I.LoadBank(p.sfxBank, replaceAll: replaceAllSfx);
    }
}
