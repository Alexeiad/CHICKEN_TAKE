using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class Vechicle : MonoBehaviour, IEntity
{
    public Transform Transform => transform;

    private IEntityRegistry<IEntity> _registry;

    [Inject]
    private void Construct(IEntityRegistry<IEntity> registry)
    {
        _registry = registry;
    }

    private void Start()
    {
        _registry?.Register(this); // Регистрируем машину в реестре при старте
    }

    private void OnDestroy()
    {
        _registry?.Unregister(this); // Удаляем из реестра при уничтожении
    }
}
