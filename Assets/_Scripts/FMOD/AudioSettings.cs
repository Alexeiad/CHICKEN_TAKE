using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public enum MusicClip
{
    Menu,
    Base,
    Level
}

public class AudioSettings
{
    private const string MASTER_KEY = "MasterVolume";
    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    private const string EventMenu = "event:/Menu&Base_Music";
    private const string EventBase = "event:/Menu&Base_Music";
    private const string EventLevel = "event:/Arena_4";

    private Bus _masterBus;
    private Bus _musicBus;
    private Bus _sfxBus;

    public AudioSettings()
    {
        _masterBus = RuntimeManager.GetBus("bus:/");
        _musicBus = RuntimeManager.GetBus("bus:/Music");
        _sfxBus = RuntimeManager.GetBus("bus:/FX");

        // ֿנטלוםÿול דנמלךמסעü טח PlayerPrefs ןנט סמחהאםטט
        SetMasterVolume(GetMasterVolume());
        SetMusicVolume(GetMusicVolume());
        SetSFXVolume(GetSFXVolume());
    }

    public void PlayMusic(MusicClip clip)
    {
        _musicBus.stopAllEvents(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);

        switch (clip)
        {
            case MusicClip.Menu:
                RuntimeManager.PlayOneShot(EventMenu);
                break;
            case MusicClip.Base:
                RuntimeManager.PlayOneShot(EventBase);
                break;
            case MusicClip.Level:
                RuntimeManager.PlayOneShot(EventLevel);
                break;
        }
    }

    public float GetMasterVolume() => PlayerPrefs.GetFloat(MASTER_KEY, 1f);
    public float GetMusicVolume() => PlayerPrefs.GetFloat(MUSIC_KEY, 1f);
    public float GetSFXVolume() => PlayerPrefs.GetFloat(SFX_KEY, 1f);

    public void SetMasterVolume(float newVolume)
    {
        _masterBus.setVolume(newVolume);
        PlayerPrefs.SetFloat(MASTER_KEY, newVolume);
    }

    public void SetMusicVolume(float newVolume)
    {
        _musicBus.setVolume(newVolume);
        PlayerPrefs.SetFloat(MUSIC_KEY, newVolume);
    }

    public void SetSFXVolume(float newVolume)
    {
        _sfxBus.setVolume(newVolume);
        PlayerPrefs.SetFloat(SFX_KEY, newVolume);
    }
}
