
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class AudioSettingsUI : MonoBehaviour
{
    [Header("Volume Sliders")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Percentage Labels")]
    [SerializeField] private TMP_Text masterVolumeValue;
    [SerializeField] private TMP_Text sfxVolumeValue;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    private void Start()
    {
        float master = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
        float sfx = PlayerPrefs.GetFloat("SFXVolume", 1f);

        masterVolumeSlider.SetValueWithoutNotify(master);
        sfxVolumeSlider.SetValueWithoutNotify(sfx);

        ApplyMasterVolume(master);
        ApplySFXVolume(sfx);

        masterVolumeSlider.onValueChanged.AddListener(ApplyMasterVolume);
        sfxVolumeSlider.onValueChanged.AddListener(ApplySFXVolume);
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(ApplyMasterVolume);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(ApplySFXVolume);
    }

    public void ApplyMasterVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("MasterVolume", volume);

        masterVolumeValue.text = Mathf.RoundToInt(volume * 100f) + "%";
    }

    public void ApplySFXVolume(float volume)
    {
        float decibels = volume <= 0.0001f
            ? -80f
            : Mathf.Log10(volume) * 20f;

        audioMixer.SetFloat("SFXVolume", decibels);
        PlayerPrefs.SetFloat("SFXVolume", volume);

        sfxVolumeValue.text = Mathf.RoundToInt(volume * 100f) + "%";
    }
}
