using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PrometeoCarController))]
[RequireComponent(typeof(Rigidbody))]
public class PrometeoSplineAIController : MonoBehaviour
{
    [Header("Spline Selection")]
    [FormerlySerializedAs("splineContainer")]
    [SerializeField] private SplineContainer _splineContainer;

    [Tooltip("Текущий индекс сплайна в контейнере (0, 1, 2, 3...)")]
    public int splineIndex = 0;

    [Header("Start Snap Settings")]
    [Tooltip("Автоматически телепортировать и выравнивать бота по линии при старте")]
    [FormerlySerializedAs("snapToSplineOnStart")]
    [SerializeField] private bool _snapToSplineOnStart = true;

    [Header("Speed Settings")]
    [FormerlySerializedAs("maxSpeed")]
    [SerializeField] private float _maxSpeed = 100f;
    [FormerlySerializedAs("physicalBrakePower")]
    [SerializeField] private float _physicalBrakePower = 15f;

    [Header("Look-Ahead Settings")]
    [FormerlySerializedAs("baseLookAhead")]
    [SerializeField] private float _baseLookAhead = 20f;
    [FormerlySerializedAs("speedLookAheadFactor")]
    [SerializeField] private float _speedLookAheadFactor = 0.18f;

    [Header("Anti-Oscillation")]
    [FormerlySerializedAs("predictionFactor")]
    [SerializeField] private float _predictionFactor = 0.18f;
    [FormerlySerializedAs("steerDeadzone")]
    [SerializeField] private float _steerDeadzone = 3.0f;

    [Header("AI Decision Timing")]
    [FormerlySerializedAs("minDecisionInterval")]
    [SerializeField] private float _minDecisionInterval = 0.4f;
    [FormerlySerializedAs("maxDecisionInterval")]
    [SerializeField] private float _maxDecisionInterval = 1.0f;

    [Header("Overtake & Lane Change Mechanics")]
    [Range(0f, 100f)]
    [Tooltip("Шанс случайной перестройки на свободной дороге")]
    public float laneChangeProbability = 30f;

    [Tooltip("Дистанция обнаружения транспорта перед капотом")]
    [FormerlySerializedAs("frontDetectionRange")]
    [SerializeField] private float _frontDetectionRange = 25f;

    [Tooltip("Множитель скорости при обгоне (Газ в пол!)")]
    [FormerlySerializedAs("aggressiveSpeedMultiplier")]
    [SerializeField] private float _aggressiveSpeedMultiplier = 1.4f;

    [Tooltip("Индексы сплайнов, доступные для перестройки")]
    [FormerlySerializedAs("allowedLaneChangeSplines")]
    [SerializeField] private List<int> _allowedLaneChangeSplines = new List<int>();

    [Header("Cooperative & Yielding System")]
    [Tooltip("Множитель скорости при уступке дороги (притормаживание)")]
    [FormerlySerializedAs("yieldSpeedMultiplier")]
    [SerializeField] private float _yieldSpeedMultiplier = 0.7f;

    [FormerlySerializedAs("yieldCheckDistance")]
    [SerializeField] private float _yieldCheckDistance = 12f;

    [Header("Lane Change Intervals")]
    [FormerlySerializedAs("minLaneChangeCooldown")]
    [SerializeField] private float _minLaneChangeCooldown = 1.5f;
    [FormerlySerializedAs("maxLaneChangeCooldown")]
    [SerializeField] private float _maxLaneChangeCooldown = 4.0f;

    [Header("Waypoints (Slow Zones)")]
    [FormerlySerializedAs("trackWaypoints")]
    [SerializeField] private List<TrackWaypoint> _trackWaypoints = new List<TrackWaypoint>();

    [Header("Stuck Recovery")]
    [Tooltip("Скорость ниже этого значения считается практически остановкой")]
    [FormerlySerializedAs("stuckSpeedThreshold")]
    [SerializeField] private float _stuckSpeedThreshold = 0.1f;

    [Tooltip("Сколько секунд машина должна стоять, прежде чем начать восстановление")]
    [FormerlySerializedAs("stuckTime")]
    [SerializeField] private float _stuckTime = 1.0f;

    [Tooltip("На сколько градусов повернуть машину при застревании")]
    [FormerlySerializedAs("stuckTurnAngle")]
    [SerializeField] private float _stuckTurnAngle = 45f;

    private NavMeshAgent _agent;
    private PrometeoCarController _carController;
    private Rigidbody _rb;

    // Таймеры и состояния
    private float _decisionTimer = 0f;
    private float _currentDecisionInterval = 0.5f;
    private float _laneChangeCooldownTimer = 0f;

