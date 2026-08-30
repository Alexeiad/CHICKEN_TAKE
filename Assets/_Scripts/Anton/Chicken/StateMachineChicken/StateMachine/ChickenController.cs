using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(ChickenAnimator))]
[RequireComponent(typeof(ChickenMovement))]
public class ChickenController : MonoBehaviour
{
    [Header("Global Config")]
    [SerializeField] private GameSettingsSO _gameSettings;

    public ChickenSettings Settings { get; private set; }
    public ChickenAnimator ChickenAnimator { get; private set; }
    public ChickenMovement ChickenMovement { get; private set; }
    public ChickenStateMachine StateMachine { get; private set; }

    public EatingState EatingState { get; private set; }
    public StandingState StandingState { get; private set; }
    public WalkingState WalkingState { get; private set; }
    public RunningState RunningState { get; private set; }

    private void Awake()
    {
        ChickenAnimator = GetComponent<ChickenAnimator>();
        ChickenMovement = GetComponent<ChickenMovement>();
        Settings = _gameSettings != null ? _gameSettings.Chicken : new ChickenSettings();

        ChickenMovement.Initialize(Settings);
        InitStateMachine();
    }

    public void SetTarget(IEntity entity)
    {
        if (entity != null)
            ChickenMovement.SetTarget(entity.Transform);
    }

    private void InitStateMachine()
    {
        StateMachine = new ChickenStateMachine();
        EatingState = new EatingState(this, StateMachine);
        StandingState = new StandingState(this, StateMachine);
        WalkingState = new WalkingState(this, StateMachine);
        RunningState = new RunningState(this, StateMachine);

        StateMachine.Initialize(EatingState);
    }

    private void Update() => StateMachine?.Update();
}