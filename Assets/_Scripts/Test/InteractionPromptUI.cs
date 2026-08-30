using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private EventBusSO _eventBus;
    [SerializeField] private GameObject _promptRoot;

    private bool _isInVehicle;

    private void OnEnable()
    {
        _eventBus.InteractableInSight += HandleInteractableInSight;
        _eventBus.InteractableOutOfSight += HandlePromptHide;
        _eventBus.VehicleEntered += HandleVehicleEntered;
        _eventBus.VehicleExited += HandleVehicleExited;
    }

    private void OnDisable()
    {
        _eventBus.InteractableInSight -= HandleInteractableInSight;
        _eventBus.InteractableOutOfSight -= HandlePromptHide;
        _eventBus.VehicleEntered -= HandleVehicleEntered;
        _eventBus.VehicleExited -= HandleVehicleExited;
    }

    private void HandleInteractableInSight(IInteractable target)
    {
        if (!_isInVehicle)
            _promptRoot.SetActive(true);
    }

    private void HandlePromptHide() => _promptRoot.SetActive(false);

    private void HandleVehicleEntered(Vehicle vehicle)
    {
        _isInVehicle = true;
        _promptRoot.SetActive(false);
    }

    private void HandleVehicleExited() => _isInVehicle = false;
}