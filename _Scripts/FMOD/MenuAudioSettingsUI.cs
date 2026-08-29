using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class MenuAudioSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private AudioSettings _audioSettings;

    [Inject]
    public void Construct(AudioSettings audioSettings)
    {
        _audioSettings = audioSettings;
    }

    private void Start()
    {
        // Выставляем слайдеры в сохраненные позиции
        if (masterSlider != null) masterSlider.value = _audioSettings.GetMasterVolume();
        if (musicSlider != null) musicSlider.value = _audioSettings.GetMusicVolume();
        if (sfxSlider != null) sfxSlider.value = _audioSettings.GetSFXVolume();

        // Подписываемся на изменение значений
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(_audioSettings.SetMasterVolume);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(_audioSettings.SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(_audioSettings.SetSFXVolume);
    }

    private void OnDestroy()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(_audioSettings.SetMasterVolume);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(_audioSettings.SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(_audioSettings.SetSFXVolume);
    }
}