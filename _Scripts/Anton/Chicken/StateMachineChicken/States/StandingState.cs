using UnityEngine;

public class StandingState : ChickenState
{
    private float _timer;

    public StandingState(ChickenController chicken, ChickenStateMachine stateMachine)
        : base(chicken, stateMachine) { }

    public override void Enter()
    {
        _timer = Random.Range(1.5f, 3f);
        Chicken.ChickenAnimator.SetAnimation(ChickenAnimator.AnimationType.Standing);
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
            if (Random.value > 0.5f)
                StateMachine.ChangeState(Chicken.WalkingState);
            else
                StateMachine.ChangeState(Chicken.EatingState);
        }
    }
}