using UnityEngine;
using Zenject;

public enum MovementMode
{
    Walk,
    Flee,
    SeekFood
}

[RequireComponent(typeof(CharacterController))]
public class ChickenMovement : MonoBehaviour
{
    [SerializeField] private ChickenAudio _audio;

    private CharacterController _controller;
    private ChickenSettings _settings;
    private IEntityRegistry<IEntity> _entityRegistry;
    private Transform _overrideTargetTransform;
    private float _verticalVelocity;
    private float _jumpCooldownTimer;

    private float _currentMaxSpeed;

    private float _individualDistanceOffset;
    private float _individualFleeAngleOffset;
    private float _individualSpeedMultiplier;

    private MovementMode _currentMovementMode;

    [Inject]
    private void Construct(IEntityRegistry<IEntity> registry)
    {
        _entityRegistry = registry;
    }

    private void Awake() => _controller = GetComponent<CharacterController>();

    public void Initialize(ChickenSettings settings, Transform target = null)
    {
        _settings = settings;
        if (target != null)
            _overrideTargetTransform = target;

        GenerateIndividualTraits();
        SetMovementMode(MovementMode.Walk);
        ApplyWallPassThrough();
    }

    /// <summary>
    /// Отключает столкновения слоя курицы со слоями стен. Работает с CharacterController,
    /// в отличие от Physics.IgnoreCollision. Забор должен быть на ОТДЕЛЬНОМ слое —
    /// его в маску не включаем, иначе куры будут проходить и через него.
    /// </summary>
    private void ApplyWallPassThrough()
    {
        if (_settings == null || _controller == null) return;

        int wallMask = _settings.PassThroughWallsMask.value;
        if (wallMask == 0) return;

        int chickenLayer = gameObject.layer;
        for (int i = 0; i < 32; i++)
        {
            if ((wallMask & (1 << i)) != 0)
                Physics.IgnoreLayerCollision(chickenLayer, i, true);
        }
    }

    public void RefreshWallPassThrough() => ApplyWallPassThrough();

    private Transform TargetTransform
    {
        get
        {
            if (_overrideTargetTransform != null)
                return _overrideTargetTransform;

            if (_entityRegistry == null)
                return null;

            Transform closestTarget = null;
            float minSqrDistance = float.MaxValue;
            Vector3 currentPos = transform.position;

            foreach (var entity in _entityRegistry.AllEntities)
            {
                if (entity == null || entity.Transform == null) continue;
                if (entity.Transform == transform) continue; // не бежим от себя

                float sqrDist = (entity.Transform.position - currentPos).sqrMagnitude;
                if (sqrDist < minSqrDistance)
                {
                    minSqrDistance = sqrDist;
                    closestTarget = entity.Transform;
                }
            }

            return closestTarget;
        }
    }

    private void GenerateIndividualTraits()
    {
        if (_settings == null) return;

        _individualDistanceOffset = Random.Range(0f, _settings.DistanceVariance);
        _individualFleeAngleOffset = Random.Range(-_settings.FleeAngleVariance, _settings.FleeAngleVariance);
        _individualSpeedMultiplier = Random.Range(
            _settings.IndividualSpeedMultiplierRange.x,
            _settings.IndividualSpeedMultiplierRange.y);
    }

    public void SetMovementMode(MovementMode mode)
    {
        if (_settings == null) return;

        bool modeChanged = _currentMovementMode != mode;
        _currentMovementMode = mode;

        SpeedProfileSettings profile = mode switch
        {
            MovementMode.Walk => _settings.WalkProfile,
            MovementMode.Flee => _settings.FleeProfile,
            MovementMode.SeekFood => _settings.SeekFoodProfile,
            _ => _settings.WalkProfile
        };

        ApplySpeedProfile(profile);

        if (!modeChanged || _audio == null)
            return;

        switch (mode)
        {
            case MovementMode.Flee:
                _audio.PlayFlee();
                break;

            case MovementMode.Walk:
            case MovementMode.SeekFood:
                _audio.PlayDefault();
                break;
        }
    }

    public void ApplySpeedProfile(SpeedProfileSettings profile)
    {
        _currentMaxSpeed = profile.MaxSpeed;
    }

    public void SetTarget(Transform target) => _overrideTargetTransform = target;

    public bool IsTargetClose(float baseDistance)
    {
        var target = TargetTransform;
        if (target == null || _settings == null) return false;

        float effectiveDistance = Mathf.Max(0.2f, baseDistance + _individualDistanceOffset);
        return GetPlanarSqrDistanceTo(target) < effectiveDistance * effectiveDistance;
    }

    public bool IsTargetFarEnough(float baseDistance)
    {
        var target = TargetTransform;
        if (target == null || _settings == null) return true;

        float effectiveDistance = Mathf.Max(0.2f, baseDistance + _individualDistanceOffset);
        return GetPlanarSqrDistanceTo(target) >= effectiveDistance * effectiveDistance;
    }

    /// <summary>
    /// Бежим от БЛИЖАЙШЕЙ угрозы. Одна угроза — одна траектория, детерминированно.
    /// </summary>
    public Vector3 GetFleeDirection()
    {
        var target = TargetTransform;
        if (target == null) return -transform.forward;

        Vector3 direction = transform.position - target.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < _settings.DirectionEpsilon)
            return -transform.forward;

        direction = Quaternion.Euler(0f, _individualFleeAngleOffset, 0f) * direction.normalized;
        return direction;
    }

    public void Move(Vector3 direction)
    {
        if (_controller == null || _settings == null) return;

        if (_jumpCooldownTimer > 0f)
            _jumpCooldownTimer -= Time.deltaTime;

        UpdateVerticalVelocity();

        bool hasDirection = direction.sqrMagnitude >= _settings.DirectionEpsilon;

        if (!hasDirection)
        {
            _controller.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
            return;
        }

        TryJump(direction);

        float speed = _currentMaxSpeed
                    * _individualSpeedMultiplier
                    * _settings.GlobalSpeedMultiplier;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            _settings.TurnSpeed * Time.deltaTime);

        Vector3 velocity = direction.normalized * speed + Vector3.up * _verticalVelocity;
        _controller.Move(velocity * Time.deltaTime);
    }

    private void TryJump(Vector3 direction)
    {
        if (_jumpCooldownTimer > 0f) return;
        if (!_controller.isGrounded) return;
        if (_verticalVelocity > 0.1f) return;
        if (_settings.ObstacleMask.value == 0) return;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir = direction.normalized;
        dir.y = 0f;

        if (Physics.Raycast(origin, dir, _settings.JumpCheckDistance,
                            _settings.ObstacleMask, QueryTriggerInteraction.Ignore))
        {
            _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(_settings.Gravity) * _settings.JumpHeight);
            _jumpCooldownTimer = _settings.JumpCooldown;
        }
    }

    private void UpdateVerticalVelocity()
    {
        _verticalVelocity = _controller.isGrounded && _verticalVelocity < 0f
            ? _settings.GroundedStickyVelocity
            : _verticalVelocity + _settings.Gravity * Time.deltaTime;
    }

    private float GetPlanarSqrDistanceTo(Transform target)
    {
        Vector3 offset = transform.position - target.position;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }
}