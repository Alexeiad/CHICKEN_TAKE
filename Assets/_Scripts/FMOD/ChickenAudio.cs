using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public class ChickenAudio : MonoBehaviour
{
    [Header("FMOD Events")]
    [SerializeField] private EventReference defaultEvent;
    [SerializeField] private EventReference fleeEvent1;
    [SerializeField] private EventReference fleeEvent2;

    [Header("Pitch")]
    [Tooltip("ћинимальный случайный pitch относительно 1.0")]
    [SerializeField] private float minPitchOffset = -0.9f;

    [Tooltip("ћаксимальный случайный pitch относительно 1.0")]
    [SerializeField] private float maxPitchOffset = 0.9f;

    /// <summary>
    /// ѕроигрывает обычный звук курицы.
    /// </summary>
    public void PlayDefault()
    {
        PlayEvent(defaultEvent);
    }

    /// <summary>
    /// ѕроигрывает один из двух случайных звуков испуга.
    /// </summary>
    public void PlayFlee()
    {
        EventReference fleeEvent = Random.value < 0.5f
            ? fleeEvent1
            : fleeEvent2;

        PlayEvent(fleeEvent);
    }

    private void PlayEvent(EventReference eventReference)
    {
        if (eventReference.IsNull)
            return;

        EventInstance instance = RuntimeManager.CreateInstance(eventReference);

        float pitch = 1f + Random.Range(minPitchOffset, maxPitchOffset);
        instance.setPitch(pitch);

        instance.set3DAttributes(
            RuntimeUtils.To3DAttributes(transform)
        );

        instance.start();
        instance.release();
    }
}