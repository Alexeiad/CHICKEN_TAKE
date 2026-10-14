using FMODUnity;
using UnityEngine;

public class PlayerChickenCollector : ChickenCollectorBase
{
    [Header("Data")]
    [SerializeField] private PlayerDataSO _data;

    [Header("Zone Event")]
    [SerializeField] private PlayerZoneEventSO _zoneEvent;

    [Header("Audio")]
    [SerializeField] private EventReference _collectEvent;
    [SerializeField] private EventReference _sellEvent;
    [SerializeField] private Vector2 _pitchRange = new Vector2(0.9f, 1.1f);

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

        PlaySound(_collectEvent);
    }

    protected override void OnSold(int coinsEarned, int chickensSold)
    {
        if (_data != null)
        {
            _data.RecordSale(coinsEarned, chickensSold);
        }
        _zoneEvent?.Sell(coinsEarned, chickensSold);

        PlaySound(_sellEvent);
    }

    private void PlaySound(EventReference eventReference)
    {
        if (eventReference.IsNull)
            return;

        var instance = RuntimeManager.CreateInstance(eventReference);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
        instance.setPitch(Random.Range(_pitchRange.x, _pitchRange.y));
        instance.start();
        instance.release();
    }
}