    // Таймер застревания
    private float _stuckTimer = 0f;

    // Состояния обгона и уступки
    public bool isOvertakingBoost { get; private set; } = false;
    private bool _isDesperateOvertake = false;
    private bool _isYielding = false;

    private float _overtakeTimer = 0f;
    private int _targetSplineAfterOvertake = -1;

    // Трекинг соперников
    private PrometeoSplineAIController _lastCarAhead = null;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _carController = GetComponent<PrometeoCarController>();
        _rb = GetComponent<Rigidbody>();

        _agent.updatePosition = false;
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
    }

    private void Start()
    {
        ResetDecisionTimer();

        _laneChangeCooldownTimer = UnityEngine.Random.Range(0.1f, _minLaneChangeCooldown);

        // Мгновенная телепортация и выравнивание при старте
        if (_snapToSplineOnStart)
        {
            AlignToSplineOnStart();
        }
    }

    /// <summary>
    /// Вычисляет целевую точку ИИ и мгновенно перемещает/разворачивает машину по направлению маршрута.
    /// </summary>
    private void AlignToSplineOnStart()
    {
        if (_splineContainer == null ||
            _splineContainer.Splines == null ||
            _splineContainer.Splines.Count <= splineIndex)
        {
            return;
        }

        Spline currentSpline = _splineContainer.Splines[splineIndex];
        float splineLength = currentSpline.GetLength();

        if (splineLength <= 0f)
        {
            return;
        }

        // Находим ближайшую точку на сплайне
        SplineUtility.GetNearestPoint(
            currentSpline,
            _splineContainer.transform.InverseTransformPoint(transform.position),
            out _,
            out float normalizedTime
        );

        // Рассчитываем позицию и вектор направления целевой точки
        float targetDistance = ((normalizedTime * splineLength) + _baseLookAhead) % splineLength;
        float targetNormalizedTime = targetDistance / splineLength;

        float3 localTargetPos = SplineUtility.EvaluatePosition(currentSpline, targetNormalizedTime);
        Vector3 worldTargetPos = _splineContainer.transform.TransformPoint((Vector3)localTargetPos);

        // 1. Телепортируем машину на ближайшую точку сплайна
        float3 localNearestPos = SplineUtility.EvaluatePosition(currentSpline, normalizedTime);
        Vector3 worldNearestPos = _splineContainer.transform.TransformPoint((Vector3)localNearestPos);

        transform.position = worldNearestPos;

        // 2. Поворачиваем машину ровно в сторону целевой точки
        Vector3 targetDirection = (worldTargetPos - worldNearestPos).normalized;

        if (targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(targetDirection, Vector3.up);
        }

        // 3. Синхронизируем NavMeshAgent и сбрасываем лишнюю инерцию
        if (_agent.isOnNavMesh)
        {
            _agent.Warp(worldNearestPos);
        }

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    private void Update()
    {
        if (GameState.IsPaused)
        {
            return;
        }

        if (_splineContainer == null ||
            _splineContainer.Splines == null)
        {
            return;
        }

        // Отсчет кулдауна перестройки
        if (_laneChangeCooldownTimer > 0f)
        {
            _laneChangeCooldownTimer -= Time.deltaTime;
        }

        // --- 1. Таймер решений AI ---
        _decisionTimer += Time.deltaTime;

        if (_decisionTimer >= _currentDecisionInterval)
        {
            ResetDecisionTimer();
            EvaluateAIBehaviors();
        }

        // --- 2. Обработка процесса обгона ---
        if (isOvertakingBoost)
        {
            _overtakeTimer -= Time.deltaTime;

            if (_overtakeTimer <= 0f)
            {
                if (_targetSplineAfterOvertake >= 0 &&
                    _targetSplineAfterOvertake < _splineContainer.Splines.Count)
                {
                    splineIndex = _targetSplineAfterOvertake;
                }

                isOvertakingBoost = false;
                _isDesperateOvertake = false;

                SetRandomLaneChangeCooldown();
            }
        }

        if (!_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }

            return;
        }

        _agent.nextPosition = transform.position;

        // --- 3. Расчет целевой точки движения ---
        Spline currentSpline = _splineContainer.Splines[splineIndex];
        float splineLength = currentSpline.GetLength();

        if (splineLength <= 0f)
        {
            return;
        }

        SplineUtility.GetNearestPoint(
            currentSpline,
            _splineContainer.transform.InverseTransformPoint(transform.position),
            out _,
            out float normalizedTime
        );

        float currentSpeed = Mathf.Abs(_carController.carSpeed);

        // Проверяем, не застряла ли машина
        HandleStuckRecovery(currentSpeed);

        float dynamicLookAhead = _baseLookAhead + (currentSpeed * _speedLookAheadFactor);

        float targetDistance = ((normalizedTime * splineLength) + dynamicLookAhead) % splineLength;
        float targetNormalizedTime = targetDistance / splineLength;

        float3 localTargetPos = SplineUtility.EvaluatePosition(currentSpline, targetNormalizedTime);
        Vector3 worldTargetPos = _splineContainer.transform.TransformPoint((Vector3)localTargetPos);

        _agent.SetDestination(worldTargetPos);

        float distanceToSpline = Vector3.Distance(transform.position, worldTargetPos);

        Vector3 finalSteerTarget = (distanceToSpline > 12f)
            ? _agent.steeringTarget
            : worldTargetPos;

        // --- 4. Управление скоростью и руление ---
        ApplyPredictiveSteering(finalSteerTarget);

        float activeSpeedLimit = GetAllowedSpeedForPosition();

        if (isOvertakingBoost)
        {
            activeSpeedLimit *= _aggressiveSpeedMultiplier;
        }
        else if (_isYielding)
        {
            activeSpeedLimit *= _yieldSpeedMultiplier;
        }

        ApplySpeedControl(currentSpeed, activeSpeedLimit);
    }

    private void FixedUpdate()
    {
        if (GameState.IsPaused)
        {
            return;
        }

        float currentSpeed = Mathf.Abs(_carController.carSpeed);
        float allowedSpeed = GetAllowedSpeedForPosition();

        if (isOvertakingBoost)
        {
            allowedSpeed *= _aggressiveSpeedMultiplier;
        }

        if (currentSpeed > allowedSpeed + 3f && _rb != null)
        {
            _rb.AddForce(-_rb.velocity.normalized * _physicalBrakePower, ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// Проверяет, не застряла ли машина.
    /// Если скорость меньше порога в течение заданного времени,
    /// поворачивает машину на заданный угол в случайную сторону.
    /// </summary>
    private void HandleStuckRecovery(float currentSpeed)
    {
        if (currentSpeed < _stuckSpeedThreshold)
        {
            _stuckTimer += Time.deltaTime;

            if (_stuckTimer >= _stuckTime)
            {
                _stuckTimer = 0f;

                // Случайно выбираем левый или правый поворот
                float direction = UnityEngine.Random.value > 0.5f ? 1f : -1f;

                Quaternion recoveryRotation =
                    _rb.rotation * Quaternion.Euler(0f, _stuckTurnAngle * direction, 0f);

                _rb.MoveRotation(recoveryRotation);

                // Даём машине ещё раз газ
                _carController.GoForward();
            }
        }
        else
        {
            // Машина снова начала двигаться, сбрасываем таймер застревания
            _stuckTimer = 0f;
        }
    }

    private void EvaluateAIBehaviors()
    {
        PrometeoSplineAIController currentCarAhead = GetCarAheadOnLane();

        _isYielding = CheckIfShouldYield(currentCarAhead);

        if (currentCarAhead != null)
        {
            if (currentCarAhead == _lastCarAhead)
            {
                TryStartOvertake(isDesperate: true);
            }
            else
            {
                if (_laneChangeCooldownTimer <= 0f)
                {
                    TryStartOvertake(isDesperate: false);
                }
            }
        }
        else
        {
            if (!isOvertakingBoost &&
                _laneChangeCooldownTimer <= 0f &&
                _allowedLaneChangeSplines.Contains(splineIndex))
            {
                float roll = UnityEngine.Random.Range(0f, 100f);

                if (roll <= laneChangeProbability)
                {
                    TryStartOvertake(isDesperate: false);
                }
            }
        }

        _lastCarAhead = currentCarAhead;
    }

    private void TryStartOvertake(bool isDesperate)
    {
        if (isOvertakingBoost)
        {
            return;
        }

        if (!_allowedLaneChangeSplines.Contains(splineIndex))
        {
            return;
        }

        int targetLane = GetAdjacentLaneIndex();

        if (targetLane != splineIndex)
        {
            isOvertakingBoost = true;
            _isDesperateOvertake = isDesperate;

            _targetSplineAfterOvertake = targetLane;

            _overtakeTimer = 3.0f;
        }
    }

    private bool CheckIfShouldYield(PrometeoSplineAIController currentCarAhead)
    {
        if (currentCarAhead != null && currentCarAhead != _lastCarAhead)
        {
            return true;
        }

        PrometeoSplineAIController[] allCars =
            FindObjectsByType<PrometeoSplineAIController>(FindObjectsSortMode.None);

        foreach (var car in allCars)
        {
            if (car == this)
            {
                continue;
            }

            if (car.isOvertakingBoost)
            {
                Vector3 toCar = car.transform.position - transform.position;
                float distance = toCar.magnitude;

                if (distance <= _yieldCheckDistance &&
                    Vector3.Dot(transform.forward, toCar.normalized) > 0.2f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private PrometeoSplineAIController GetCarAheadOnLane()
    {
        PrometeoSplineAIController[] allCars =
            FindObjectsByType<PrometeoSplineAIController>(FindObjectsSortMode.None);

        PrometeoSplineAIController closestCar = null;
        float minDistance = _frontDetectionRange;

        foreach (var car in allCars)
        {
            if (car == this)
            {
                continue;
            }

            Vector3 dirToCar = car.transform.position - transform.position;
            float distance = dirToCar.magnitude;

            if (distance <= _frontDetectionRange)
            {
                float dot = Vector3.Dot(transform.forward, dirToCar.normalized);

                if (dot > 0.5f && distance < minDistance)
                {
                    minDistance = distance;
                    closestCar = car;
                }
            }
        }

        return closestCar;
    }

    private int GetAdjacentLaneIndex()
    {
        int totalSplines = _splineContainer.Splines.Count;
        List<int> possibleLanes = new List<int>();

        int leftLane = splineIndex - 1;

        if (leftLane >= 0 && _allowedLaneChangeSplines.Contains(leftLane))
        {
            possibleLanes.Add(leftLane);
        }

        int rightLane = splineIndex + 1;

        if (rightLane < totalSplines && _allowedLaneChangeSplines.Contains(rightLane))
        {
            possibleLanes.Add(rightLane);
        }

        if (possibleLanes.Count > 0)
        {
            return possibleLanes[UnityEngine.Random.Range(0, possibleLanes.Count)];
        }

        return splineIndex;
    }

    private void ResetDecisionTimer()
    {
        _decisionTimer = 0f;
        _currentDecisionInterval = UnityEngine.Random.Range(_minDecisionInterval, _maxDecisionInterval);
    }

    private void SetRandomLaneChangeCooldown()
    {
        _laneChangeCooldownTimer = UnityEngine.Random.Range(_minLaneChangeCooldown, _maxLaneChangeCooldown);
    }

    private void ApplyPredictiveSteering(Vector3 targetPosition)
    {
        Vector3 localTarget = transform.InverseTransformPoint(targetPosition);

        if (localTarget.z > -1f)
        {
            float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
            float turnSpeed = _rb.angularVelocity.y * Mathf.Rad2Deg;
            float predictedAngle = angleToTarget - (turnSpeed * _predictionFactor);

            if (Mathf.Abs(predictedAngle) < _steerDeadzone)
            {
                _carController.ResetSteeringAngle();
            }
            else if (predictedAngle > 0f)
            {
                _carController.TurnRight();
            }
            else
            {
                _carController.TurnLeft();
            }
        }
        else
        {
            _carController.ThrottleOff();
            _carController.GoReverse();
            _carController.TurnRight();
        }
    }

    private void ApplySpeedControl(float currentSpeed, float targetSpeedLimit)
    {
        if (currentSpeed > targetSpeedLimit)
        {
            _carController.ThrottleOff();
            _carController.Brakes();
        }
        else
        {
            _carController.CancelInvoke("DecelerateCar");
            _carController.GoForward();
        }
    }

    private float GetAllowedSpeedForPosition()
    {
        float minAllowedSpeed = _maxSpeed;

        foreach (var wp in _trackWaypoints)
        {
            if (wp == null || !wp.isSlowZone)
            {
                continue;
            }

            float distance = Vector3.Distance(
                Vector3.ProjectOnPlane(transform.position, Vector3.up),
                Vector3.ProjectOnPlane(wp.transform.position, Vector3.up)
            );

            if (distance <= wp.zoneRadius)
            {
                float zoneLimit = _maxSpeed * wp.speedMultiplier;

                if (zoneLimit < minAllowedSpeed)
                {
                    minAllowedSpeed = zoneLimit;
                }
            }
        }

        return minAllowedSpeed;
    }

    public void SetSplineIndex(int newIndex)
    {
        if (_splineContainer != null &&
            newIndex >= 0 &&
            newIndex < _splineContainer.Splines.Count)
        {
            splineIndex = newIndex;
        }
    }
}