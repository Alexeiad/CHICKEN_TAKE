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

    // Динамические коэффициенты текущей функции скорости V(t) = a * t + b
    private float _currentA;
    private float _currentB;
    private float _currentMaxSpeed;
    private float _movementTimer;

    // Индивидуальные уникальные характеристики этой конкретной курицы
    private float _individualDistanceOffset;  // Отклонение дистанции (в пределах +-5м)
    private float _individualFleeAngleOffset;  // Сдвиг угла побега
    private float _individualSpeedMultiplier;  // Физическая форма (множитель скорости)

    
    private MovementMode _currentMovementMode;

    [Inject]
    private void Construct(IEntityRegistry<IEntity> registry)
    {
        _entityRegistry = registry;
    }

    private void Awake() => _controller = GetComponent<CharacterController>();

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

    public void Initialize(ChickenSettings settings, Transform target = null)
    {
        _settings = settings;
        if (target != null)
            _overrideTargetTransform = target;

        GenerateIndividualTraits();
        SetMovementMode(MovementMode.Walk);
    }

    /// <summary>
    /// Генерирует уникальные физические и психологические черты особи
    /// </summary>
    private void GenerateIndividualTraits()
    {
        if (_settings == null) return;

        // 1. Персональное отклонение по расстоянию реагирования (+-5м по умолчанию)
        _individualDistanceOffset = Random.Range(-_settings.DistanceVariance, _settings.DistanceVariance);

        // 2. Индивидуальный угол отклонения при бегстве
        _individualFleeAngleOffset = Random.Range(-_settings.FleeAngleVariance, _settings.FleeAngleVariance);

        // 3. Персональная физическая кондиция (выносливость/скорость)
        _individualSpeedMultiplier = Random.Range(
            _settings.IndividualSpeedMultiplierRange.x,
            _settings.IndividualSpeedMultiplierRange.y
        );
    }

    /// <summary>
    /// Переключает профиль скорости V(t) на основе состояния
    /// </summary>
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

    /// <summary>
    /// Генерирует новые случайно выбранные коэффициенты a и b для этой курицы
    /// </summary>
    public void ApplySpeedProfile(SpeedProfileSettings profile)
    {
        _currentA = Random.Range(profile.ARange.x, profile.ARange.y);
        _currentB = Random.Range(profile.BRange.x, profile.BRange.y);
        _currentMaxSpeed = profile.MaxSpeed;
        _movementTimer = 0f;
    }

    public void SetTarget(Transform target) => _overrideTargetTransform = target;

    /// <summary>
    /// Проверка близости с учетом персонального отклонения курицы.
    /// </summary>
    public bool IsTargetClose(float baseDistance)
    {
        var target = TargetTransform;
        if (target == null || _settings == null) return false;

        // Рассчитываем порог с персональным отклонением (защита от отрицательных значений)
        float effectiveDistance = Mathf.Max(0.2f, baseDistance + _individualDistanceOffset);
        return GetPlanarSqrDistanceTo(target) < effectiveDistance * effectiveDistance;
    }

    /// <summary>
    /// Проверка отдаления с учетом персонального отклонения курицы.
    /// </summary>
    public bool IsTargetFarEnough(float baseDistance)
    {
        var target = TargetTransform;
        if (target == null || _settings == null) return true;

        float effectiveDistance = Mathf.Max(0.2f, baseDistance + _individualDistanceOffset);
        return GetPlanarSqrDistanceTo(target) >= effectiveDistance * effectiveDistance;
    }

    /// <summary>
    /// Рассчитывает вектор побега с учетом индивидуального угла отклонения траектории.
    /// </summary>
    public Vector3 GetFleeDirection()
    {
        var target = TargetTransform;
        if (target == null) return -transform.forward;

        Vector3 direction = transform.position - target.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < _settings.DirectionEpsilon)
            return -transform.forward;

        // Поворачиваем вектор побега на персональный угол курицы
        direction = Quaternion.Euler(0f, _individualFleeAngleOffset, 0f) * direction.normalized;
        return direction;
    }

    /// <summary>
    /// Перемещение. Формула V(t) = (a * t + b) * IndividualMultiplier.
    /// </summary>
    public void Move(Vector3 direction)
    {
        if (_controller == null || _settings == null) return;

        bool hasDirection = direction.sqrMagnitude >= _settings.DirectionEpsilon;

        if (hasDirection)
        {
            _movementTimer += Time.deltaTime;

            // Расчет скорости с учетом времени движения и индивидуального множителя курицы
            float rawSpeed = (_currentA * _movementTimer) + _currentB;
            float currentSpeed = Mathf.Clamp(rawSpeed, 0f, _currentMaxSpeed) * _individualSpeedMultiplier;

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _settings.TurnSpeed * Time.deltaTime);

            UpdateVerticalVelocity();
            Vector3 velocity = direction.normalized * currentSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
        else
        {
            _movementTimer = 0f;
            UpdateVerticalVelocity();
            _controller.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
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