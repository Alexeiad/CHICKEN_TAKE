using TMPro;
using UnityEngine;
using Zenject;

public sealed class Speedometer : MonoBehaviour
{
    public const int ReverseGearCode = -1;

    [Header("Ссылки")]
    [SerializeField] private RectTransform _gauge;
    [SerializeField] private RectTransform _needle;
    [SerializeField] private TMP_Text _speedText;
    [SerializeField] private TMP_Text _gearText;

    [Header("Pivot стрелки")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _pivotY = 0f;

    [Header("Углы")]
    [SerializeField] private float _minAngle = 135f;
    [SerializeField] private float _maxAngle = -135f;

    [Header("Настройки")]
    [SerializeField] private float _maxSpeedKmh = 180f;
    [SerializeField] private float _smoothTime = 0.08f;
    [Tooltip("Порог скорости (км/ч), ниже которого движение назад не считается R при околонулевом дрейфе")]
    [SerializeField] private float _reverseThresholdKmh = 0.5f;

    [Header("Передача")]
    [Tooltip("Пороги скорости (км/ч) для переключения. Кол-во порогов = кол-во передач - 1")]
    [SerializeField] private float[] _gearThresholds = { 20f, 50f, 90f, 130f };

    private const float MsToKmh = 3.6f;

    private Transform _target;
    private Rigidbody _targetRb;
    private PrometeoCarController _carController;
    private Vector3 _lastPosition;
    private bool _hasLastPosition;
    private float _signedKmh;
    private float _speedVel;
    private string _lastShownSpeed = string.Empty;
    private int _lastShownGear = int.MinValue;

    [Inject]
    public void Construct(Vechicle vehicle)
    {
        if (vehicle != null) SetTarget(vehicle.transform);
    }

    private void Awake()
    {
        if (_needle != null)
            _needle.pivot = new Vector2(0.5f, _pivotY);

        SetNeedleAngle(_minAngle);
    }

    private void Start()
    {
        if (_target == null)
        {
            Vechicle found = FindObjectOfType<Vechicle>();
            if (found != null) SetTarget(found.transform);
        }
    }

    public void SetTarget(Transform target)
    {
        _target = target;
        if (target != null)
        {
            _targetRb = target.GetComponentInParent<Rigidbody>();
            _carController = target.GetComponentInParent<PrometeoCarController>();
        }
        else
        {
            _targetRb = null;
            _carController = null;
        }
        _hasLastPosition = false;
    }

    private void LateUpdate()
    {
        if (_needle != null && _gauge != null)
            _needle.position = _gauge.position;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float rawSignedKmh = ReadSignedSpeedKmh(dt);
        _signedKmh = Mathf.SmoothDamp(_signedKmh, rawSignedKmh, ref _speedVel, _smoothTime);

        float absKmh = Mathf.Abs(_signedKmh);
        float t = Mathf.Clamp01(absKmh / Mathf.Max(1f, _maxSpeedKmh));
        SetNeedleAngle(Mathf.Lerp(_minAngle, _maxAngle, t));

        if (_speedText != null)
        {
            string shown = Mathf.RoundToInt(absKmh).ToString();
            if (shown != _lastShownSpeed)
            {
                _lastShownSpeed = shown;
                _speedText.text = shown;
            }
        }

        if (_gearText != null)
        {
            int gear = GetGear(_signedKmh);
            if (gear != _lastShownGear)
            {
                _lastShownGear = gear;
                _gearText.text = FormatGear(gear);
            }
        }
    }

    // -1 = R (задний ход), 1 = 1 передача (0 км/ч), 2+ = выше порогов
    private int GetGear(float signedKmh)
    {
        if (_carController != null && _carController.carSpeed < -_reverseThresholdKmh)
            return ReverseGearCode;

        if (signedKmh < -_reverseThresholdKmh)
            return ReverseGearCode;

        float forwardKmh = Mathf.Max(0f, signedKmh);
        int gear = 1;
        for (int i = 0; i < _gearThresholds.Length; i++)
            if (forwardKmh >= _gearThresholds[i]) gear = i + 2;
        return gear;
    }

    private static string FormatGear(int gear)
    {
        return gear == ReverseGearCode ? "R" : gear.ToString();
    }

    private void SetNeedleAngle(float angle)
    {
        if (_needle != null)
            _needle.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private float ReadSignedSpeedKmh(float dt)
    {
        if (_target == null) return 0f;

        if (_carController != null)
            return _carController.carSpeed;

        if (_targetRb != null && !_targetRb.isKinematic)
        {
            float signedMs = Vector3.Dot(_targetRb.velocity, _target.forward);
            return signedMs * MsToKmh;
        }

        Vector3 pos = _target.position;
        if (!_hasLastPosition)
        {
            _lastPosition = pos;
            _hasLastPosition = true;
            return 0f;
        }

        Vector3 delta = pos - _lastPosition;
        _lastPosition = pos;

        float dist = delta.magnitude;
        if (dist > 25f) return _signedKmh;

        float signedDist = Vector3.Dot(delta, _target.forward);
        return (signedDist / dt) * MsToKmh;
    }
}