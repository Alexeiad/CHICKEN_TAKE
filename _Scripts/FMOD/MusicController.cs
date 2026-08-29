using UnityEngine;
using Zenject;

public class MusicController : MonoBehaviour
{
    [SerializeField] private MusicClip _musicClip;

    private AudioSettings _audioSettings;


    [Inject]
    public void Construct(AudioSettings audioSettings)
    {
        _audioSettings = audioSettings;
    }

    private void Start()
    {
        _audioSettings.PlayMusic(_musicClip);
    }
}