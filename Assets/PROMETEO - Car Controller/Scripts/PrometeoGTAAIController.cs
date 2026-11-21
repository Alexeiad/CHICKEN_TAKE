using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using Unity.Mathematics;

public enum DrivingLane
{
    Left = -1,
    Center = 0,
    Right = 1
}

public enum AIPersonality
{
    Bully,      // Задира: разгоняется со старта, выжидает, бьет в бок и скидывает соперника за 15м
    Cautious,   // Опасающийся: разгоняется со всеми, но держится аккуратно
    Strategist, // Стратег: умный выбор полос и чистая траектория
    Random
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PrometeoCarController))]
[RequireComponent(typeof(Rigidbody))]
public class PrometeoGTAAIController : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Все числовые настройки ИИ берутся из этого ассета (Bot → AI). Без него используются значения по умолчанию.")]
    [SerializeField] private VehiclesSettings _settings;

    [Header("Spline")]
    [FormerlySerializedAs("splineContainer")]
    [SerializeField] private SplineContainer _splineContainer;

    [Header("AI Personality")]
    [FormerlySerializedAs("personality")]
    [SerializeField] private AIPersonality _personality = AIPersonality.Strategist;

    [Header("Current Lane")]
    [FormerlySerializedAs("currentLane")]
    [SerializeField] private DrivingLane _currentLane = DrivingLane.Center;

    // Рабочие настройки (из ассета или значения по умолчанию)
    private BotAISettings _ai;
    private PersonalitySettings _tuning;

    private DrivingLane _targetLane;
    private bool _isLaneChanging = false;
    private float _laneChangeProgress = 0f;
    private float _laneDecisionCooldown = 0f;

    private bool _isUsingNitro = false;

    private bool _isBullyHunting = false;
    private Transform _bullyTarget = null;
    private float _bullyCooldown = 0f;

    private bool _crashAirborne = false;
    private float _crashTimer = 0f;
    private float _crashCooldownTimer = 0f;
    private float _flipTimer = 0f;

    // Таймер старта (чтобы со старта они сначала разогнались)
    private float _startAccelerationTimer = 3.0f;

    // Компоненты
    private NavMeshAgent _agent;
    private PrometeoCarController _carController;
    private Rigidbody _rb;

    // Внутренние переменные
    private float _normalizedSplineTime = 0f;
    private float _decisionTimer = 0f;
    private float _targetSpeedForCorner = 120f;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _carController = GetComponent<PrometeoCarController>();
        _rb = GetComponent<Rigidbody>();

        if (_settings != null)
        {
            _ai = _settings.bot.ai;
        }
        else
        {
            Debug.LogWarning("PrometeoGTAAIController: VehiclesSettings не назначен, используются значения по умолчанию.", this);
            _ai = new BotAISettings();
        }

        _startAccelerationTimer = _ai.startAccelerationTime;
        _targetSpeedForCorner = _ai.baseMaxSpeed;

        if (_agent != null)
        {
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.updateUpAxis = false;
        }

        InitializePersonality();
        _targetLane = _currentLane;
    }

    private void InitializePersonality()
    {
        if (_personality == AIPersonality.Random)
        {
            AIPersonality[] types = { AIPersonality.Bully, AIPersonality.Cautious, AIPersonality.Strategist };
            _personality = types[UnityEngine.Random.Range(0, types.Length)];
        }

        switch (_personality)
        {
            case AIPersonality.Bully:
                _tuning = _ai.bully;
                break;
            case AIPersonality.Cautious:
                _tuning = _ai.cautious;
                break;
            default:
                _tuning = _ai.strategist;
                break;
        }
    }

    private void Start()
    {
        if (_splineContainer != null && _splineContainer.Splines.Count > 0)
        {
            SnapToRoadOnStart();
        }
    }

    private void Update()
    {
        if (GameState.IsPaused) return;

        if (_splineContainer == null || _splineContainer.Splines.Count == 0) return;

        if (_crashCooldownTimer > 0f) _crashCooldownTimer -= Time.deltaTime;

        if (_crashTimer > 0f)
        {
            _crashTimer -= Time.deltaTime;
            _carController.ThrottleOff();
            _carController.ResetSteeringAngle();
            return;
        }

        if (_crashAirborne)
        {
            if (IsGrounded())
            {
                _crashAirborne = false;
            }
            else
            {
                _carController.ThrottleOff();
                _carController.ResetSteeringAngle();
                return;
            }
        }

        if (HandleFlipRecovery()) return;

        if (_startAccelerationTimer > 0f)
        {
            _startAccelerationTimer -= Time.deltaTime;
            UpdateSplineNavigation();
            return;
        }

        _decisionTimer += Time.deltaTime;
        if (_decisionTimer >= _ai.decisionInterval)
        {
            _decisionTimer = 0f;
            EvaluateAIBehavior();
        }

        if (_bullyCooldown > 0f) _bullyCooldown -= Time.deltaTime;
        if (_laneDecisionCooldown > 0f) _laneDecisionCooldown -= Time.deltaTime;

        if (_isBullyHunting)
        {
            float myOffset = GetCurrentDistanceFromCenter();
            float targetOffset = _bullyTarget != null ? GetDistanceFromCenter(_bullyTarget.position) : 0f;

            if (_bullyTarget == null ||
                myOffset >= (_ai.maxRoadBoundary - _ai.bullyBoundaryMarginSelf) ||
                targetOffset >= (_ai.maxRoadBoundary - _ai.bullyBoundaryMarginTarget))
            {
                _isBullyHunting = false;
                _bullyTarget = null;
                _bullyCooldown = _ai.bullyCooldown;
                SwitchToLane(DrivingLane.Center);
            }
        }

        UpdateLaneTransition();
        UpdateSplineNavigation();
    }

    private void FixedUpdate()
    {
        if (GameState.IsPaused) return;

        if (_carController == null || _rb == null) return;

        if (_crashTimer > 0f || _crashAirborne) return;

        float currentSpeed = Mathf.Abs(_carController.carSpeed);
        _targetSpeedForCorner = EvaluateCorneringSpeed();

        if (_startAccelerationTimer > 0f)
        {
            _carController.CancelInvoke("DecelerateCar");
            _carController.GoForward();
            return;
        }

        bool wantNitro = _targetSpeedForCorner >= _ai.baseMaxSpeed * _tuning.nitroSpeedFraction;
        _isUsingNitro = _tuning.allowNitro && wantNitro;

        float desiredSpeed = _isUsingNitro ? _ai.baseMaxSpeed * _tuning.nitroMultiplier : _targetSpeedForCorner;

        ApplyArcadeGripAndBrakes(currentSpeed, desiredSpeed);
    }

    private void SnapToRoadOnStart()
    {
        Spline spline = _splineContainer.Splines[0];
        Vector3 localPos = _splineContainer.transform.InverseTransformPoint(transform.position);

        SplineUtility.GetNearestPoint(spline, localPos, out float3 nearestPoint, out _normalizedSplineTime);

        Vector3 spawnPosition = GetEvaluatedPositionWithLaneOffset(_normalizedSplineTime, _currentLane);
        Vector3 forwardTangent = GetEvaluatedTangent(_normalizedSplineTime);

        transform.position = spawnPosition;
        if (forwardTangent != Vector3.zero) transform.rotation = Quaternion.LookRotation(forwardTangent, Vector3.up);

        if (_agent != null && _agent.isOnNavMesh) _agent.Warp(spawnPosition);

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.Sleep();
        }
    }

    private Vector3 GetEvaluatedPositionWithLaneOffset(float t, DrivingLane lane)
    {
        Spline spline = _splineContainer.Splines[0];
        float3 localPos = SplineUtility.EvaluatePosition(spline, t);
        float3 localTangent = SplineUtility.EvaluateTangent(spline, t);

        Vector3 worldPos = _splineContainer.transform.TransformPoint((Vector3)localPos);
        Vector3 worldTangent = _splineContainer.transform.TransformDirection((Vector3)localTangent).normalized;

        Vector3 rightDir = Vector3.Cross(Vector3.up, worldTangent).normalized;
        float clampedOffset = Mathf.Clamp((int)lane * _ai.laneOffsetDistance, -_ai.maxRoadBoundary, _ai.maxRoadBoundary);
        return worldPos + (rightDir * clampedOffset);
    }

    private Vector3 GetEvaluatedTangent(float t)
    {
        Spline spline = _splineContainer.Splines[0];
        float3 localTangent = SplineUtility.EvaluateTangent(spline, t);
        return _splineContainer.transform.TransformDirection((Vector3)localTangent).normalized;
    }

    private float GetCurrentDistanceFromCenter()
    {
        return GetDistanceFromCenter(transform.position);
    }

    private float GetDistanceFromCenter(Vector3 pos)
    {
        Spline spline = _splineContainer.Splines[0];
        Vector3 localPos = _splineContainer.transform.InverseTransformPoint(pos);
        SplineUtility.GetNearestPoint(spline, localPos, out _, out float t);
        Vector3 centerPos = _splineContainer.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(spline, t));
        return Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(centerPos.x, 0, centerPos.z));
    }

    private void UpdateSplineNavigation()
    {
        Spline spline = _splineContainer.Splines[0];
        float splineLength = spline.GetLength();
        if (splineLength <= 0f) return;

        Vector3 localPos = _splineContainer.transform.InverseTransformPoint(transform.position);
        SplineUtility.GetNearestPoint(spline, localPos, out _, out _normalizedSplineTime);

        if (GetCurrentDistanceFromCenter() > _ai.maxRoadBoundary)
        {
            _isBullyHunting = false;
            Vector3 centerSafePos = GetEvaluatedPositionWithLaneOffset(_normalizedSplineTime, DrivingLane.Center);
            transform.position = Vector3.Lerp(transform.position, centerSafePos, Time.deltaTime * _ai.emergencyReturnSpeed);
            return;
        }

        float currentSpeed = Mathf.Abs(_carController.carSpeed);
        float dynamicLookAhead = _ai.baseLookAhead + (currentSpeed * _ai.speedLookAheadFactor);
        float targetNormalizedTime = Mathf.Repeat(((_normalizedSplineTime * splineLength) + dynamicLookAhead) / splineLength, 1f);

        DrivingLane activeLane = _isLaneChanging
            ? (_laneChangeProgress < _ai.laneChangeMidpoint ? _currentLane : _targetLane)
            : _currentLane;
        Vector3 worldTargetPos = GetEvaluatedPositionWithLaneOffset(targetNormalizedTime, activeLane);

        if (_isBullyHunting && _bullyTarget != null && _startAccelerationTimer <= 0f)
        {
            worldTargetPos = _bullyTarget.position;
        }

        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.nextPosition = transform.position;
            _agent.SetDestination(worldTargetPos);
        }

        ApplySteering(worldTargetPos);
    }

    private void EvaluateAIBehavior()
    {
        if (_isLaneChanging || _laneDecisionCooldown > 0f) return;

        if (_tuning.canBullyHunt && !_isBullyHunting && _bullyCooldown <= 0f)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _ai.bullySearchRadius);
            foreach (var hit in hits)
            {
                var otherAI = hit.GetComponentInParent<PrometeoGTAAIController>();
                var vehicleScript = hit.GetComponentInParent<Vehicle>();

                Transform enemyTransform = null;
                if (otherAI != null && otherAI != this) enemyTransform = otherAI.transform;
                else if (vehicleScript != null && hit.transform != transform) enemyTransform = hit.transform;

                if (enemyTransform != null)
                {
                    float enemyOffsetFromCenter = GetDistanceFromCenter(enemyTransform.position);
                    if (enemyOffsetFromCenter > _ai.bullyMinTargetOffset)
                    {
                        _isBullyHunting = true;
                        _bullyTarget = enemyTransform;
                        return;
                    }
                }
            }
        }

        Vector3 castOrigin = transform.position + Vector3.up * _ai.obstacleCastOriginHeight;
        if (Physics.SphereCast(castOrigin, _ai.obstacleDetectRadius, transform.forward, out RaycastHit hitInfo, _ai.obstacleDetectDistance))
        {
            var otherAI = hitInfo.collider.GetComponentInParent<PrometeoGTAAIController>();
            var vehicleScript = hitInfo.collider.GetComponentInParent<Vehicle>();

            if ((otherAI != null && otherAI != this) || vehicleScript != null)
            {
                DrivingLane bestLane = SelectBestAvailableLane();
                if (bestLane != _currentLane)
                {
                    SwitchToLane(bestLane);
                    _laneDecisionCooldown = _ai.laneDecisionCooldown;
                }
            }
        }
    }

    private DrivingLane SelectBestAvailableLane()
    {
        DrivingLane[] possibleLanes = { DrivingLane.Left, DrivingLane.Center, DrivingLane.Right };
        DrivingLane bestLane = _currentLane;
        float maxScore = -999f;

        foreach (DrivingLane lane in possibleLanes)
        {
            if (lane == _currentLane) continue;

            Vector3 laneCheckPos = GetEvaluatedPositionWithLaneOffset(_normalizedSplineTime, lane);
            float score = _ai.laneBaseScore;

            if (Mathf.Abs((int)lane * _ai.laneOffsetDistance) > _ai.maxRoadBoundary) continue;

            Vector3 checkOrigin = laneCheckPos + Vector3.up * _ai.obstacleCastOriginHeight;
            if (Physics.SphereCast(checkOrigin, _ai.laneCheckRadius, transform.forward, out _, _ai.laneCheckDistance))
            {
                score -= _ai.laneBlockedPenalty;
            }

            if (lane != DrivingLane.Center)
            {
                score += _tuning.sideLaneBonus;
            }

            if (score > maxScore)
            {
                maxScore = score;
                bestLane = lane;
            }
        }

        return bestLane;
    }

    private void SwitchToLane(DrivingLane newLane)
    {
        if (newLane == _currentLane) return;
        _targetLane = newLane;
        _isLaneChanging = true;
        _laneChangeProgress = 0f;
    }

    private void UpdateLaneTransition()
    {
        if (!_isLaneChanging) return;

        _laneChangeProgress += Time.deltaTime * _tuning.laneChangeSpeed;
        if (_laneChangeProgress >= 1f)
        {
            _currentLane = _targetLane;
            _isLaneChanging = false;
        }
    }

    private float EvaluateCorneringSpeed()
    {
        Spline spline = _splineContainer.Splines[0];
        float splineLength = spline.GetLength();
        if (splineLength <= 0f) return _ai.baseMaxSpeed;

        float minDot = 1f;
        int checkSteps = Mathf.Max(1, _ai.cornerCheckSteps);

        for (int i = 1; i <= checkSteps; i++)
        {
            float dist = (_ai.cornerCheckDistance / checkSteps) * i;
            float checkTime = Mathf.Repeat(_normalizedSplineTime + (dist / splineLength), 1f);

            Vector3 currentDir = GetEvaluatedTangent(_normalizedSplineTime);
            Vector3 futureDir = GetEvaluatedTangent(checkTime);

            float dot = Vector3.Dot(currentDir, futureDir);
            if (dot < minDot) minDot = dot;
        }

        if (minDot < _ai.cornerSharpnessDot)
        {
            float cornerFactor = Mathf.InverseLerp(_ai.cornerMinDot, _ai.cornerSharpnessDot, minDot);
            return Mathf.Lerp(_ai.baseMaxSpeed * _tuning.cornerBrakeStrength, _ai.baseMaxSpeed, cornerFactor);
        }

        return _ai.baseMaxSpeed;
    }

    private void ApplyArcadeGripAndBrakes(float currentSpeed, float desiredSpeed)
    {
        if (currentSpeed > desiredSpeed + _ai.brakeOvershootTolerance)
        {
            _carController.ThrottleOff();
            _carController.Brakes();
            _rb.AddForce(-_rb.velocity.normalized * (_ai.physicalBrakePower * _ai.brakeForceMultiplier), ForceMode.Acceleration);
        }
        else if (currentSpeed < desiredSpeed)
        {
            _carController.CancelInvoke("DecelerateCar");
            _carController.GoForward();
        }
        else
        {
            _carController.ThrottleOff();
        }

        if (_targetSpeedForCorner < _ai.baseMaxSpeed * _ai.gripAlignSpeedFraction &&
            _rb.velocity.sqrMagnitude > _ai.gripAlignMinSqrSpeed)
        {
            Vector3 roadTangent = GetEvaluatedTangent(_normalizedSplineTime);
            _rb.velocity = Vector3.Lerp(_rb.velocity, roadTangent * currentSpeed, Time.fixedDeltaTime * _ai.gripAlignRate);
        }
    }

    private void ApplySteering(Vector3 targetPosition)
    {
        Vector3 localTarget = transform.InverseTransformPoint(targetPosition);
        float angleToTarget = Mathf.Atan2(localTarget.x, Mathf.Abs(localTarget.z)) * Mathf.Rad2Deg;
        float turnSpeed = _rb.angularVelocity.y * Mathf.Rad2Deg;
        float predictedAngle = angleToTarget - (turnSpeed * _ai.predictionFactor);

        float steerInput = Mathf.Abs(predictedAngle) < _ai.steerDeadzone
            ? 0f
            : Mathf.Clamp(predictedAngle / _carController.MaxSteeringAngle, -1f, 1f);

        _carController.SetSteering(steerInput);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (GameState.IsPaused || _rb == null) return;
        if (_crashCooldownTimer > 0f || _startAccelerationTimer > 0f) return;

        ContactPoint contact = collision.GetContact(0);

        if (contact.normal.y > _ai.groundNormalMinY) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < _ai.crashImpactThreshold) return;

        float impact = Mathf.InverseLerp(
            _ai.crashImpactThreshold,
            _ai.crashImpactThreshold * _ai.crashMaxImpactMultiplier,
            impactSpeed);

        float upSpeed = Mathf.Lerp(_ai.launchUpSpeedMin, _ai.launchUpSpeedMax, impact);
        _rb.AddForce(Vector3.up * upSpeed, ForceMode.VelocityChange);

        Vector3 tumbleAxis = Vector3.Cross(Vector3.up, contact.normal);
        if (tumbleAxis.sqrMagnitude < 0.001f) tumbleAxis = transform.right;
        tumbleAxis.Normalize();

        Vector3 torque =
            tumbleAxis * (_ai.launchTumbleSpeed * impact) +
            Vector3.up * (UnityEngine.Random.Range(-1f, 1f) * _ai.launchYawSpeed * impact);
        _rb.AddTorque(torque, ForceMode.VelocityChange);

        _isBullyHunting = false;
        _bullyTarget = null;

        _crashTimer = _ai.crashRecoveryTime * (_ai.crashRecoveryMinFactor + impact);
        _crashAirborne = true;
        _crashCooldownTimer = _ai.crashCooldown;
        _flipTimer = 0f;
    }

    private bool HandleFlipRecovery()
    {
        bool isFlipped = Vector3.Dot(transform.up, Vector3.up) < _ai.flipUpDot;

        if (!isFlipped || _rb.velocity.sqrMagnitude > _ai.flipMaxSqrSpeed)
        {
            _flipTimer = 0f;
            return false;
        }

        _flipTimer += Time.deltaTime;
        _carController.ThrottleOff();

        if (_flipTimer >= _ai.flipRecoveryTime)
        {
            _flipTimer = 0f;

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;

            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position += Vector3.up * _ai.flipLiftHeight;
            _rb.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }

        return true;
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * _ai.groundCheckOriginHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, _ai.groundCheckDistance, ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit h in hits)
        {
            if (h.rigidbody != _rb) return true;
        }

        return false;
    }

    public void SetLane(DrivingLane newLane)
    {
        SwitchToLane(newLane);
    }
}