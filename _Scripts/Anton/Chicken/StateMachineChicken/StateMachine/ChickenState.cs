public abstract class ChickenState
{
    protected ChickenController Chicken;
    protected ChickenStateMachine StateMachine;

    protected ChickenState(ChickenController chicken, ChickenStateMachine stateMachine)
    {
        Chicken = chicken;
        StateMachine = stateMachine;
    }

    public virtual void Enter() { }

    public virtual void Exit() { }

    public virtual void Update() { }
}
