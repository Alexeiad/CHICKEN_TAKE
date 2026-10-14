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

    private void LateUpdate()
    {
        if (InformationPointUI.BlocksGameplayInput) return;
        Rotation();
        View();
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
        _distanceTween?.Kill();
    }
}