using System.Collections;
using UnityEngine;

public class InteractableDetector : MonoBehaviour
{
    [SerializeField] private EventBusSO _eventBus;
    [SerializeField] private Camera _camera;
    [SerializeField] private float _maxDistance = 5f;
    [SerializeField] private float _checkInterval = 0.3f;

    private IInteractable _currentTarget;
    private Coroutine _routine;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void OnEnable() => _routine = StartCoroutine(CheckLoop());

    private void OnDisable()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        if (_currentTarget != null)
        {
            _currentTarget = null;
            _eventBus.InteractableOutOfSight?.Invoke();
        }
    }

    private IEnumerator CheckLoop()
    {
        var wait = new WaitForSeconds(_checkInterval);
        while (true)
        {
            IInteractable found = FindInteractableInSight();

            if (found != null && !ReferenceEquals(found, _currentTarget))
            {
                _currentTarget = found;
                _eventBus.InteractableInSight?.Invoke(_currentTarget);
            }
            else if (found == null && _currentTarget != null)
            {
                _currentTarget = null;
                _eventBus.InteractableOutOfSight?.Invoke();
            }

            yield return wait;
        }
    }

    private IInteractable FindInteractableInSight()
    {
        Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, _maxDistance);

        foreach (var hit in hits)
        {
            if (hit.transform.TryGetComponent(out IInteractable interactable))
                return interactable;
        }
        return null;
    }
}