using DG.Tweening;
using System.Linq;
using UnityEngine;
using Zenject;

public class CameraController : MonoBehaviour
{
    private IEntityRegistry<IEntity> _playerRegistry;
    private CameraSettings _settings;

    private float _yaw;
    private float _pitch;
    private Tween _distanceTween;

    // текущие значения, которые плавно едут к целевым
    private bool _initialized;
    private bool _targetAcquired;
    private Vector3 _currentOffset;
    private Vector2 _currentLimits;
    private float _currentDistance;

    public Vector3 Forward =>
        Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

    public Vector3 Right =>
        Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

    [Inject]
    public void Construct(
        IEntityRegistry<IEntity> playerRegistry,
        CameraSettings settings)
    {
        _playerRegistry = playerRegistry;
        _settings = settings;
    }

    private void Awake()
    {
        TryAcquireTarget();
    }

    private void OnEnable()
    {
        if (_targetAcquired)
            SubscribeGameState();
    }

    private void OnDisable()
    {
        UnsubscribeGameState();
    }

    private void LateUpdate()
    {
        if (!_targetAcquired)
        {
            TryAcquireTarget();
            if (!_targetAcquired)
                return;

            SubscribeGameState();
        }

        if (GameState.IsPaused || InformationPointUI.BlocksGameplayInput)
            return;

        Rotation();
        View();
    }

    private void SubscribeGameState()
    {
        GameState.PauseChanged -= OnPauseChanged;
        GameState.PauseChanged += OnPauseChanged;
        ApplyPauseCursorState(GameState.IsPaused);
    }

    private void UnsubscribeGameState()
    {
        GameState.PauseChanged -= OnPauseChanged;
    }

    private bool TryAcquireTarget()
    {
        if (_targetAcquired)
            return true;

        if (_playerRegistry == null || _settings == null)
            return false;

        Player player = _playerRegistry.AllEntities
            .OfType<Player>()
            .FirstOrDefault(x => x != null && x.gameObject.activeInHierarchy);

        if (player == null)
            return false;

        bool inVehicle = PlayerFootstepAudio.isOnSeat;

        Vector3 targetOffset = inVehicle ? _settings.Vehicle.TargetOffset : _settings.TargetOffset;
        Vector2 targetLimits = inVehicle ? _settings.Vehicle.VerticalLimits : _settings.VerticalLimits;
        float targetDistance = inVehicle ? _settings.Vehicle.Distance : _settings.Distance;

        _currentOffset = targetOffset;
        _currentLimits = targetLimits;
        _currentDistance = targetDistance;
        _initialized = true;

        Vector3 forward = GetFlatForward(player, inVehicle);
        _yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        _pitch = Mathf.Clamp(_pitch, _currentLimits.x, _currentLimits.y);

        Vector3 center = player.transform.position + _currentOffset;
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        transform.position = center + rotation * new Vector3(0f, 0f, -_currentDistance);
        transform.LookAt(center);

        _targetAcquired = true;
        return true;
    }

    private static Vector3 GetFlatForward(Player player, bool inVehicle)
    {
        Transform source = player.transform;
        if (inVehicle && source.parent != null)
            source = source.parent;

        Vector3 flatForward = Vector3.ProjectOnPlane(source.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);

        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;

        return flatForward.normalized;
    }

    private void OnPauseChanged(bool paused)
    {
        ApplyPauseCursorState(paused);

        if (paused)
            _distanceTween?.Pause();
        else
            _distanceTween?.Play();
    }

    private static void ApplyPauseCursorState(bool paused)
    {
        if (!paused) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Rotation()
    {
        Player player = _playerRegistry.AllEntities
            .OfType<Player>()
            .FirstOrDefault(x => x != null && x.gameObject.activeInHierarchy);

        if (player == null)
            return;

        bool inVehicle = PlayerFootstepAudio.isOnSeat;

        Vector3 targetOffset = inVehicle ? _settings.Vehicle.TargetOffset : _settings.TargetOffset;
        Vector2 targetLimits = inVehicle ? _settings.Vehicle.VerticalLimits : _settings.VerticalLimits;
        float targetDistance = inVehicle ? _settings.Vehicle.Distance : _settings.Distance;

        // первый кадр: сразу ставим значения без перехода
        if (!_initialized)
        {
            _currentOffset = targetOffset;
            _currentLimits = targetLimits;
            _currentDistance = targetDistance;
            _initialized = true;
        }
        else
        {
            float speed = _settings.Vehicle.TransitionSpeed;
            float t = speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * Time.deltaTime);

            _currentOffset = Vector3.Lerp(_currentOffset, targetOffset, t);
            _currentLimits = Vector2.Lerp(_currentLimits, targetLimits, t);
            _currentDistance = Mathf.Lerp(_currentDistance, targetDistance, t);
        }

        Vector3 center = player.transform.position + _currentOffset;

        _yaw += Input.GetAxis("Mouse X") * _settings.Sensitivity;
        _pitch -= Input.GetAxis("Mouse Y") * _settings.Sensitivity;
        _pitch = Mathf.Clamp(_pitch, _currentLimits.x, _currentLimits.y);

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        transform.position = center + rotation * new Vector3(0f, 0f, -_currentDistance);
        transform.LookAt(center);
    }

    private void View()
    {
        if (PlayerFootstepAudio.isOnSeat)
            return;

        if (!Input.GetKeyDown(_settings.Key))
            return;

        CameraMode targetMode =
            _settings.Mode == CameraMode.FirstPerson
                ? CameraMode.ThirdPerson
                : CameraMode.FirstPerson;

        float targetDistance =
            targetMode == CameraMode.FirstPerson
                ? _settings.MinDistance
                : _settings.MaxDistance;

        _settings.Mode = targetMode;

        _distanceTween?.Kill();

        _distanceTween = DOTween.To(
                () => _settings.Distance,
                value => _settings.Distance = value,
                targetDistance,
                0.5f)
            .SetEase(Ease.OutQuad);
    }

    private void OnDestroy()
    {
        UnsubscribeGameState();
        _distanceTween?.Kill();
    }
}