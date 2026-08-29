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
            _data.Chicken = _collectedCount;
            _data.InvokeDataChanged();
        }
    }

    protected override void OnSold(int coinsEarned, int chickensSold)
    {
        if (_data != null)
        {
            _data.Cash += coinsEarned;
            _data.Chicken = 0;
            _data.InvokeDataChanged();
        }
        _zoneEvent?.Sell(coinsEarned, chickensSold);
    }
}