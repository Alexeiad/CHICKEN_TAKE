using UnityEngine;

public class VehicleController : MonoBehaviour
{
    public enum VehicleState
    {
        Idle,
        Accelerating,
        Reversing,
        Braking,
        Handbraking
    }

    [System.Serializable]
    public struct WheelPair
    {
        public WheelCollider collider;
        public Transform mesh;
    }

    [Header("Wheel Bindings")]
    [SerializeField] private WheelPair frontLeft;
    [SerializeField] private WheelPair frontRight;
    [SerializeField] private WheelPair rearLeft;
    [SerializeField] private WheelPair rearRight;

    [Header("Vehicle Specs")]
    [SerializeField] private float maxMotorTorque = 2000f;
    [SerializeField] private float maxBrakeTorque = 3000f;
    [SerializeField] private float handbrakeTorque = 8000f;
    [SerializeField] private float maxSteerAngle = 35f;
    [SerializeField] private float airPitchTorque = 1500f;

    [Header("Acceleration & Pitch Settings")]
    [Tooltip("Множитель физического наклона кузова при разгоне/торможении")]
    [SerializeField] private float bodyPitchTorque = 300f;

    [Tooltip("Минимальный питч звука (холостой ход)")]
    [SerializeField] private float minAudioPitch = 0.8f;

    [Tooltip("Максимальный питч звука")]
    [SerializeField] private float maxAudioPitch = 2.2f;

    [Tooltip("Чувствительность звукового питча к ускорению")]
    [SerializeField] private float audioPitchSensitivity = 0.05f;

    [Header("Commands (FSM Input)")]
    [SerializeField] private bool commandGas;
    [SerializeField] private bool commandBrake;
    [SerializeField] private bool commandHandbrake;
    [SerializeField] private bool commandLeft;
    [SerializeField] private bool commandRight;
    [SerializeField] private bool commandUp;
    [SerializeField] private bool commandDown;

    [Header("Current State")]
    [SerializeField] private VehicleState currentState = VehicleState.Idle;

    private Rigidbody rb;
    private Vector3 lastVelocity;
    private float currentForwardAcceleration;

    // Публичное свойство для получения текущего звукового питча (для FMOD / Audio)
    public float CurrentAudioPitch { get; private set; } = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass += new Vector3(0, -0.5f, 0);
    }

    private void Update()
    {
        UpdateFSM();
        UpdateWheelVisuals();
    }

    private void FixedUpdate()
    {
        CalculateAcceleration();
        ApplyControlAlgorithm();
        ApplyBodyPitchFromAcceleration();
    }

    // Расчет продольного ускорения и звукового питча
    private void CalculateAcceleration()
    {
        Vector3 currentVelocity = rb.velocity; // Для Unity 6+ (в старых версиях: rb.velocity)
        Vector3 accelVector = (currentVelocity - lastVelocity) / Time.fixedDeltaTime;

        // Векторное произведение дает ускорение именно по оси движения (вперед/назад)
        currentForwardAcceleration = Vector3.Dot(accelVector, transform.forward);
        lastVelocity = currentVelocity;

        // Расчет питча для аудио на основе ускорения и текущей скорости
        float speedFactor = currentVelocity.magnitude * 0.02f;
        float accelFactor = Mathf.Abs(currentForwardAcceleration) * audioPitchSensitivity;

        CurrentAudioPitch = Mathf.Clamp(minAudioPitch + speedFactor + accelFactor, minAudioPitch, maxAudioPitch);
    }

    // 1. Конечный автомат: определяем текущий режим движения
    private void UpdateFSM()
    {
        if (commandHandbrake)
        {
            currentState = VehicleState.Handbraking;
        }
        else if (commandBrake)
        {
            currentState = VehicleState.Braking;
        }
        else if (commandGas)
        {
            currentState = commandDown ? VehicleState.Reversing : VehicleState.Accelerating;
        }
        else
        {
            currentState = VehicleState.Idle;
        }
    }

    // 2. Алгоритм управления физикой и колесами
    private void ApplyControlAlgorithm()
    {
        float steerInput = 0f;
        if (commandLeft) steerInput -= 1f;
        if (commandRight) steerInput += 1f;

        float currentSteerAngle = steerInput * maxSteerAngle;
        frontLeft.collider.steerAngle = currentSteerAngle;
        frontRight.collider.steerAngle = currentSteerAngle;

        float currentMotor = 0f;
        float currentBrake = 0f;

        switch (currentState)
        {
            case VehicleState.Accelerating:
                currentMotor = maxMotorTorque;
                break;

            case VehicleState.Reversing:
                currentMotor = -maxMotorTorque * 0.5f;
                break;

            case VehicleState.Braking:
                currentBrake = maxBrakeTorque;
                break;

            case VehicleState.Handbraking:
                rearLeft.collider.brakeTorque = handbrakeTorque;
                rearRight.collider.brakeTorque = handbrakeTorque;
                break;

            case VehicleState.Idle:
                currentBrake = 10f;
                break;
        }

        rearLeft.collider.motorTorque = currentMotor;
        rearRight.collider.motorTorque = currentMotor;

        if (currentState != VehicleState.Handbraking)
        {
            frontLeft.collider.brakeTorque = currentBrake;
            frontRight.collider.brakeTorque = currentBrake;
            rearLeft.collider.brakeTorque = currentBrake;
            rearRight.collider.brakeTorque = currentBrake;
        }

        HandleAirControl();
    }

    // Физический наклон (Pitch) кузова при ускорении/торможении (имитация переноса массы)
    private void ApplyBodyPitchFromAcceleration()
    {
        bool isGrounded = frontLeft.collider.isGrounded || frontRight.collider.isGrounded ||
                          rearLeft.collider.isGrounded || rearRight.collider.isGrounded;

        if (isGrounded && Mathf.Abs(currentForwardAcceleration) > 0.1f)
        {
            // При разгоне нос задирается (-X torque), при торможении нос опускается (+X torque)
            float pitchTorque = -currentForwardAcceleration * bodyPitchTorque;
            rb.AddRelativeTorque(Vector3.right * pitchTorque, ForceMode.Force);
        }
    }

    private void HandleAirControl()
    {
        bool isGrounded = frontLeft.collider.isGrounded || frontRight.collider.isGrounded ||
                          rearLeft.collider.isGrounded || rearRight.collider.isGrounded;

        if (!isGrounded)
        {
            float pitchInput = 0f;
            if (commandUp) pitchInput += 1f;
            if (commandDown) pitchInput -= 1f;

            if (Mathf.Abs(pitchInput) > 0.01f)
            {
                rb.AddRelativeTorque(Vector3.right * pitchInput * airPitchTorque, ForceMode.Acceleration);
            }
        }
    }

    private void UpdateWheelVisuals()
    {
        SyncWheelMesh(frontLeft);
        SyncWheelMesh(frontRight);
        SyncWheelMesh(rearLeft);
        SyncWheelMesh(rearRight);
    }

    private void SyncWheelMesh(WheelPair pair)
    {
        if (pair.collider == null || pair.mesh == null) return;

        pair.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        pair.mesh.position = pos;
        pair.mesh.rotation = rot;
    }
}