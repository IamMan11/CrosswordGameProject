// SceneAudioProfile.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Scene Audio Profile")]
public class SceneAudioProfile : ScriptableObject
{
    public string sceneName;

    [Header("BGM per scene")]
    public AudioClip bgmBase;
    public AudioClip bgmMid;
    public AudioClip bgmHigh;

    [Range(0f,1f)] public float bgmLocalGain = 1f;

    [Header("SFX Bank for this scene")]
    public SfxEntry[] sfxBank; // ← แก้จาก SfxPlayer.SfxEntry[] เป็น SfxEntry[]
}