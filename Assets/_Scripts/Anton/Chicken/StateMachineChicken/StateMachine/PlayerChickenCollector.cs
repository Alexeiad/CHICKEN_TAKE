using UnityEngine;

public class PlayerChickenCollector : ChickenCollectorBase
{
    [Header("Data")]
    [SerializeField] private PlayerDataSO _data;

    [Header("Zone Event")]
    [SerializeField] private PlayerZoneEventSO _zoneEvent;

    protected override void Start()
    {
        if (_data != null)
            _collectedCount = _data.Chicken;
    }

    protected override void OnCollected()
    {
        if (_data != null)
        {
            _data.RecordCollection(_collectedCount);
        }
    }

    protected override void OnSold(int coinsEarned, int chickensSold)
    {
        if (_data != null)
        {
            _data.RecordSale(coinsEarned, chickensSold);
        }
        _zoneEvent?.Sell(coinsEarned, chickensSold);
    }
}
