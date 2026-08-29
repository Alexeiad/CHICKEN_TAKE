public class ChickenStateMachine
{
    public ChickenState CurrentState { get; private set; }

    public void Initialize(ChickenState startState)
    {
        if (startState == null)
            throw new System.ArgumentNullException(nameof(startState));

        CurrentState = startState;
        CurrentState.Enter();
    }

    public void ChangeState(ChickenState newState)
    {
        if (newState == null)
            throw new System.ArgumentNullException(nameof(newState));

        if (ReferenceEquals(CurrentState, newState))
            return;

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}
