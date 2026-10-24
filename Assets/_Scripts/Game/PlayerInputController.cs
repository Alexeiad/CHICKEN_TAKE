using System;
using System.Linq;
using UnityEngine;
using Zenject;

public class PlayerInputController : ITickable
{
    public static Action<PlayerFootstepAudio> OnAudioReady;

    private readonly IEntityRegistry<IEntity> _playerRegistry;
    private readonly CameraController _cameraController;
    private readonly PlayerSettings _settings;

    [Inject] private Animator _playerAnimator;

    private PlayerFootstepAudio _footstepAudio;

    private const float _rotationSpeed = 10f;
    private float _runMultiplier = 2f;

    private bool _isJumping;
    private bool _leftGround;

    private bool _jumpPending;
    private float _jumpTimer;

    private bool _landingPending;
    private bool _landingDone;
    private float _landingTimer;

    // Последнее состояние ввода, нужно чтобы правильно выйти из прыжка
    private bool _lastIsMoving;
    private bool _lastIsRunning;

    public PlayerInputController(
        IEntityRegistry<IEntity> playerRegistry,
        CameraController cameraController,
        PlayerSettings settings)
    {
        _playerRegistry = playerRegistry;
        _cameraController = cameraController;
        _settings = settings;

        OnAudioReady += audio => _footstepAudio = audio;
    }

    public void Tick()
    {
        Player player = _playerRegistry.AllEntities
            .OfType<Player>()
            .FirstOrDefault();

        if (InformationPointUI.BlocksGameplayInput)
        {
            _footstepAudio?.StopFootsteps();

            // Если окно открыли во время приседания, отменяем прыжок
            if (_jumpPending)
                CancelPendingJump();

            if (_playerAnimator != null && !_isJumping)
                SetLocomotion(false, false);

            // чтобы гравитация продолжала работать, пока открыто окно
            player?.Movement.Move(Vector3.zero);
            return;
        }

        if (player == null)
            return;

        UpdateJumpDelay(player);
        UpdateJumpState(player);

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection =
            _cameraController.Forward * vertical +
            _cameraController.Right * horizontal;

        // Пока персонаж приседает перед прыжком, стоим на месте
        if (_jumpPending && _settings.LockMoveWhileCrouch)
            moveDirection = Vector3.zero;

        // Пока персонаж амортизирует после приземления, стоим на месте
        if (_landingPending && _settings.LockMoveWhileLanding)
            moveDirection = Vector3.zero;

        bool isMoving = moveDirection.sqrMagnitude > 0.001f;
        bool isRunning = isMoving && Input.GetKey(KeyCode.LeftShift);

        _lastIsMoving = isMoving;
        _lastIsRunning = isRunning;

        // Пока прыжок не закончился, idle, ходьба и бег в аниматор не пишутся
        if (!_isJumping)
            SetLocomotion(isMoving, isRunning);

        if (isMoving && !_isJumping)
            _footstepAudio?.StartFootsteps();
        else
            _footstepAudio?.StopFootsteps();

        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        if (isMoving)
        {
            Vector3 lookDirection = moveDirection;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(lookDirection);

                player.transform.rotation = Quaternion.Slerp(
                    player.transform.rotation,
                    targetRotation,
                    Time.deltaTime * _rotationSpeed
                );
            }
        }

        if (isRunning)
            moveDirection *= _runMultiplier;

        if (Input.GetKeyDown(KeyCode.Space) && !_isJumping && player.Movement.IsGrounded)
            StartJump();

        player.Movement.Move(moveDirection);
    }

    // Единое место, где пишутся idle / walk / run
    private void SetLocomotion(bool isMoving, bool isRunning)
    {
        if (_playerAnimator == null)
            return;

        _playerAnimator.SetBool(_settings.AnimKeyIdle, !isMoving);
        _playerAnimator.SetBool(_settings.AnimKeyWalk, isMoving);
        _playerAnimator.SetBool(_settings.AnimKeyRun, isRunning);
    }

    private void StartJump()
    {
        _isJumping = true;
        _leftGround = false;

        _landingPending = false;
        _landingDone = false;
        _landingTimer = 0f;

        _jumpPending = true;
        _jumpTimer = _settings.JumpDelay;

        // Анимация приседания стартует сразу, физический прыжок позже
        _playerAnimator.SetBool(_settings.AnimKeyJump, true);
        _playerAnimator.SetBool(_settings.AnimKeyIdle, false);
        _playerAnimator.SetBool(_settings.AnimKeyWalk, false);
        _playerAnimator.SetBool(_settings.AnimKeyRun, false);
    }

    private void CancelPendingJump()
    {
        _jumpPending = false;
        _jumpTimer = 0f;
        _isJumping = false;
        _leftGround = false;

        _landingPending = false;
        _landingDone = false;
        _landingTimer = 0f;

        if (_playerAnimator != null)
        {
            _playerAnimator.SetBool(_settings.AnimKeyJump, false);
            SetLocomotion(false, false);
        }
    }

    private void UpdateJumpDelay(Player player)
    {
        if (!_jumpPending)
            return;

        _jumpTimer -= Time.deltaTime;

        if (_jumpTimer > 0f)
            return;

        _jumpPending = false;
        player.Movement.Jump();
    }

    private void UpdateJumpState(Player player)
    {
        if (!_isJumping || _jumpPending)
            return;

        bool grounded = player.Movement.IsGrounded;

        // Запоминаем, что персонаж реально оторвался от земли
        if (!grounded)
            _leftGround = true;

        // Коснулись земли: запускаем таймер амортизации
        if (!_landingPending && !_landingDone && _leftGround && grounded)
        {
            _landingPending = true;
            _landingTimer = _settings.LandingDelay;
        }

        // Ждём окончания задержки приземления, потом выключаем isJump
        if (_landingPending)
        {
            _landingTimer -= Time.deltaTime;

            if (_landingTimer <= 0f)
            {
                _landingPending = false;
                _landingDone = true;

                _playerAnimator.SetBool(_settings.AnimKeyJump, false);

                // Сразу выставляем idle / walk / run, иначе выход из Jump не сработает
                SetLocomotion(_lastIsMoving, _lastIsRunning);
            }
        }

        // Ждём, пока анимация прыжка доиграет и выйдет из состояния
        if (_landingDone && !IsJumpAnimationPlaying())
        {
            _isJumping = false;
            _leftGround = false;
            _landingDone = false;
        }
    }

    private bool IsJumpAnimationPlaying()
    {
        if (string.IsNullOrEmpty(_settings.JumpStateName))
            return false;

        if (_playerAnimator.IsInTransition(0))
            return _playerAnimator.GetNextAnimatorStateInfo(0).IsName(_settings.JumpStateName);

        AnimatorStateInfo state = _playerAnimator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(_settings.JumpStateName) && state.normalizedTime < 1f;
    }
}