using UnityEngine;

public class PlayerVehicleStateController : MonoBehaviour
{
    [SerializeField] private EventBusSO _eventBus;
    [SerializeField] private Transform _player;
    [SerializeField] private PlayerInteractionInput _interactionInput;

    // TODO: скрипт пешего передвижени€ ещЄ не реализован
    // [SerializeField] private PlayerMovement _playerMovement;

    // TODO: скрипт управлени€ машиной ещЄ не реализован
    // [SerializeField] private VehicleController _vehicleController;

    private Vehicle _currentVehicle;

    private void OnEnable()
    {
        _eventBus.InteractRequested += HandleInteractRequested;
        _eventBus.ExitVehicleRequested += HandleExitRequested;
    }

    private void OnDisable()
    {
        _eventBus.InteractRequested -= HandleInteractRequested;
        _eventBus.ExitVehicleRequested -= HandleExitRequested;
    }

    private void HandleInteractRequested(Transform interactor, IInteractable target)
    {
        if (_currentVehicle != null)
            return; // уже за рулЄм Ч игнорируем

        if (target is Vehicle vehicle)
            EnterVehicle(vehicle);
    }

    private void EnterVehicle(Vehicle vehicle)
    {
        _currentVehicle = vehicle;

        _player.position = vehicle.InteractionPoint.position;
        _player.rotation = vehicle.InteractionPoint.rotation;

        _interactionInput.enabled = false;
        // _playerMovement.enabled = false;
        // _vehicleController.enabled = true;
        // _vehicleController.Init(vehicle);

        _eventBus.VehicleEntered?.Invoke(vehicle);
    }

    private void HandleExitRequested()
    {
        if (_currentVehicle == null)
            return;

        _player.position = _currentVehicle.ExitPoint.position;
        _player.rotation = _currentVehicle.ExitPoint.rotation;

        // _vehicleController.enabled = false;
        _interactionInput.enabled = true;
        // _playerMovement.enabled = true;

        _currentVehicle = null;
        _eventBus.VehicleExited?.Invoke();
    }
}