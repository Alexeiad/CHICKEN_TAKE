using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class PrometeoCarController : MonoBehaviour
{
    // Внутренние технические константы, чтобы не засорять инспектор
    private const float ShiftHysteresisKmh = 3f;
    private const float FirstGearResetSpeedKmh = 0.5f;
    private const float StopDirectionThreshold = 0.35f;
    private const float DriftLateralSpeed = 2.5f;
    private const float SkidLateralSpeed = 5f;
    private const float SkidMinSpeedKmh = 12f;
    private const float IdleRpmFallRate = 2f;
    private const float IdleEnginePitchDivisor = 25f;

    [Header("SETTINGS")]
    [SerializeField] private VehiclesSettings _settings;
    public bool isAi;

    [Header("WHEELS")]
    public GameObject frontLeftMesh;
    public WheelCollider frontLeftCollider;
    public GameObject frontRightMesh;
    public WheelCollider frontRightCollider;
    public GameObject rearLeftMesh;
    public WheelCollider rearLeftCollider;
    public GameObject rearRightMesh;
    public WheelCollider rearRightCollider;

    [Header("EFFECTS")]
    public ParticleSystem RLWParticleSystem;
    public ParticleSystem RRWParticleSystem;
    public TrailRenderer RLWTireSkid;
    public TrailRenderer RRWTireSkid;

    [Header("UI")]
    public Text carSpeedText;
    public Text currentGearText;

    [Header("SOUNDS")]
    public AudioSource carEngineSound;
    public AudioSource tireScreechSound;

    [Header("TOUCH CONTROLS")]
    public GameObject throttleButton;
    public GameObject reverseButton;
    public GameObject turnRightButton;
    public GameObject turnLeftButton;
    public GameObject handbrakeButton;

    [Header("CAR DATA")]
    [HideInInspector] public float carSpeed;
    [HideInInspector] public bool isDrifting;
    [HideInInspector] public bool isTractionLocked;

    public int CurrentGear => carSpeed < -0.5f ? -1 : _currentGear;
    public float CurrentRPM => _currentRPM;
    public int MaxSpeed => _t != null ? Mathf.RoundToInt(_t.MaxForwardSpeed) : 0;
    public int MaxSteeringAngle => _s != null ? _s.maxSteeringAngle : 0;

    private SharedVehicleSettings _s;
    private VehicleTuning _t;
    private bool _useEffects;
    private bool _useSounds;
    private bool _useUI;
    private bool _useTouchControls;

    private Rigidbody _rigidbody;
    private WheelCollider[] _wheels;
    private GameObject[] _wheelMeshes;
    private WheelFrictionCurve[] _defaultFriction;
    private float[] _defaultExtremumSlip;

    private PrometeoTouchInput _throttleInput;
    private PrometeoTouchInput _reverseInput;
    private PrometeoTouchInput _leftInput;
    private PrometeoTouchInput _rightInput;
    private PrometeoTouchInput _handbrakeInput;

    private float _initialEnginePitch;
    private float _steeringAxis;
    private float _localVelocityX;
    private float _localVelocityZ;
    private float _driftingAxis;

    private int _requestedDirection;
    private bool _brakeRequested;
    private bool _handbrakeRequested;
    private bool _initialized;

    private int _currentGear = 1;
    private float _currentRPM;
    private float _shiftTimer;

    private void Start() => Initialize();

    private void OnEnable() => GameState.PauseChanged += OnPauseChanged;

    private void Update()
    {
        if (!_initialized || GameState.IsPaused) return;

        UpdateCarData();
        if (!isAi) HandlePlayerInput();
        UpdateDrifting();
        UpdateSounds();
        UpdateEffects();
        UpdateUI();
        AnimateWheels();
    }

    private void FixedUpdate()
    {
        if (!_initialized || GameState.IsPaused) return;

        UpdateCarData();
        ApplyDrivePhysics();
        ApplySteering();
        UpdateWheelFriction();
    }

    private void ResolveSettings()
    {
        VehiclesSettings settings = _settings;
        if (settings == null)
        {
            Debug.LogWarning("PrometeoCarController: VehiclesSettings не назначен, используются значения по умолчанию.", this);
            settings = ScriptableObject.CreateInstance<VehiclesSettings>();
        }

        _s = settings.shared;
        _t = isAi ? settings.bot.tuning : settings.player.tuning;

        _useEffects = _t.useEffects;
        _useSounds = _t.useSounds;
        _useUI = !isAi && settings.player.useUI;
        _useTouchControls = !isAi && settings.player.useTouchControls;
    }

    private void Initialize()
    {
        ResolveSettings();

        _rigidbody = GetComponent<Rigidbody>();
        if (!HasRequiredWheels())
        {
            Debug.LogError("PrometeoCarController: Missing WheelColliders.", this);
            enabled = false;
            return;
        }

        _rigidbody.centerOfMass = _s.bodyMassCenter;

        _wheels = new[] { frontLeftCollider, frontRightCollider, rearLeftCollider, rearRightCollider };
        _wheelMeshes = new[] { frontLeftMesh, frontRightMesh, rearLeftMesh, rearRightMesh };

        CacheWheelFriction();
        SetupTouchControls();

        if (carEngineSound != null) _initialEnginePitch = carEngineSound.pitch;
        if (!_useSounds) StopSounds();
        if (!_useEffects) StopEffects();

        _requestedDirection = 0;
        _brakeRequested = false;
        _handbrakeRequested = false;

        _currentGear = 1;
        _currentRPM = _s.idleRPM;
        _shiftTimer = 0f;

        SetMotorTorque(0f);
        SetBrakeTorque(0f);
        _initialized = true;
    }

    private bool HasRequiredWheels() =>
        frontLeftCollider != null && frontRightCollider != null &&
        rearLeftCollider != null && rearRightCollider != null;

    private void CacheWheelFriction()
    {
        _defaultFriction = new WheelFrictionCurve[4];
        _defaultExtremumSlip = new float[4];
        for (int i = 0; i < _wheels.Length; i++)
        {
            _defaultFriction[i] = _wheels[i].sidewaysFriction;
            _defaultExtremumSlip[i] = _defaultFriction[i].extremumSlip;
        }
    }

    private void SetupTouchControls()
    {
        if (!_useTouchControls) return;
        _throttleInput = GetTouchInput(throttleButton);
        _reverseInput = GetTouchInput(reverseButton);
        _leftInput = GetTouchInput(turnLeftButton);
        _rightInput = GetTouchInput(turnRightButton);
        _handbrakeInput = GetTouchInput(handbrakeButton);

        if (_throttleInput == null || _reverseInput == null ||
            _leftInput == null || _rightInput == null || _handbrakeInput == null)
        {
            Debug.LogWarning("PrometeoCarController: Touch controls are not configured.", this);
            _useTouchControls = false;
        }
    }

    private PrometeoTouchInput GetTouchInput(GameObject button) =>
        button != null ? button.GetComponent<PrometeoTouchInput>() : null;

    private void UpdateCarData()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(_rigidbody.velocity);
        _localVelocityX = localVelocity.x;
        _localVelocityZ = localVelocity.z;
        carSpeed = _localVelocityZ * 3.6f;
    }

    private void HandlePlayerInput()
    {
        bool forward, reverse, left, right, handbrake;

        if (_useTouchControls)
        {
            forward = _throttleInput.buttonPressed;
            reverse = _reverseInput.buttonPressed;
            left = _leftInput.buttonPressed;
            right = _rightInput.buttonPressed;
            handbrake = _handbrakeInput.buttonPressed;
        }
        else
        {
            forward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            reverse = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            handbrake = Input.GetKey(KeyCode.Space);
        }

        if (forward && !reverse) GoForward();
        else if (reverse && !forward) GoReverse();
        else ThrottleOff();

        if (left) TurnLeft();
        else if (right) TurnRight();
        else ResetSteeringAngle();

        if (handbrake) Handbrake();
        else RecoverTraction();
    }

    // PHYSICS

    private void ApplyDrivePhysics()
    {
        if (_handbrakeRequested) { ApplyHandbrakeTorque(); return; }
        if (_brakeRequested) { SetBrakeTorque(_s.brakeForce); return; }

        if (_requestedDirection == 0)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(_s.engineBrakeForce);
            _currentRPM = Mathf.MoveTowards(_currentRPM, _s.idleRPM, (_s.maxRPM - _s.idleRPM) * Time.fixedDeltaTime * IdleRpmFallRate);
            return;
        }

        if (_localVelocityZ * _requestedDirection < -StopDirectionThreshold)
        {
            SetBrakeTorque(_s.brakeForce);
            return;
        }

        float currentSpeed = Mathf.Abs(carSpeed);
        float speedLimit = _requestedDirection > 0 ? _t.MaxForwardSpeed : _s.maxReverseSpeed;

        if (currentSpeed >= speedLimit)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(0f);
            return;
        }

        SetBrakeTorque(0f);

        if (_requestedDirection > 0)
        {
            if (_s.useGearbox && HasValidGears()) UpdateGearbox(currentSpeed);
            else _currentGear = 1;

            _currentRPM = CalculateRPM(currentSpeed);

            float torque = CalculateForwardTorque();
            if (_shiftTimer > 0f) torque *= _s.torqueWhileShifting;

            SetMotorTorque(torque);
        }
        else
        {
            _currentGear = 1;
            _currentRPM = Mathf.Lerp(_s.idleRPM, _s.maxRPM, currentSpeed / Mathf.Max(1f, _s.maxReverseSpeed));
            float torque = AccelerationToWheelTorque(_t.reverseAcceleration * _t.globalPowerMultiplier);
            SetMotorTorque(-torque);
        }
    }

    private float CalculateForwardTorque()
    {
        if (!HasValidGears())
            return AccelerationToWheelTorque(1.0f * _t.globalPowerMultiplier);

        int idx = Mathf.Clamp(_currentGear - 1, 0, _t.gears.Length - 1);
        GearSettings gear = _t.gears[idx];
        float targetAccel = gear.acceleration * gear.powerMultiplier * _t.globalPowerMultiplier;
        return AccelerationToWheelTorque(targetAccel);
    }

    private float AccelerationToWheelTorque(float targetAccel)
    {
        if (_rigidbody == null || rearLeftCollider == null || targetAccel <= 0f) return 0f;

        float wheelRadius = Mathf.Max(0.01f, rearLeftCollider.radius);
        float totalForce = targetAccel * _rigidbody.mass;
        float wheels = Mathf.Max(1, _s.drivenWheels);

        return totalForce * wheelRadius / wheels;
    }

    private void SetMotorTorque(float torque)
    {
        if (_wheels == null) return;
        for (int i = 0; i < _wheels.Length; i++)
        {
            _wheels[i].brakeTorque = 0f;
            _wheels[i].motorTorque = torque;
        }
    }

    private void SetBrakeTorque(float torque)
    {
        if (_wheels == null) return;
        for (int i = 0; i < _wheels.Length; i++)
        {
            _wheels[i].motorTorque = 0f;
            _wheels[i].brakeTorque = torque;
        }
    }

    // GEARBOX

    private bool HasValidGears() => _t.gears != null && _t.gears.Length > 0;

    private void UpdateGearbox(float speed)
    {
        if (_shiftTimer > 0f)
        {
            _shiftTimer -= Time.fixedDeltaTime;
            return;
        }

        GearSettings[] gears = _t.gears;
        int maxGear = gears.Length;

        if (speed < FirstGearResetSpeedKmh)
        {
            if (_currentGear != 1)
            {
                _currentGear = 1;
                _shiftTimer = _s.shiftTime * 0.5f;
            }
            return;
        }

        if (_currentGear < maxGear && speed >= gears[_currentGear - 1].maxSpeed)
        {
            _currentGear++;
            _shiftTimer = _s.shiftTime;
            return;
        }

        if (_currentGear > 1 && speed < gears[_currentGear - 2].maxSpeed - ShiftHysteresisKmh)
        {
            _currentGear--;
            _shiftTimer = _s.shiftTime;
        }
    }

    private float CalculateRPM(float speed)
    {
        if (!HasValidGears() || _currentGear < 1) return _s.idleRPM;

        GearSettings[] gears = _t.gears;
        int idx = Mathf.Clamp(_currentGear - 1, 0, gears.Length - 1);
        float prevTop = idx > 0 ? gears[idx - 1].maxSpeed : 0f;
        float thisTop = gears[idx].maxSpeed;
        float span = Mathf.Max(0.01f, thisTop - prevTop);
        float t = Mathf.Clamp01((speed - prevTop) / span);
        return Mathf.Lerp(_s.idleRPM, _s.maxRPM, t);
    }

    // ENGINE COMMANDS

    public void GoForward()
    {
        _requestedDirection = 1;
        _brakeRequested = false;
        _handbrakeRequested = false;
    }

    public void GoReverse()
    {
        _requestedDirection = -1;
        _brakeRequested = false;
        _handbrakeRequested = false;
    }

    public void ThrottleOff()
    {
        _requestedDirection = 0;
        _brakeRequested = false;
        _handbrakeRequested = false;
    }

    public void Brakes()
    {
        _requestedDirection = 0;
        _brakeRequested = true;
    }

    public void DecelerateCar() => ThrottleOff();

    // STEERING

    public void TurnLeft()
    {
        if (GameState.IsPaused || _s == null) return;
        _steeringAxis = Mathf.Max(-1f, _steeringAxis - Time.deltaTime * _s.steerSpeedRate * _t.steeringSpeed);
    }

    public void TurnRight()
    {
        if (GameState.IsPaused || _s == null) return;
        _steeringAxis = Mathf.Min(1f, _steeringAxis + Time.deltaTime * _s.steerSpeedRate * _t.steeringSpeed);
    }

    public void ResetSteeringAngle()
    {
        if (GameState.IsPaused || _s == null) return;
        _steeringAxis = Mathf.MoveTowards(_steeringAxis, 0f, Time.deltaTime * _s.steerSpeedRate * _t.steeringSpeed);
    }

    public void SetSteering(float input)
    {
        if (GameState.IsPaused || _s == null) return;
        input = Mathf.Clamp(input, -1f, 1f);
        _steeringAxis = Mathf.MoveTowards(_steeringAxis, input, Time.deltaTime * _s.steerSpeedRate * _t.steeringSpeed);
    }

    private void ApplySteering()
    {
        if (!_initialized) return;
        float angle = _steeringAxis * _s.maxSteeringAngle;
        float t = Mathf.Clamp01(_t.steeringSpeed * Time.fixedDeltaTime * _s.wheelTurnRate);

        frontLeftCollider.steerAngle = Mathf.Lerp(frontLeftCollider.steerAngle, angle, t);
        frontRightCollider.steerAngle = Mathf.Lerp(frontRightCollider.steerAngle, angle, t);
    }

    // HANDBRAKE

    public void Handbrake()
    {
        if (GameState.IsPaused) return;
        _requestedDirection = 0;
        _brakeRequested = false;
        _handbrakeRequested = true;
        _driftingAxis = Mathf.Clamp01(_driftingAxis + Time.deltaTime);
        isTractionLocked = true;
    }

    public void RecoverTraction()
    {
        if (GameState.IsPaused) return;
        _handbrakeRequested = false;
        isTractionLocked = false;
        _driftingAxis = Mathf.MoveTowards(_driftingAxis, 0f, Time.deltaTime / Mathf.Max(0.01f, _s.driftRecoveryTime));
    }

    private void ApplyHandbrakeTorque()
    {
        float handbrakeTorque = _s.brakeForce * _s.handbrakeBrakeMultiplier;

        frontLeftCollider.motorTorque = 0f; frontLeftCollider.brakeTorque = 0f;
        frontRightCollider.motorTorque = 0f; frontRightCollider.brakeTorque = 0f;
        rearLeftCollider.motorTorque = 0f; rearLeftCollider.brakeTorque = handbrakeTorque;
        rearRightCollider.motorTorque = 0f; rearRightCollider.brakeTorque = handbrakeTorque;
    }

    private void UpdateWheelFriction()
    {
        if (_wheels == null || _defaultExtremumSlip == null) return;

        float slipFactor = 1f + (_s.handbrakeDriftMultiplier - 1f) * _driftingAxis;
        for (int i = 0; i < _wheels.Length; i++)
        {
            WheelFrictionCurve friction = _wheels[i].sidewaysFriction;
            friction.extremumSlip = _defaultExtremumSlip[i] * slipFactor;
            _wheels[i].sidewaysFriction = friction;
        }
    }

    private void UpdateDrifting()
    {
        isDrifting = isTractionLocked || Mathf.Abs(_localVelocityX) > DriftLateralSpeed;
    }

    // WHEELS, EFFECTS, UI, SOUNDS

    private void AnimateWheels()
    {
        for (int i = 0; i < _wheels.Length; i++)
        {
            if (_wheelMeshes[i] == null) continue;
            _wheels[i].GetWorldPose(out Vector3 position, out Quaternion rotation);
            _wheelMeshes[i].transform.SetPositionAndRotation(position, rotation);
        }
    }

    private void UpdateEffects()
    {
        if (!_useEffects) return;
        bool drifting = isDrifting;
        bool skidding = (isTractionLocked || Mathf.Abs(_localVelocityX) > SkidLateralSpeed) && Mathf.Abs(carSpeed) > SkidMinSpeedKmh;

        SetParticle(RLWParticleSystem, drifting);
        SetParticle(RRWParticleSystem, drifting);
        if (RLWTireSkid != null) RLWTireSkid.emitting = skidding;
        if (RRWTireSkid != null) RRWTireSkid.emitting = skidding;
    }

    private void SetParticle(ParticleSystem particle, bool play)
    {
        if (particle == null) return;
        if (play && !particle.isPlaying) particle.Play();
        else if (!play && particle.isPlaying) particle.Stop();
    }

    private void StopEffects()
    {
        SetParticle(RLWParticleSystem, false);
        SetParticle(RRWParticleSystem, false);
        if (RLWTireSkid != null) RLWTireSkid.emitting = false;
        if (RRWTireSkid != null) RRWTireSkid.emitting = false;
    }

    private void UpdateUI()
    {
        if (!_useUI)
        {
            if (carSpeedText != null) carSpeedText.text = "0";
            return;
        }

        if (carSpeedText != null)
            carSpeedText.text = Mathf.RoundToInt(Mathf.Abs(carSpeed)).ToString();

        if (currentGearText != null)
        {
            string gearText;
            if (Mathf.Abs(carSpeed) < FirstGearResetSpeedKmh) gearText = "N";
            else if (carSpeed < 0f) gearText = "R";
            else gearText = _currentGear.ToString();
            currentGearText.text = gearText;
        }
    }

    public void CarSpeedUI() => UpdateUI();

    private void UpdateSounds()
    {
        if (!_useSounds) return;

        if (carEngineSound != null)
        {
            if (_requestedDirection != 0)
            {
                float rpmT = Mathf.Clamp01((_currentRPM - _s.idleRPM) / Mathf.Max(1f, _s.maxRPM - _s.idleRPM));
                carEngineSound.pitch = _initialEnginePitch + rpmT;
            }
            else
            {
                carEngineSound.pitch = _initialEnginePitch + _rigidbody.velocity.magnitude / IdleEnginePitchDivisor;
            }
        }

        if (tireScreechSound != null)
        {
            bool playScreech = isDrifting || (isTractionLocked && Mathf.Abs(carSpeed) > SkidMinSpeedKmh);
            if (playScreech && !tireScreechSound.isPlaying) tireScreechSound.Play();
            else if (!playScreech && tireScreechSound.isPlaying) tireScreechSound.Stop();
        }
    }

    public void CarSounds() => UpdateSounds();

    private void StopSounds()
    {
        if (carEngineSound != null) carEngineSound.Stop();
        if (tireScreechSound != null) tireScreechSound.Stop();
    }

    private void OnPauseChanged(bool paused)
    {
        if (!_useSounds) return;

        if (paused)
        {
            if (carEngineSound != null) carEngineSound.Pause();
            if (tireScreechSound != null) tireScreechSound.Pause();
        }
        else
        {
            if (carEngineSound != null) carEngineSound.UnPause();
            if (tireScreechSound != null) tireScreechSound.UnPause();
        }
    }

    private void OnDisable()
    {
        GameState.PauseChanged -= OnPauseChanged;

        if (_useSounds) StopSounds();
        StopEffects();
    }
}