using System;
using UnityEngine;

[CreateAssetMenu(menuName = "EventBus/EventBusSO")]
public class EventBusSO : ScriptableObject
{
    // ќбнаружение цели дл€ взаимодействи€ (замена VehicleClose/VehicleFar)
    public Action<IInteractable> InteractableInSight;
    public Action InteractableOutOfSight;

    // »грок нажал E р€дом с целью
    public Action<Transform, IInteractable> InteractRequested;

    // TODO: будет invoke-итьс€ из VehicleController, когда игрок нажмЄт E внутри машины
    public Action ExitVehicleRequested;

    // —осто€ние "сидит/не сидит" Ч слушает UI и всЄ, чему нужно это знать
    public Action<Vehicle> VehicleEntered;
    public Action VehicleExited;
}