using UnityEngine;

public class VehicleExitInput : MonoBehaviour
{
    [SerializeField] private EventBusSO _eventBus;
    [SerializeField] private KeyCode _exitKey = KeyCode.Space;

    private void Update()
    {
        if (Input.GetKeyDown(_exitKey))
            _eventBus.ExitVehicleRequested?.Invoke();
    }
}