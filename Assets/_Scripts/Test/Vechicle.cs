using UnityEngine;

public class Vehicle : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform _seatPoint;
    [SerializeField] private Transform _exitPoint;

    public Transform InteractionPoint => _seatPoint;
    public Transform ExitPoint => _exitPoint;

    // TODO: скрипт управления машиной (газ/руль) появится позже
    // public VehicleController Controller;
}