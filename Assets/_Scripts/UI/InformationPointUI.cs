using System.Linq;
using UnityEngine;
using Zenject;

public class InformationPointUI : MonoBehaviour
{
    [SerializeField] private GameObject _canvas;
    [SerializeField] private float _interactionDistance = 2f;
    [SerializeField] private float _openDelay = 0.5f;

    [Inject] private IEntityRegistry<IEntity> _registry;

    private Player _player;
    private Camera _camera;

    private bool _isOpen;
    private float _moveToCenterTimer;

    private Vector3 _previousPlayerPosition;

    private Vector3 _cameraPosition;
    private Quaternion _cameraRotation;

    private CursorLockMode _previousCursorLockState;
    private bool _previousCursorVisible;

    private void Awake()
    {
        _canvas.SetActive(false);
    }

    private void Update()
    {
        if (_player == null)
        {
            _player = _registry.AllEntities
                .OfType<Player>()
                .FirstOrDefault();

            if (_player == null)
                return;

            _previousPlayerPosition = _player.transform.position;
            return;
        }

        Vector3 currentPosition = _player.transform.position;
        Vector3 movement = currentPosition - _previousPlayerPosition;

        _previousPlayerPosition = currentPosition;

        float distance = Vector3.Distance(
            transform.position,
            currentPosition
        );

        if (distance > _interactionDistance)
        {
            _moveToCenterTimer = 0f;

            if (_isOpen)
                Close();

            return;
        }

        if (_isOpen)
            return;

        movement.y = 0f;

        if (movement.sqrMagnitude <= 0.000001f)
        {
            _moveToCenterTimer = 0f;
            return;
        }

        Vector3 toCenter = transform.position - currentPosition;
        toCenter.y = 0f;

        if (toCenter.sqrMagnitude <= 0.000001f)
        {
            _moveToCenterTimer = 0f;
            return;
        }

        float direction = Vector3.Dot(
            movement.normalized,
            toCenter.normalized
        );

        if (direction <= 0f)
        {
            _moveToCenterTimer = 0f;
            return;
        }

        _moveToCenterTimer += Time.deltaTime;

        if (_moveToCenterTimer >= _openDelay)
        {
            _moveToCenterTimer = 0f;
            Open();
        }
    }

    private void Open()
    {
        _camera = Camera.main;

        if (_camera == null)
            return;

        _cameraPosition = _camera.transform.position;
        _cameraRotation = _camera.transform.rotation;

        _previousCursorLockState = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _canvas.SetActive(true);

        _isOpen = true;
    }

    private void LateUpdate()
    {
        if (!_isOpen || _camera == null)
            return;

        _camera.transform.SetPositionAndRotation(
            _cameraPosition,
            _cameraRotation
        );
    }

    public void Close()
    {
        if (!_isOpen)
            return;

        _canvas.SetActive(false);

        Cursor.lockState = _previousCursorLockState;
        Cursor.visible = _previousCursorVisible;

        _isOpen = false;
        _moveToCenterTimer = 0f;
    }
}