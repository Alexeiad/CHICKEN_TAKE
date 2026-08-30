using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class UFOController : MonoBehaviour
{
    private enum UFOState
    {
        Patrol,
        Targeting,
        Abduction,
        ReturningToPatrol
    }

    [Header("Patrol")]
    [SerializeField] private float _patrolRadius = 100f;
    [SerializeField] private float _minPatrolHeight = 20f;
    [SerializeField] private float _maxPatrolHeight = 80f;
    [SerializeField] private float _patrolSpeed = 12f;

    [Header("Patrol Randomization")]
    [SerializeField] private float _minDirectionChangeTime = 3f;
    [SerializeField] private float _maxDirectionChangeTime = 8f;

    [Header("Targeting")]
    [SerializeField] private float _aboveChickenDistance = 1f;
    [SerializeField] private float _targetingSpeed = 15f;

    [Header("Abduction")]
    [SerializeField] private float _abductionHeight = 10f;
    [SerializeField] private float _chickenFlySpeed = 20f;
    [SerializeField] private float _chickenCatchDistance = 1f;
    [SerializeField] private float _descentSpeed = 8f;

    [Header("Vehicle")]
    [SerializeField] private float _vehicleEscapeDistance = 5f;

    [Header("State Timing")]
    [SerializeField] private float _minPatrolTime = 5f;
    [SerializeField] private float _maxPatrolTime = 15f;

    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed = 90f;

    private Chicken.Factory _chickenFactory;

    private UFOState _state;

    private Vector3 _startPosition;
    private Vector3 _patrolTarget;
    private Vector3 _abductionPosition;

    private float _stateTimer;
    private float _directionChangeTimer;

    private Chicken _targetChicken;

    [Inject]
    private void Construct(Chicken.Factory chickenFactory)
    {
        _chickenFactory = chickenFactory;
    }

    private void Start()
    {
        _startPosition = transform.position;

        EnterPatrol();
    }

    private void Update()
    {
        RotateAroundY();

        if (CheckVehicleNearby())
            return;

        switch (_state)
        {
            case UFOState.Patrol:
                UpdatePatrol();
                break;

            case UFOState.Targeting:
                UpdateTargeting();
                break;

            case UFOState.Abduction:
                UpdateAbduction();
                break;

            case UFOState.ReturningToPatrol:
                UpdateReturningToPatrol();
                break;
        }
    }

    private void RotateAroundY()
    {
        transform.Rotate(
            0f,
            _rotationSpeed * Time.deltaTime,
            0f,
            Space.Self
        );
    }

    #region Vehicle

    private bool CheckVehicleNearby()
    {
        Vehicle[] vehicles = FindObjectsByType<Vehicle>(
            FindObjectsSortMode.None
        );

        foreach (Vehicle vehicle in vehicles)
        {
            if (vehicle == null)
                continue;

            if (!vehicle.gameObject.activeInHierarchy)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                vehicle.transform.position
            );

            if (distance < _vehicleEscapeDistance)
            {
                EscapeFromVehicle();
                return true;
            }
        }

        return false;
    }

    private void EscapeFromVehicle()
    {
        transform.position = _startPosition;

        _targetChicken = null;

        EnterPatrol();
    }

    #endregion

    #region Patrol

    private void EnterPatrol()
    {
        _state = UFOState.Patrol;

        _stateTimer = Random.Range(
            _minPatrolTime,
            _maxPatrolTime
        );

        _directionChangeTimer = 0f;

        _targetChicken = null;

        ChooseNewPatrolTarget();
    }

    private void UpdatePatrol()
    {
        _stateTimer -= Time.deltaTime;
        _directionChangeTimer -= Time.deltaTime;

        if (_stateTimer <= 0f)
        {
            TryStartAbduction();

            if (_state == UFOState.Patrol)
            {
                _stateTimer = Random.Range(
                    _minPatrolTime,
                    _maxPatrolTime
                );
            }

            return;
        }

        if (_directionChangeTimer <= 0f)
        {
            ChooseNewPatrolTarget();
        }

        MoveTowards(
            _patrolTarget,
            _patrolSpeed
        );

        ClampToPatrolArea();
    }

    private void ChooseNewPatrolTarget()
    {
        Vector2 randomCircle =
            Random.insideUnitCircle * _patrolRadius;

        float randomHeight = Random.Range(
            _minPatrolHeight,
            _maxPatrolHeight
        );

        _patrolTarget = new Vector3(
            _startPosition.x + randomCircle.x,
            _startPosition.y + randomHeight,
            _startPosition.z + randomCircle.y
        );

        _directionChangeTimer = Random.Range(
            _minDirectionChangeTime,
            _maxDirectionChangeTime
        );
    }

    private void ClampToPatrolArea()
    {
        Vector3 offset =
            transform.position - _startPosition;

        Vector2 horizontalOffset = new Vector2(
            offset.x,
            offset.z
        );

        if (horizontalOffset.magnitude > _patrolRadius)
        {
            horizontalOffset =
                horizontalOffset.normalized * _patrolRadius;

            transform.position = new Vector3(
                _startPosition.x + horizontalOffset.x,
                transform.position.y,
                _startPosition.z + horizontalOffset.y
            );
        }

        float minY =
            _startPosition.y + _minPatrolHeight;

        float maxY =
            _startPosition.y + _maxPatrolHeight;

        Vector3 position = transform.position;

        position.y = Mathf.Clamp(
            position.y,
            minY,
            maxY
        );

        transform.position = position;
    }

    #endregion

    #region Targeting

    private void EnterTargeting(Chicken chicken)
    {
        if (chicken == null)
        {
            EnterPatrol();
            return;
        }

        _state = UFOState.Targeting;
        _targetChicken = chicken;
    }

    private void UpdateTargeting()
    {
        if (!IsChickenValid())
        {
            EnterPatrol();
            return;
        }

        Vector3 targetPosition = new Vector3(
            _targetChicken.transform.position.x,
            transform.position.y,
            _targetChicken.transform.position.z
        );

        MoveTowards(
            targetPosition,
            _targetingSpeed
        );

        Vector3 horizontalOffset = new Vector3(
            transform.position.x -
            _targetChicken.transform.position.x,

            0f,

            transform.position.z -
            _targetChicken.transform.position.z
        );

        if (horizontalOffset.magnitude <= _aboveChickenDistance)
        {
            EnterAbduction();
        }
    }

    #endregion

    #region Abduction

    private void EnterAbduction()
    {
        if (!IsChickenValid())
        {
            EnterPatrol();
            return;
        }

        _state = UFOState.Abduction;

        _abductionPosition = new Vector3(
            _targetChicken.transform.position.x,
            _targetChicken.transform.position.y +
            _abductionHeight,
            _targetChicken.transform.position.z
        );
    }

    private void UpdateAbduction()
    {
        if (!IsChickenValid())
        {
            EnterReturningToPatrol();
            return;
        }

        if (Vector3.Distance(
                transform.position,
                _abductionPosition
            ) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                _abductionPosition,
                _descentSpeed * Time.deltaTime
            );

            return;
        }

        Transform chickenTransform =
            _targetChicken.transform;

        chickenTransform.position = Vector3.MoveTowards(
            chickenTransform.position,
            transform.position,
            _chickenFlySpeed * Time.deltaTime
        );

        float distance = Vector3.Distance(
            chickenTransform.position,
            transform.position
        );

        if (distance <= _chickenCatchDistance)
        {
            Destroy(_targetChicken.gameObject);

            _targetChicken = null;

            EnterReturningToPatrol();
        }
    }

    #endregion

    #region Returning

    private void EnterReturningToPatrol()
    {
        _state = UFOState.ReturningToPatrol;
    }

    private void UpdateReturningToPatrol()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            _startPosition,
            _patrolSpeed * Time.deltaTime
        );

        if (Vector3.Distance(
                transform.position,
                _startPosition
            ) <= 0.1f)
        {
            transform.position = _startPosition;

            EnterPatrol();
        }
    }

    #endregion

    #region Target Selection

    private void TryStartAbduction()
    {
        Chicken chicken = GetRandomChicken();

        if (chicken == null)
            return;

        EnterTargeting(chicken);
    }

    private Chicken GetRandomChicken()
    {
        IReadOnlyCollection<Chicken> chickens =
            Chicken.ActiveChickens;

        if (chickens == null || chickens.Count == 0)
            return null;

        List<Chicken> validChickens = new();

        foreach (Chicken chicken in chickens)
        {
            if (chicken == null)
                continue;

            if (!chicken.gameObject.activeInHierarchy)
                continue;

            validChickens.Add(chicken);
        }

        if (validChickens.Count == 0)
            return null;

        int randomIndex = Random.Range(
            0,
            validChickens.Count
        );

        return validChickens[randomIndex];
    }

    private bool IsChickenValid()
    {
        return _targetChicken != null &&
               _targetChicken.gameObject.activeInHierarchy;
    }

    #endregion

    #region Movement

    private void MoveTowards(
        Vector3 target,
        float speed
    )
    {
        Vector3 direction =
            target - transform.position;

        if (direction.sqrMagnitude < 0.01f)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            speed * Time.deltaTime
        );
    }

    #endregion
}