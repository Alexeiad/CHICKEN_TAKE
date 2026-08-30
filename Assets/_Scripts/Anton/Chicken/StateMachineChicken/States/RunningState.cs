using UnityEngine;

public class RunningState : ChickenState
{
    public RunningState(ChickenController chicken, ChickenStateMachine stateMachine)
        : base(chicken, stateMachine) { }

    public override void Enter()
    {
        Chicken.ChickenAnimator.SetAnimation(ChickenAnimator.AnimationType.Running);
        Chicken.ChickenMovement.SetMovementMode(MovementMode.Flee);
    }

    public override void Update()
    {
        if (Chicken.ChickenMovement.IsTargetFarEnough(Chicken.Settings.CalmDistance))
        {
            StateMachine.ChangeState(Chicken.StandingState);
            return;
        }

        Vector3 fleeDirection = Chicken.ChickenMovement.GetFleeDirection();
        Chicken.ChickenMovement.Move(fleeDirection);
    }
}