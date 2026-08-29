using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public class PlayerFootstepAudio : MonoBehaviour
{
    public static bool isOnSeat;

    [Header("FMOD Event")]
    [SerializeField] private EventReference footstepEvent;

    [Header("Pitch")]
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.2f;

    [Header("Step Timing")]
    [SerializeField] private float stepInterval = 0.45f;

    private EventInstance _currentStep;
    private float _stepTimer;
    private bool _isWalking;

    private void Start()
    {
        PlayerInputController.OnAudioReady?.Invoke(this);
    }

    private void Update()
    {
        if (!_isWalking)
            return;

        _stepTimer -= Time.deltaTime;

        if (_stepTimer <= 0f)
        {
            PlayStep();
            _stepTimer = stepInterval;
        }
    }

    public void StartFootsteps()
    {
        if (isOnSeat || _isWalking)
            return;

        _isWalking = true;
        _stepTimer = 0f;
    }

    public void StopFootsteps()
    {
        _isWalking = false;
        _stepTimer = 0f;

        if (_currentStep.isValid())
        {
            _currentStep.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _currentStep.release();
            _currentStep = default;
        }
    }

    private void PlayStep()
    {
        if (footstepEvent.IsNull || !_isWalking || isOnSeat)
            return;

        _currentStep = RuntimeManager.CreateInstance(footstepEvent);

        _currentStep.set3DAttributes(
            RuntimeUtils.To3DAttributes(transform)
        );

        _currentStep.setPitch(
            Random.Range(minPitch, maxPitch)
        );

        _currentStep.start();
    }

    private void OnDestroy()
    {
        StopFootsteps();
    }
}