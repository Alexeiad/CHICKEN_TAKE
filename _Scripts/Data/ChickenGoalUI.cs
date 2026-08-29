using TMPro;
using UnityEngine;

public class ChickenGoalUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerZoneEventSO _zoneEvent;
    [SerializeField] private TextMeshProUGUI _goalText;

    [Header("Goal")]
    [SerializeField] private int _startGoal = 10;

    [Tooltip("Коэффициент геометрической прогрессии")]
    [SerializeField] private float _growthMultiplier = 1.5f;

    [Tooltip("Округление цели до этого шага")]
    [SerializeField] private int _roundStep = 5;

    [Header("Save")]
    [SerializeField] private string _saveKey = "ChickenGoal";

    private int _currentProgress;
    private int _currentGoal;
    private int _goalLevel;

    private string ProgressKey => $"{_saveKey}_Progress";
    private string LevelKey => $"{_saveKey}_Level";

    private void Awake()
    {
        Load();
        UpdateText();
    }

    private void OnEnable()
    {
        if (_zoneEvent != null)
            _zoneEvent.OnSell += OnSell;
    }

    private void OnDisable()
    {
        if (_zoneEvent != null)
            _zoneEvent.OnSell -= OnSell;
    }

    private void OnSell(int coins, int chickens)
    {
        if (chickens <= 0)
            return;

        _currentProgress += chickens;

        while (_currentProgress >= _currentGoal)
        {
            _currentProgress -= _currentGoal;
            _goalLevel++;

            _currentGoal = CalculateGoal(_goalLevel);
        }

        Save();
        UpdateText();
    }

    private int CalculateGoal(int level)
    {
        float goal = _startGoal * Mathf.Pow(
            _growthMultiplier,
            level
        );

        int roundedGoal = Mathf.RoundToInt(
            goal / _roundStep
        ) * _roundStep;

        return Mathf.Max(_startGoal, roundedGoal);
    }

    private void Save()
    {
        PlayerPrefs.SetInt(ProgressKey, _currentProgress);
        PlayerPrefs.SetInt(LevelKey, _goalLevel);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        _currentProgress = PlayerPrefs.GetInt(
            ProgressKey,
            0
        );

        _goalLevel = PlayerPrefs.GetInt(
            LevelKey,
            0
        );

        _currentGoal = CalculateGoal(_goalLevel);
    }

    private void UpdateText()
    {
        if (_goalText == null)
            return;

        _goalText.text = $"{_currentProgress}/{_currentGoal}";
    }
}