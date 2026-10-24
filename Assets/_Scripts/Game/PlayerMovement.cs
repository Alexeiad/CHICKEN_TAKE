using UnityEngine;
using Zenject;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour, IPlayerMovement
{
    private CharacterController _characterController;
    private PlayerSettings _settings;

    private float _verticalVelocity;
    private bool _jumpRequested;

    public bool IsGrounded => _characterController.isGrounded;

    [Inject]
    public void Construct(PlayerSettings settings)
    {
        _settings = settings;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    public void Jump()
    {
        if (_characterController.isGrounded)
            _jumpRequested = true;
    }

    public void Move(Vector3 worldDirection)
    {
        if (_settings == null) return;

        Vector3 velocity = worldDirection * _settings.Speed;

        if (_characterController.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -1f;

        if (_jumpRequested)
        {
            _verticalVelocity = Mathf.Sqrt(_settings.JumpHeight * -2f * _settings.Gravity);
            _jumpRequested = false;
        }

        _verticalVelocity += _settings.Gravity * Time.deltaTime;

        velocity.y = _verticalVelocity;
        _characterController.Move(velocity * Time.deltaTime);
    }
}