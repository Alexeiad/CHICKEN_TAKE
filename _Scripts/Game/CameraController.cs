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
        Rotation();
        View();
    }

    private void Rotation()
    {
        Player player = _playerRegistry.AllEntities
            .OfType<Player>()
            .FirstOrDefault(x =>
                x != null &&
                x.gameObject.activeInHierarchy);

        if (player == null)
            return;

        Vector3 center =
            player.transform.position +
            _settings.TargetOffset;

        _yaw +=
            Input.GetAxis("Mouse X") *
            _settings.Sensitivity;

        _pitch -=
            Input.GetAxis("Mouse Y") *
            _settings.Sensitivity;

        _pitch = Mathf.Clamp(
            _pitch,
            _settings.VerticalLimits.x,
            _settings.VerticalLimits.y);

        Quaternion rotation =
            Quaternion.Euler(_pitch, _yaw, 0f);

        transform.position =
            center +
            rotation * new Vector3(
                0f,
                0f,
                -_settings.Distance);

        transform.LookAt(center);
    }

    private void View()
    {
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