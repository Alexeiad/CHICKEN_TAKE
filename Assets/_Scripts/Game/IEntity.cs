using UnityEngine;

public interface IEntity
{
    Transform Transform { get; }
    string Name { get; set; }
}