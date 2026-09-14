using System;
using UnityEngine;
using Zenject;

public abstract class ChickenCollectorBase : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] protected Transform _rayOrigin;
    [SerializeField] protected float _rayDistance = 1.0f;
    [SerializeField] protected LayerMask _chickenLayer;

    [Header("Sell Zone")]
    [SerializeField] protected Transform _sellPoint;
    [SerializeField] protected float _sellDistance = 2.0f;

    [Header("Chicken Collection")]
    [SerializeField] protected int _maxCollectedCount = 15;

    protected int _collectedCount;
    protected IEntityRegistry<IEntity> _entityRegistry;
    protected bool _wasInSellRange;

    public int CollectedCount => _collectedCount;
    public int MaxCollectedCount => _maxCollectedCount;

    public string CollectedCountText =>
        $"{_collectedCount}/{_maxCollectedCount}";

    public event Action OnChickenCountChanged;
    public event Action<int, int> OnSell;

    [Inject]
    private void Construct(IEntityRegistry<IEntity> entityRegistry)
    {
        _entityRegistry = entityRegistry;
    }

    protected virtual void Start()
    {
    }

    protected virtual void Update()
    {
        CheckForChicken();
        CheckSellZone();
    }

    private void CheckForChicken()
    {
        if (_collectedCount >= _maxCollectedCount)
            return;

        Transform origin = _rayOrigin != null
            ? _rayOrigin
            : transform;

        if (!Physics.Raycast(
                origin.position,
                origin.forward,
                out RaycastHit hit,
                _rayDistance,
                _chickenLayer))
        {
            return;
        }

        if (hit.collider.TryGetComponent<Chicken>(out var chicken))
            Collect(chicken);
    }

    private void Collect(Chicken chicken)
    {
        if (_collectedCount >= _maxCollectedCount)
            return;

        if (chicken is IEntity entity)
            _entityRegistry?.Unregister(entity);

        _collectedCount++;

        OnCollected();
        OnChickenCountChanged?.Invoke();

        Destroy(chicken.gameObject);
    }

    protected virtual void OnCollected()
    {
    }

    private void CheckSellZone()
    {
        if (_sellPoint == null)
            return;

        bool inRange =
            Vector3.Distance(
                transform.position,
                _sellPoint.position
            ) <= _sellDistance;

        if (inRange && !_wasInSellRange && _collectedCount > 0)
            SellAllChickens();

        _wasInSellRange = inRange;
    }

    public void SellAllChickens()
    {
        if (_collectedCount <= 0)
            return;

        int chickensSold = _collectedCount;
        int coinsEarned = chickensSold * 5;

        _collectedCount = 0;

        OnChickenCountChanged?.Invoke();
        OnSell?.Invoke(coinsEarned, chickensSold);

        OnSold(coinsEarned, chickensSold);
    }

    protected abstract void OnSold(
        int coinsEarned,
        int chickensSold);
}