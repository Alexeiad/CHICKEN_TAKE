using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(ChickenAnimator))]
[RequireComponent(typeof(ChickenMovement))]
public class ChickenController : MonoBehaviour
{
    [Header("Global Config")]
    [SerializeField] private GameSettingsSO _gameSettings;

    [Header("Debug")]
    [SerializeField] private bool _logDiagnostics;

    public ChickenSettings Settings { get; private set; }
    public ChickenAnimator ChickenAnimator { get; private set; }
    public ChickenMovement ChickenMovement { get; private set; }
    public ChickenStateMachine StateMachine { get; private set; }

    public EatingState EatingState { get; private set; }
    public StandingState StandingState { get; private set; }
    public WalkingState WalkingState { get; private set; }
    public RunningState RunningState { get; private set; }

    /// Индивидуальный множитель этой курицы. Роллится один раз при инициализации.
    public float IndividualSpeedMultiplier { get; private set; } = 1f;

    /// Общий множитель для всех кур и всех состояний.
    public float GlobalSpeedMultiplier => Settings != null ? Settings.GlobalSpeedMultiplier : 1f;

    private void Awake()
    {
        ChickenAnimator = GetComponent<ChickenAnimator>();
        ChickenMovement = GetComponent<ChickenMovement>();
        LoadAndApplySettings();
    }

    public void Reinitialize()
    {
        LoadAndApplySettings();
    }

    private void LoadAndApplySettings()
    {
        Settings = _gameSettings != null ? _gameSettings.Chicken : new ChickenSettings();

        var range = Settings.IndividualSpeedMultiplierRange;
        IndividualSpeedMultiplier = Random.Range(range.x, range.y);

        ChickenMovement.Initialize(Settings);
        InitStateMachine();
    }

    // ---------- Итоговая скорость ----------
    // Возвращает скорость по профилю на момент t с учётом общего и индивидуального множителей.
    public float GetProfileSpeed(SpeedProfileSettings profile, float t)
    {
        float a = Mathf.Lerp(profile.ARange.x, profile.ARange.y, 0.5f);
        float b = Mathf.Lerp(profile.BRange.x, profile.BRange.y, 0.5f);
        float raw = a * t + b;
        float capped = Mathf.Min(raw, profile.MaxSpeed);
        return capped * GlobalSpeedMultiplier * IndividualSpeedMultiplier;
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

    private void Update()
    {
        StateMachine?.Update();

        if (_logDiagnostics && Settings != null)
        {
            var cc = ChickenMovement != null
                ? ChickenMovement.GetComponent<CharacterController>()
                : null;

            Debug.Log(
                $"[{name}] FSM={StateMachine?.CurrentState?.GetType().Name} " +
                $"Global={GlobalSpeedMultiplier:F2} Indiv={IndividualSpeedMultiplier:F2} " +
                $"CC.velocity={cc?.velocity.magnitude:F2}",
                this);
        }
    }

    [ContextMenu("Reinitialize Chicken")]
    private void DebugReinitialize() => Reinitialize();
}