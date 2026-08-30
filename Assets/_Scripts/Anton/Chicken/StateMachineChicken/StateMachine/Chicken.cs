using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class Chicken : MonoBehaviour, IEntity
{
    private static readonly HashSet<Chicken> _activeChickens = new();

    public static IReadOnlyCollection<Chicken> ActiveChickens => _activeChickens;

    public Transform Transform => transform;

    private void OnEnable()
    {
        _activeChickens.Add(this);
    }

    private void OnDisable()
    {
        _activeChickens.Remove(this);
    }

    private void OnDestroy()
    {
        _activeChickens.Remove(this);
    }

    public class Factory : PlaceholderFactory<Chicken>
    {
    }
}
