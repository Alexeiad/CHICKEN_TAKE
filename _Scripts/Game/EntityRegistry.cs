using System.Collections.Generic;
using System.Linq;

public class EntityRegistry<T> : IEntityRegistry<T>
{
    private readonly List<T> _entities = new List<T>();

    public IEnumerable<T> AllEntities => _entities;

    // Current возвращает первый элемент из коллекции (или null)
    public T Current => _entities.FirstOrDefault();

    // HasTarget проверяет, есть ли хоть одна сущность
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
}