using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelUI : MonoBehaviour
{
    [Header("UI Sliders (0..1)")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    void OnEnable()
    {
        // เปิดหน้าตั้งค่า -> sync ค่าเสมอ
        RefreshFromMixer();
    }

    public void RefreshFromMixer()
    {
        if (AudioMixerController.I == null) return;
        if (masterSlider) masterSlider.SetValueWithoutNotify(AudioMixerController.I.GetMaster());
        if (musicSlider)  musicSlider.SetValueWithoutNotify(AudioMixerController.I.GetMusic());
        if (sfxSlider)    sfxSlider.SetValueWithoutNotify(AudioMixerController.I.GetSfx());
    }

    // ===== Events: OnValueChanged ของสไลเดอร์ =====
    public void OnMasterChanged(float v)
    {
        if (AudioMixerController.I == null) return;
        AudioMixerController.I.SetMaster(v);
        SfxPlayer.Play(SfxId.UI_Hover);
    }
    public void OnMusicChanged(float v)
    {
        if (AudioMixerController.I == null) return;
        AudioMixerController.I.SetMusic(v);
        SfxPlayer.Play(SfxId.UI_Hover);
    }
    public void OnSfxChanged(float v)
    {
        if (AudioMixerController.I == null) return;
        AudioMixerController.I.SetSfx(v);
        SfxPlayer.Play(SfxId.UI_Hover);
    }
}
