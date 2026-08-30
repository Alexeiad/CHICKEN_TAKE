using System;
using UnityEngine;
using Zenject;

public class BotChickenCollector : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] private Transform _rayOrigin;
    [SerializeField] private float _rayDistance = 1.0f;
    [SerializeField] private LayerMask _chickenLayer;

    [Header("Sell Zone")]
    [SerializeField] private Transform _sellPoint;
    [SerializeField] private float _sellDistance = 2.0f;

    private int _collectedCount;

    private IEntityRegistry<IEntity> _entityRegistry;

    private bool _wasInSellRange;

    private BotDataSO _data;

    public int CollectedCount => _collectedCount;
    public BotDataSO Data => _data;

    public event Action<int, int> OnSell;

    [Inject]
    private void Construct(IEntityRegistry<IEntity> entityRegistry)
    {
        _entityRegistry = entityRegistry;
    }

    private void Awake()
    {
        _data = ScriptableObject.CreateInstance<BotDataSO>();

        _collectedCount = 0;

        _data.Chicken = 0;
        _data.Cash = 0;
    }

    private void Update()
    {
        CheckForChicken();
        CheckSellZone();
    }

    private void CheckForChicken()
    {
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
        if (chicken is IEntity entity)
            _entityRegistry?.Unregister(entity);

        _collectedCount++;

        _data.Chicken = _collectedCount;

        Destroy(chicken.gameObject);
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

        _data.Chicken = 0;
        _data.Cash += coinsEarned;

        OnSell?.Invoke(
            coinsEarned,
            chickensSold
        );
    }

    public void RemoveChickens(int amount)
    {
        if (_data == null)
            return;

        amount = Mathf.Clamp(
            amount,
            0,
            _collectedCount
        );

        if (amount <= 0)
            return;

        _collectedCount -= amount;

        _data.Chicken = _collectedCount;
    }
}