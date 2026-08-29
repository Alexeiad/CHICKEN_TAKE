using UnityEngine;

public class PlayerInteractionInput : MonoBehaviour
{
    [SerializeField] private EventBusSO _eventBus;
    [SerializeField] private KeyCode _interactKey = KeyCode.E;

    private IInteractable _currentTarget;

    private void OnEnable()
    {
        _eventBus.InteractableInSight += HandleInteractableInSight;
        _eventBus.InteractableOutOfSight += HandleInteractableOutOfSight;
    }

    private void OnDisable()
    {
        _eventBus.InteractableInSight -= HandleInteractableInSight;
        _eventBus.InteractableOutOfSight -= HandleInteractableOutOfSight;
    }

    private void Update()
    {
        if (_currentTarget != null && Input.GetKeyDown(_interactKey))
            _eventBus.InteractRequested?.Invoke(transform, _currentTarget);
    }

    private void HandleInteractableInSight(IInteractable target) => _currentTarget = target;
    private void HandleInteractableOutOfSight() => _currentTarget = null;
}