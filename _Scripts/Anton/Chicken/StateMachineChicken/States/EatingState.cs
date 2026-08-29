using UnityEngine;

public class EatingState : ChickenState
{
    private float _timer;

    public EatingState(ChickenController chicken, ChickenStateMachine stateMachine)
        : base(chicken, stateMachine) { }

    public override void Enter()
    {
        _timer = Random.Range(2f, 5f);
        Chicken.ChickenAnimator.SetAnimation(ChickenAnimator.AnimationType.Eating);
    }

    public override void Update()
    {
        // Проверяем через модуль движения
        if (Chicken.ChickenMovement.IsTargetClose(Chicken.Settings.FleeDistance))
        {
            StateMachine.ChangeState(Chicken.RunningState);
            return;
        }

        _timer -= Time.deltaTime;

        if (_timer <= 0)
            StateMachine.ChangeState(Chicken.StandingState);
    }
}