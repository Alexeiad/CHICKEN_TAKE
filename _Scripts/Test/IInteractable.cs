using UnityEngine;

public interface IInteractable
{
    // Точка, куда переместится игрок при взаимодействии (сидушка, а не центр машины)
    Transform InteractionPoint { get; }
}