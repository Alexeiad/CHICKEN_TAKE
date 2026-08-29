using UnityEngine;
using Zenject;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour, IPlayerMovement
{
    private CharacterController _characterController;
    private PlayerSettings _settings;

    private float _verticalVelocity;

    [Inject]
    public void Construct(PlayerSettings settings)
    {
        _settings = settings;
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    public void Move(Vector3 direction)
    {
        if (_settings == null) return;

        Vector3 velocity = direction * _settings.Speed;

        if (_characterController.isGrounded)
        {
            _verticalVelocity = -1f;
        }
        else
        {
            _verticalVelocity += _settings.Gravity * Time.deltaTime;
        }

        velocity.y = _verticalVelocity;
        _characterController.Move(velocity * Time.deltaTime);
    }
}