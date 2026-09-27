using UnityEngine;
using Zenject;

public class Player : MonoBehaviour, IEntity
{
    public enum VehicleState
    {
        NotInVehicle,
        InVehicle
    }

    public IPlayerMovement Movement => _movement;
    public Transform Transform => transform;
    public VehicleState State => _state;

    private IEntityRegistry<IEntity> _registry;
    private IPlayerMovement _movement;
    private VehicleState _state = VehicleState.NotInVehicle;

    [Inject]
    private void Construct(IEntityRegistry<IEntity> registry)
    {
        _registry = registry;
    }

    private void Awake()
    {
        _movement = GetComponent<IPlayerMovement>();
    }

    private void Start()
    {
        _registry?.Register(this);
    }

    private void OnDestroy()
    {
        _registry?.Unregister(this);
    }

    public void SetVehicleState(VehicleState state)
    {
        _state = state;
    }
}