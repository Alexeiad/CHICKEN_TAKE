using System.Collections.Generic;

public interface IEntityRegistry<T>
{
    T Current { get; }
    bool HasTarget { get; }
    IEnumerable<T> AllEntities { get; }

    void Register(T entity);
    void Unregister(T entity);
}