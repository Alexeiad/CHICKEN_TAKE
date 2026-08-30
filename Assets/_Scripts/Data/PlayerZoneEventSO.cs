using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "PlayerZoneEvent", menuName = "Game Events/Player Zone Event")]
public class PlayerZoneEventSO : ZoneEventBase
{
    public UnityAction<int, int> OnSell;

    public void Sell(int coins, int chickens) => OnSell?.Invoke(coins, chickens);
}