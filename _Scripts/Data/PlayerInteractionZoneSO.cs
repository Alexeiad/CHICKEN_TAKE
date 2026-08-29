using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerInteractionZoneSO",
    menuName = "Game/Player Interaction Zone")]
public class PlayerInteractionZoneSO : ScriptableObject
{
    public event Action<bool> OnZoneStateChanged;

    public void SetZoneState(bool isInZone)
    {
        OnZoneStateChanged?.Invoke(isInZone);
    }
}