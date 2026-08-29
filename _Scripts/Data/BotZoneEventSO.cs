using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "BotZoneEvent", menuName = "Game Events/Bot Zone Event")]
public class BotZoneEventSO : ZoneEventBase
{
    public UnityAction<int, int> OnSell;

    public void Sell(int coins, int chickens) => OnSell?.Invoke(coins, chickens);
}