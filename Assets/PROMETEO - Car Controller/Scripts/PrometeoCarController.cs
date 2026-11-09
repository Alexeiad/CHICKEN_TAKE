using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class PrometeoCarController : MonoBehaviour
{
    [Header("CAR SETUP")]
    public bool isAi;

    [Range(20, 5000)] public int maxSpeed = 90;
    [Range(10, 1200)] public int maxReverseSpeed = 45;
    [Range(1, 1000)] public int accelerationMultiplier = 2;
    [Range(10, 45)] public int maxSteeringAngle = 27;
    [Range(0.1f, 5f)] public float steeringSpeed = 0.5f;
    [Range(100, 600)] public int brakeForce = 350;
    [Range(1, 100)] public int decelerationMultiplier = 2;
    [Range(1, 100)] public int handbrakeDriftMultiplier = 5;

    [Header("ENGINE BRAKE")]
    [Tooltip("“ормоз при отпущенном газе. 100 = почти не тормозит, 600+ = ощутимо замедл€ет.")]
    [Range(0, 2000)] public float engineBrakeForce = 600f;

    public Vector3 bodyMassCenter;

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
    public bool useEffects;
    public ParticleSystem RLWParticleSystem;
    public ParticleSystem RRWParticleSystem;
    public TrailRenderer RLWTireSkid;
    public TrailRenderer RRWTireSkid;

    [Header("UI")]
    public bool useUI;
    public Text carSpeedText;

    [Header("SOUNDS")]
    public bool useSounds;
    public AudioSource carEngineSound;
    public AudioSource tireScreechSound;

    [Header("TOUCH CONTROLS")]
    public bool useTouchControls;
    public GameObject throttleButton;
    public GameObject reverseButton;
    public GameObject turnRightButton;
    public GameObject turnLeftButton;
    public GameObject handbrakeButton;

    [Header("CAR DATA")]
    [HideInInspector] public float carSpeed;
    [HideInInspector] public bool isDrifting;
    [HideInInspector] public bool isTractionLocked;

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

    private const float StopThreshold = 0.35f;
    private const float DriftThreshold = 2.5f;

    private void Start() { Initialize(); }

    private void Update()
    {
        if (!_initialized) return;

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
        if (!_initialized) return;

        UpdateCarData();
        ApplyDrivePhysics();
        ApplySteering();            // теперь руль всегда примен€етс€ в физике
    }

    private void Initialize()
    {
        _rigidbody = GetComponent<Rigidbody>();

        if (!HasRequiredWheels())
        {
            Debug.LogError("PrometeoCarController: Missing WheelColliders.", this);
            enabled = false;
            return;
        }

        _rigidbody.centerOfMass = bodyMassCenter;

        _wheels = new[] { frontLeftCollider, frontRightCollider, rearLeftCollider, rearRightCollider };
        _wheelMeshes = new[] { frontLeftMesh, frontRightMesh, rearLeftMesh, rearRightMesh };

        CacheWheelFriction();
        SetupTouchControls();

        if (carEngineSound != null) _initialEnginePitch = carEngineSound.pitch;
        if (!useSounds) StopSounds();
        if (!useEffects) StopEffects();
        if (carSpeedText != null && !useUI) carSpeedText.text = "0";

        _requestedDirection = 0;
        _brakeRequested = false;
        _handbrakeRequested = false;

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
        if (!useTouchControls) return;
        _throttleInput = GetTouchInput(throttleButton);
        _reverseInput = GetTouchInput(reverseButton);
        _leftInput = GetTouchInput(turnLeftButton);
        _rightInput = GetTouchInput(turnRightButton);
        _handbrakeInput = GetTouchInput(handbrakeButton);

        if (_throttleInput == null || _reverseInput == null ||
            _leftInput == null || _rightInput == null || _handbrakeInput == null)
        {
            Debug.LogWarning("PrometeoCarController: Touch controls are not configured.", this);
            useTouchControls = false;
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

        if (useTouchControls)
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
        if (_brakeRequested) { SetBrakeTorque(brakeForce); return; }

        // √аз отпущен Ч двигатель тормозит.
        if (_requestedDirection == 0)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(engineBrakeForce);
            return;
        }

        // ѕереключение направлени€ Ч сначала тормозим.
        if (_localVelocityZ * _requestedDirection < -StopThreshold)
        {
            SetBrakeTorque(brakeForce);
            return;
        }

        float currentSpeed = Mathf.Abs(carSpeed);
        float speedLimit = _requestedDirection > 0 ? maxSpeed : maxReverseSpeed;

        if (currentSpeed >= speedLimit)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(0f);
            return;
        }

        SetBrakeTorque(0f);
        SetMotorTorque(accelerationMultiplier * 50f * _requestedDirection);
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
        _steeringAxis = Mathf.Max(-1f, _steeringAxis - Time.deltaTime * 10f * steeringSpeed);
    }

    public void TurnRight()
    {
        _steeringAxis = Mathf.Min(1f, _steeringAxis + Time.deltaTime * 10f * steeringSpeed);
    }

    public void ResetSteeringAngle()
    {
        _steeringAxis = Mathf.MoveTowards(_steeringAxis, 0f, Time.deltaTime * 10f * steeringSpeed);
    }

    private void ApplySteering()
    {
        if (!_initialized) return;
        float angle = _steeringAxis * maxSteeringAngle;
        float t = Mathf.Clamp01(steeringSpeed * Time.fixedDeltaTime * 20f);

        frontLeftCollider.steerAngle = Mathf.Lerp(frontLeftCollider.steerAngle, angle, t);
        frontRightCollider.steerAngle = Mathf.Lerp(frontRightCollider.steerAngle, angle, t);
    }

    // HANDBRAKE

    public void Handbrake()
    {
        _requestedDirection = 0;
        _brakeRequested = false;
        _handbrakeRequested = true;
        _driftingAxis = Mathf.Clamp01(_driftingAxis + Time.deltaTime);
        isTractionLocked = true;
        UpdateDrifting();
        SetDriftFriction();
    }

    public void RecoverTraction()
    {
        _handbrakeRequested = false;
        isTractionLocked = false;
        _driftingAxis = Mathf.MoveTowards(_driftingAxis, 0f, Time.deltaTime / 1.5f);

        if (_wheels == null) return;
        for (int i = 0; i < _wheels.Length; i++)
        {
            WheelFrictionCurve friction = _wheels[i].sidewaysFriction;
            friction.extremumSlip = Mathf.Lerp(
                _defaultExtremumSlip[i],
                _defaultExtremumSlip[i] * handbrakeDriftMultiplier,
                _driftingAxis);
            _wheels[i].sidewaysFriction = friction;
        }
    }

    private void ApplyHandbrakeTorque()
    {
        frontLeftCollider.motorTorque = 0f; frontLeftCollider.brakeTorque = 0f;
        frontRightCollider.motorTorque = 0f; frontRightCollider.brakeTorque = 0f;
        rearLeftCollider.motorTorque = 0f; rearLeftCollider.brakeTorque = brakeForce * 2f;
        rearRightCollider.motorTorque = 0f; rearRightCollider.brakeTorque = brakeForce * 2f;
    }

    private void SetDriftFriction()
    {
        for (int i = 0; i < _wheels.Length; i++)
        {
            WheelFrictionCurve friction = _defaultFriction[i];
            friction.extremumSlip = _defaultExtremumSlip[i] * handbrakeDriftMultiplier * _driftingAxis;
            _wheels[i].sidewaysFriction = friction;
        }
    }

    private void UpdateDrifting()
    {
        isDrifting = Mathf.Abs(_localVelocityX) > DriftThreshold;
    }

    // WHEELS

    private void AnimateWheels()
    {
        for (int i = 0; i < _wheels.Length; i++)
        {
            if (_wheelMeshes[i] == null) continue;
            _wheels[i].GetWorldPose(out Vector3 position, out Quaternion rotation);
            _wheelMeshes[i].transform.SetPositionAndRotation(position, rotation);
        }
    }

    // EFFECTS

    private void UpdateEffects()
    {
        if (!useEffects) return;
        bool drifting = isDrifting;
        bool skidding = (isTractionLocked || Mathf.Abs(_localVelocityX) > 5f) && Mathf.Abs(carSpeed) > 12f;

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

    // UI

    private void UpdateUI()
    {
        if (useUI && carSpeedText != null)
            carSpeedText.text = Mathf.RoundToInt(Mathf.Abs(carSpeed)).ToString();
    }

    public void CarSpeedUI() => UpdateUI();

    // SOUNDS

    private void UpdateSounds()
    {
        if (!useSounds) return;
        if (carEngineSound != null)
            carEngineSound.pitch = _initialEnginePitch + _rigidbody.velocity.magnitude / 25f;

        if (tireScreechSound != null)
        {
            bool playScreech = isDrifting || (isTractionLocked && Mathf.Abs(carSpeed) > 12f);
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

    private void OnDisable()
    {
        if (useSounds) StopSounds();
        StopEffects();
    }
}