using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class Speedometer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text _speedText;
    [Tooltip("Image с Image Type = Filled (Radial 360), заполняется по скорости")]
    [SerializeField] private Image _gauge;
    [Tooltip("Необязательно: подпись максимальной скорости на шкале")]
    [SerializeField] private TMP_Text _maxLabel;

    [Header("Настройки")]
    [SerializeField] private float _maxSpeedKmh = 200f;
    [SerializeField] private float _smoothing = 8f;

    [Header("Шкала")]
    [Tooltip("Какая доля круга занята шкалой: 0.75 = 270 градусов")]
    [SerializeField, Range(0.1f, 1f)] private float _gaugeSweep = 0.75f;
    [Tooltip("Цвет шкалы от медленной скорости (слева) к максимальной (справа)")]
    [SerializeField] private Gradient _gaugeGradient = CreateDefaultGradient();

    private const float MsToKmh = 3.6f;
    private const float MaxStepPerFrame = 20f;

    private Vechicle _vehicle;
    private Vector3 _lastPosition;
    private float _speedKmh;
    private int _shownSpeed = -1;

    [Inject]
    public void Construct(Vechicle vehicle)
    {
        _vehicle = vehicle;
    }

    private void Start()
    {
        _lastPosition = _vehicle.transform.position;

        if (_maxLabel != null)
            _maxLabel.text = Mathf.RoundToInt(_maxSpeedKmh).ToString();

        Render();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateSpeed(dt);
        Render();
    }

    private void UpdateSpeed(float dt)
    {
        Vector3 position = _vehicle.transform.position;
        float step = Vector3.Distance(position, _lastPosition);
        _lastPosition = position;

        // Телепорт или респавн в скорость не идут.
        if (step > MaxStepPerFrame) return;

        float rawKmh = step / dt * MsToKmh;
        _speedKmh = Mathf.Lerp(_speedKmh, rawKmh, 1f - Mathf.Exp(-_smoothing * dt));
    }

    private void Render()
    {
        int speed = Mathf.RoundToInt(_speedKmh);

        if (speed != _shownSpeed)
        {
            _shownSpeed = speed;
            _speedText.text = speed.ToString();
        }

        if (_gauge == null) return;

        float normalized = Mathf.Clamp01(_speedKmh / _maxSpeedKmh);
        _gauge.fillAmount = normalized * _gaugeSweep;
        _gauge.color = _gaugeGradient.Evaluate(normalized);
    }

    private static Gradient CreateDefaultGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.20f, 0.90f, 0.40f), 0f),
                new GradientColorKey(new Color(1.00f, 0.85f, 0.20f), 0.6f),
                new GradientColorKey(new Color(1.00f, 0.25f, 0.25f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            });
        return gradient;
    }
}