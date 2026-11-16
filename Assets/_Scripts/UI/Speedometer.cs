using TMPro;
using UnityEngine;
using Zenject;

public sealed class Speedometer : MonoBehaviour
{
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

    [Header("Передача")]
    [Tooltip("Пороги скорости (км/ч) для переключения. Кол-во порогов = кол-во передач - 1")]
    [SerializeField] private float[] _gearThresholds = { 20f, 50f, 90f, 130f };

    private const float MsToKmh = 3.6f;

    private Transform _target;
    private Rigidbody _targetRb;
    private Vector3 _lastPosition;
    private bool _hasLastPosition;
    private float _currentKmh;
    private float _speedVel;
    private int _lastShownInt = -1;
    private int _lastShownGear = -1;

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
        _targetRb = target != null ? target.GetComponentInParent<Rigidbody>() : null;
        _hasLastPosition = false;
    }

    private void LateUpdate()
    {
        if (_needle != null && _gauge != null)
            _needle.position = _gauge.position;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float rawKmh = ReadSpeedKmh(dt);
        _currentKmh = Mathf.SmoothDamp(_currentKmh, rawKmh, ref _speedVel, _smoothTime);

        float t = Mathf.Clamp01(_currentKmh / Mathf.Max(1f, _maxSpeedKmh));
        SetNeedleAngle(Mathf.Lerp(_minAngle, _maxAngle, t));

        if (_speedText != null)
        {
            int shown = Mathf.RoundToInt(_currentKmh);
            if (shown != _lastShownInt)
            {
                _lastShownInt = shown;
                _speedText.text = shown.ToString();
            }
        }

        if (_gearText != null)
        {
            int gear = GetGear(_currentKmh);
            if (gear != _lastShownGear)
            {
                _lastShownGear = gear;
                _gearText.text = gear.ToString();
            }
        }
    }

    // 1 передача = 0 км/ч, 2 = выше первого порога, и т.д.
    private int GetGear(float kmh)
    {
        int gear = 1;
        for (int i = 0; i < _gearThresholds.Length; i++)
            if (kmh >= _gearThresholds[i]) gear = i + 2;
        return gear;
    }

    private void SetNeedleAngle(float angle)
    {
        if (_needle != null)
            _needle.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private float ReadSpeedKmh(float dt)
    {
        if (_target == null) return 0f;

        if (_targetRb != null && !_targetRb.isKinematic)
            return _targetRb.velocity.magnitude * MsToKmh;

        Vector3 pos = _target.position;
        if (!_hasLastPosition)
        {
            _lastPosition = pos;
            _hasLastPosition = true;
            return 0f;
        }

        float dist = Vector3.Distance(pos, _lastPosition);
        _lastPosition = pos;

        if (dist > 25f) return _currentKmh;
        return (dist / dt) * MsToKmh;
    }
}