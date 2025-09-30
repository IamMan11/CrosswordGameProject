
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ตัวอย่างสคริปต์ต่อกับ UI Slider ในหน้า Settings
/// - ผูกฟังก์ชันนี้กับ OnValueChanged ของแต่ละสไลเดอร์
/// </summary>
public class AudioOptionsUI : MonoBehaviour
{
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    void Start()
    {
        if (AudioMixerController.I)
        {
            if (masterSlider) masterSlider.value = AudioMixerController.I.GetMaster();
            if (musicSlider)  musicSlider.value  = AudioMixerController.I.GetMusic();
            if (sfxSlider)    sfxSlider.value    = AudioMixerController.I.GetSfx();
        }
    }

    public void OnMasterChanged(float v) { AudioMixerController.I?.SetMaster(v); }
    public void OnMusicChanged (float v) { AudioMixerController.I?.SetMusic(v);  }
    public void OnSfxChanged   (float v) { AudioMixerController.I?.SetSfx(v);    }
}
