using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class Speedometer : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private TMP_Text _speedText;
    [SerializeField] private RectTransform _needle;
    [SerializeField] private Image _gaugeFill;

    [Header("Настройки")]
    [SerializeField] private float _maxSpeedKmh = 180f;
    [SerializeField] private float _startAngle = 135f;
    [SerializeField] private float _sweepAngle = 270f;
    [SerializeField] private float _smoothTime = 0.08f;

    [Header("Цель")]
    [SerializeField] private Transform _targetOverride;

    private const float MsToKmh = 3.6f;

    private Transform _target;
    private Rigidbody _targetRb;
    private Vector3 _lastPosition;
    private bool _hasLastPosition;
    private float _currentKmh;
    private float _needleVel;
    private int _lastShownInt = -1;

    [Inject]
    public void Construct(Vechicle vehicle)
    {
        if (vehicle != null) SetTarget(vehicle.transform);
    }

    private void Awake()
    {
        if (_needle != null)
        {
            // Устанавливаем Pivot на самый низ стрелки (центр основания)
            _needle.pivot = new Vector2(0.5f, 0f);
            _needle.localRotation = Quaternion.Euler(0f, 0f, _startAngle);
        }

        if (_target == null && _targetOverride != null)
            SetTarget(_targetOverride);
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

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float rawKmh = ReadSpeedKmh(dt);
        _currentKmh = Mathf.SmoothDamp(_currentKmh, rawKmh, ref _needleVel, _smoothTime);

        float t = Mathf.Clamp01(_currentKmh / Mathf.Max(1f, _maxSpeedKmh));

        if (_needle != null)
        {
            float angle = _startAngle - t * _sweepAngle;
            _needle.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (_gaugeFill != null)
        {
            _gaugeFill.fillAmount = t * (_sweepAngle / 360f);
            _gaugeFill.color = Color.Lerp(new Color(0.2f, 0.9f, 0.45f), new Color(1f, 0.25f, 0.2f), t * t);
        }

        if (_speedText != null)
        {
            int shown = Mathf.RoundToInt(_currentKmh);
            if (shown != _lastShownInt)
            {
                _lastShownInt = shown;
                _speedText.text = shown.ToString();
            }
        }
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