using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
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
    [Header("Spline Setup & Boundaries")]
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private float laneOffsetDistance = 5f;
    [Tooltip("Максимальная граница трассы (15м). Задира и ИИ не пересекают её при маневрах.")]
    [SerializeField] private float maxRoadBoundary = 15f;

    [Header("AI Personality")]
    [SerializeField] private AIPersonality personality = AIPersonality.Strategist;

    [Header("Current Lane")]
    [SerializeField] private DrivingLane currentLane = DrivingLane.Center;
    private DrivingLane targetLane;
    private bool isLaneChanging = false;
    private float laneChangeProgress = 0f;
    private float laneChangeSpeed = 5f;
    private float laneDecisionCooldown = 0f;

    [Header("Speed & Physics")]
    [SerializeField] private float baseMaxSpeed = 120f;
    [SerializeField] private float physicalBrakePower = 45f;
    private float nitroMultiplier = 1.45f;
    private bool isUsingNitro = false;

    [Header("Cornering & Grip")]
    [SerializeField] private float baseLookAhead = 22f;
    [SerializeField] private float speedLookAheadFactor = 0.15f;
    [SerializeField] private float cornerCheckDistance = 40f;

    [Header("Bully Tactics (Задира)")]
    [SerializeField] private float bullySearchRadius = 30f;
    private bool isBullyHunting = false;
    private Transform bullyTarget = null;
    private float bullyCooldown = 0f;

    [Header("Steering")]
    [SerializeField] private float predictionFactor = 0.15f;
    [SerializeField] private float steerDeadzone = 1.5f;

    // Таймер старта (чтобы со старта они сначала разогнались)
    private float startAccelerationTimer = 3.0f;

    // Компоненты
    private NavMeshAgent agent;
    private PrometeoCarController carController;
    private Rigidbody rb;

    // Внутренние переменные
    private float normalizedSplineTime = 0f;
    private float decisionTimer = 0f;
    private float targetSpeedForCorner = 120f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        carController = GetComponent<PrometeoCarController>();
        rb = GetComponent<Rigidbody>();

        if (agent != null)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.updateUpAxis = false;
        }

        InitializePersonality();
        targetLane = currentLane;
    }

    private void InitializePersonality()
    {
        if (personality == AIPersonality.Random)
        {
            AIPersonality[] types = { AIPersonality.Bully, AIPersonality.Cautious, AIPersonality.Strategist };
            personality = types[UnityEngine.Random.Range(0, types.Length)];
        }

        switch (personality)
        {
            case AIPersonality.Bully:
                nitroMultiplier = 1.65f;
                laneChangeSpeed = 8.0f;
                break;
            case AIPersonality.Cautious:
                nitroMultiplier = 1.15f;
                laneChangeSpeed = 2.5f;
                break;
            case AIPersonality.Strategist:
                nitroMultiplier = 1.35f;
                laneChangeSpeed = 5.0f;
                break;
        }
    }

    private void Start()
    {
        if (splineContainer != null && splineContainer.Splines.Count > 0)
        {
            SnapToRoadOnStart();
        }
    }

    private void Update()
    {
        if (splineContainer == null || splineContainer.Splines.Count == 0) return;

        // Отсчет стартового разгона (первые 3 секунды никому не мешаем, набираем скорость)
        if (startAccelerationTimer > 0f)
        {
            startAccelerationTimer -= Time.deltaTime;
            UpdateSplineNavigation();
            return;
        }

        decisionTimer += Time.deltaTime;
        if (decisionTimer >= 0.15f)
        {
            decisionTimer = 0f;
            EvaluateAABehavior();
        }

        if (bullyCooldown > 0f) bullyCooldown -= Time.deltaTime;
        if (laneDecisionCooldown > 0f) laneDecisionCooldown -= Time.deltaTime;

        // Отмена тарана задиры при подходе к опасной границе
        if (isBullyHunting)
        {
            float myOffset = GetCurrentDistanceFromCenter();
            float targetOffset = bullyTarget != null ? GetDistanceFromCenter(bullyTarget.position) : 0f;

            if (bullyTarget == null || myOffset >= (maxRoadBoundary - 2.5f) || targetOffset >= (maxRoadBoundary - 1f))
            {
                isBullyHunting = false;
                bullyTarget = null;
                bullyCooldown = 4.0f;
                SwitchToLane(DrivingLane.Center);
            }
        }

        UpdateLaneTransition();
        UpdateSplineNavigation();
    }

    private void FixedUpdate()
    {
        if (carController == null || rb == null) return;

        float currentSpeed = Mathf.Abs(carController.carSpeed);
        targetSpeedForCorner = EvaluateCorneringSpeed();

        // В самом начале гонки (первые 3 секунды) — газ в пол без ограничений для быстрого старта!
        if (startAccelerationTimer > 0f)
        {
            carController.CancelInvoke("DecelerateCar");
            carController.GoForward();
            return;
        }

        bool wantNitro = (personality == AIPersonality.Bully) ? (targetSpeedForCorner >= baseMaxSpeed * 0.85f) : (targetSpeedForCorner >= baseMaxSpeed * 0.95f);
        isUsingNitro = allowNitroBoostCheck() && wantNitro;

        float desiredSpeed = isUsingNitro ? baseMaxSpeed * nitroMultiplier : targetSpeedForCorner;

        ApplyArcadeGripAndBrakes(currentSpeed, desiredSpeed);
    }

    private bool allowNitroBoostCheck()
    {
        return personality != AIPersonality.Cautious;
    }

    private void SnapToRoadOnStart()
    {
        Spline spline = splineContainer.Splines[0];
        Vector3 localPos = splineContainer.transform.InverseTransformPoint(transform.position);

        SplineUtility.GetNearestPoint(spline, localPos, out float3 nearestPoint, out normalizedSplineTime);

        Vector3 spawnPosition = GetEvaluatedPositionWithLaneOffset(normalizedSplineTime, currentLane);
        Vector3 forwardTangent = GetEvaluatedTangent(normalizedSplineTime);

        transform.position = spawnPosition;
        if (forwardTangent != Vector3.zero) transform.rotation = Quaternion.LookRotation(forwardTangent, Vector3.up);

        if (agent != null && agent.isOnNavMesh) agent.Warp(spawnPosition);

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }

    private Vector3 GetEvaluatedPositionWithLaneOffset(float t, DrivingLane lane)
    {
        Spline spline = splineContainer.Splines[0];
        float3 localPos = SplineUtility.EvaluatePosition(spline, t);
        float3 localTangent = SplineUtility.EvaluateTangent(spline, t);

        Vector3 worldPos = splineContainer.transform.TransformPoint((Vector3)localPos);
        Vector3 worldTangent = splineContainer.transform.TransformDirection((Vector3)localTangent).normalized;

        Vector3 rightDir = Vector3.Cross(Vector3.up, worldTangent).normalized;
        float clampedOffset = Mathf.Clamp((int)lane * laneOffsetDistance, -maxRoadBoundary, maxRoadBoundary);
        return worldPos + (rightDir * clampedOffset);
    }

    private Vector3 GetEvaluatedTangent(float t)
    {
        Spline spline = splineContainer.Splines[0];
        float3 localTangent = SplineUtility.EvaluateTangent(spline, t);
        return splineContainer.transform.TransformDirection((Vector3)localTangent).normalized;
    }

    private float GetCurrentDistanceFromCenter()
    {
        return GetDistanceFromCenter(transform.position);
    }

    private float GetDistanceFromCenter(Vector3 pos)
    {
        Spline spline = splineContainer.Splines[0];
        Vector3 localPos = splineContainer.transform.InverseTransformPoint(pos);
        SplineUtility.GetNearestPoint(spline, localPos, out _, out float t);
        Vector3 centerPos = splineContainer.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(spline, t));
        return Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(centerPos.x, 0, centerPos.z));
    }

    private void UpdateSplineNavigation()
    {
        Spline spline = splineContainer.Splines[0];
        float splineLength = spline.GetLength();
        if (splineLength <= 0f) return;

        Vector3 localPos = splineContainer.transform.InverseTransformPoint(transform.position);
        SplineUtility.GetNearestPoint(spline, localPos, out _, out normalizedSplineTime);

        // АВАРИЙНЫЙ ВОЗВРАТ: Если тачку вынесло за 15 метров
        if (GetCurrentDistanceFromCenter() > maxRoadBoundary)
        {
            isBullyHunting = false;
            Vector3 centerSafePos = GetEvaluatedPositionWithLaneOffset(normalizedSplineTime, DrivingLane.Center);
            transform.position = Vector3.Lerp(transform.position, centerSafePos, Time.deltaTime * 6f);
            return;
        }

        float currentSpeed = Mathf.Abs(carController.carSpeed);
        float dynamicLookAhead = baseLookAhead + (currentSpeed * speedLookAheadFactor);
        float targetNormalizedTime = Mathf.Repeat(((normalizedSplineTime * splineLength) + dynamicLookAhead) / splineLength, 1f);

        DrivingLane activeLane = isLaneChanging ? (laneChangeProgress < 0.5f ? currentLane : targetLane) : currentLane;
        Vector3 worldTargetPos = GetEvaluatedPositionWithLaneOffset(targetNormalizedTime, activeLane);

        // Преследование задиры (включается только после окончания стартового разгона)
        if (isBullyHunting && bullyTarget != null && startAccelerationTimer <= 0f)
        {
            worldTargetPos = bullyTarget.position;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
            agent.SetDestination(worldTargetPos);
        }

        ApplySteering(worldTargetPos);
    }

    private void EvaluateAABehavior()
    {
        if (isLaneChanging || laneDecisionCooldown > 0f) return;

        // 1. Поведение ЗАДИРЫ: ищет жертву для выталкивания
        if (personality == AIPersonality.Bully && !isBullyHunting && bullyCooldown <= 0f)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, bullySearchRadius);
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
                    if (enemyOffsetFromCenter > 4f)
                    {
                        isBullyHunting = true;
                        bullyTarget = enemyTransform;
                        return;
                    }
                }
            }
        }

        // 2. Умный выбор полосы при препятствии впереди
        if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, 1.8f, transform.forward, out RaycastHit hitInfo, 22f))
        {
            var otherAI = hitInfo.collider.GetComponentInParent<PrometeoGTAAIController>();
            var vehicleScript = hitInfo.collider.GetComponentInParent<Vehicle>();

            if ((otherAI != null && otherAI != this) || vehicleScript != null)
            {
                DrivingLane bestLane = SelectBestAvailableLane();
                if (bestLane != currentLane)
                {
                    SwitchToLane(bestLane);
                    laneDecisionCooldown = 2.0f;
                }
            }
        }
    }

    private DrivingLane SelectBestAvailableLane()
    {
        DrivingLane[] possibleLanes = { DrivingLane.Left, DrivingLane.Center, DrivingLane.Right };
        DrivingLane bestLane = currentLane;
        float maxScore = -999f;

        foreach (DrivingLane lane in possibleLanes)
        {
            if (lane == currentLane) continue;

            Vector3 laneCheckPos = GetEvaluatedPositionWithLaneOffset(normalizedSplineTime, lane);
            float score = 10f;

            if (Mathf.Abs((int)lane * laneOffsetDistance) > maxRoadBoundary) continue;

            if (Physics.SphereCast(laneCheckPos + Vector3.up * 0.5f, 1.5f, transform.forward, out _, 15f))
            {
                score -= 20f;
            }

            if (personality == AIPersonality.Cautious && lane != DrivingLane.Center)
            {
                score += 5f;
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
        if (newLane == currentLane) return;
        targetLane = newLane;
        isLaneChanging = true;
        laneChangeProgress = 0f;
    }

    private void UpdateLaneTransition()
    {
        if (!isLaneChanging) return;

        laneChangeProgress += Time.deltaTime * laneChangeSpeed;
        if (laneChangeProgress >= 1f)
        {
            currentLane = targetLane;
            isLaneChanging = false;
        }
    }

    private float EvaluateCorneringSpeed()
    {
        Spline spline = splineContainer.Splines[0];
        float splineLength = spline.GetLength();
        if (splineLength <= 0f) return baseMaxSpeed;

        float minDot = 1f;
        int checkSteps = 10;

        for (int i = 1; i <= checkSteps; i++)
        {
            float dist = (cornerCheckDistance / checkSteps) * i;
            float checkTime = Mathf.Repeat(normalizedSplineTime + (dist / splineLength), 1f);

            Vector3 currentDir = GetEvaluatedTangent(normalizedSplineTime);
            Vector3 futureDir = GetEvaluatedTangent(checkTime);

            float dot = Vector3.Dot(currentDir, futureDir);
            if (dot < minDot) minDot = dot;
        }

        if (minDot < 0.94f)
        {
            float brakeStrength = (personality == AIPersonality.Cautious) ? 0.35f : (personality == AIPersonality.Bully ? 0.6f : 0.45f);
            float cornerFactor = Mathf.InverseLerp(-0.3f, 0.94f, minDot);
            return Mathf.Lerp(baseMaxSpeed * brakeStrength, baseMaxSpeed, cornerFactor);
        }

        return baseMaxSpeed;
    }

    private void ApplyArcadeGripAndBrakes(float currentSpeed, float desiredSpeed)
    {
        if (currentSpeed > desiredSpeed + 1f)
        {
            carController.ThrottleOff();
            carController.Brakes();
            rb.AddForce(-rb.velocity.normalized * (physicalBrakePower * 1.5f), ForceMode.Acceleration);
        }
        else if (currentSpeed < desiredSpeed)
        {
            carController.CancelInvoke("DecelerateCar");
            carController.GoForward();
        }
        else
        {
            carController.ThrottleOff();
        }

        if (targetSpeedForCorner < baseMaxSpeed * 0.7f && rb.velocity.sqrMagnitude > 25f)
        {
            Vector3 roadTangent = GetEvaluatedTangent(normalizedSplineTime);
            rb.velocity = Vector3.Lerp(rb.velocity, roadTangent * currentSpeed, Time.fixedDeltaTime * 3f);
        }
    }

    private void ApplySteering(Vector3 targetPosition)
    {
        Vector3 localTarget = transform.InverseTransformPoint(targetPosition);
        float angleToTarget = Mathf.Atan2(localTarget.x, Mathf.Abs(localTarget.z)) * Mathf.Rad2Deg;
        float turnSpeed = rb.angularVelocity.y * Mathf.Rad2Deg;
        float predictedAngle = angleToTarget - (turnSpeed * predictionFactor);

        if (Mathf.Abs(predictedAngle) < steerDeadzone)
        {
            carController.ResetSteeringAngle();
        }
        else if (predictedAngle > 0f)
        {
            carController.TurnRight();
        }
        else
        {
            carController.TurnLeft();
        }
    }

    public void SetLane(DrivingLane newLane)
    {
        SwitchToLane(newLane);
    }
}