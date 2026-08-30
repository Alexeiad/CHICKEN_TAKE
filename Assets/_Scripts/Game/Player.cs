using UnityEngine;
using Zenject;

public class Player : MonoBehaviour, IEntity
{
    
    public IPlayerMovement Movement => _movement;
    public Transform Transform => transform;

    private IEntityRegistry<IEntity> _registry; // Изменили с Player на IEntity
    private IPlayerMovement _movement;

    [Inject]
    private void Construct(IEntityRegistry<IEntity> registry) // Изменили тип параметра
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
}