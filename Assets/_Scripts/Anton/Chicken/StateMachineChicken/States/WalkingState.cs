using UnityEngine;

public class WalkingState : ChickenState
{
    private float _timer;
    private Vector3 _moveDirection;

    public WalkingState(ChickenController chicken, ChickenStateMachine stateMachine)
        : base(chicken, stateMachine) { }

    public override void Enter()
    {
        _timer = Random.Range(3f, 6f);

        float randomAngle = Random.Range(0f, 360f);
        _moveDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;

        Chicken.ChickenAnimator.SetAnimation(ChickenAnimator.AnimationType.Walking);
        Chicken.ChickenMovement.SetMovementMode(MovementMode.Walk);
    }

    public override void Update()
    {
        if (Chicken.ChickenMovement.IsTargetClose(Chicken.Settings.FleeDistance))
        {
            StateMachine.ChangeState(Chicken.RunningState);
            return;
        }

        _timer -= Time.deltaTime;

        if (_timer <= 0)
        {
            StateMachine.ChangeState(Chicken.StandingState);
            return;
        }

        Chicken.ChickenMovement.Move(_moveDirection);
    }
}