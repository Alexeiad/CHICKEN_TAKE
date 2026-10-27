using System;
using System.Collections.Generic;
using System.Linq;
using Zenject;

public class EntityRegistry<T> : IEntityRegistry<T>, IDisposable
{
    private readonly List<T> _entities = new List<T>();

    public IEnumerable<T> AllEntities => _entities;

    public T Current => _entities.FirstOrDefault();

    public bool HasTarget => _entities.Count > 0;

    public void Register(T entity)
    {
        if (entity != null && !_entities.Contains(entity))
            _entities.Add(entity);
    }

    public void Unregister(T entity)
    {
        if (entity != null)
            _entities.Remove(entity);
    }

    public void Dispose()
    {
        _entities.Clear(); // критично при выгрузке сцены
    }
}