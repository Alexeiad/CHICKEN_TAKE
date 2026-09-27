using TMPro;
using UnityEngine;

public class ChickenQuest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerZoneEventSO _zoneEvent;
    [SerializeField] private PlayerDataSO _playerData;
    [SerializeField] private TextMeshProUGUI _goalText;
    [SerializeField] private TextMeshProUGUI _rewardText;

    [Header("Goal")]
    [SerializeField] private int _startGoal = 50;

    [Tooltip("Коэффициент геометрической прогрессии")]
    [SerializeField] private float _growthMultiplier = 1.4f;

    [Tooltip("Округление цели")]
    [SerializeField] private int _roundStep = 5;

    [Header("Save")]
    [SerializeField] private string _saveKey = "ChickenGoal";

    private int _currentProgress;
    private int _currentGoal;
    private int _goalLevel;
    private int _rewardedMoney;

    private string ProgressKey => $"{_saveKey}_Progress";
    private string LevelKey => $"{_saveKey}_Level";
    private string RewardedKey => $"{_saveKey}_Rewarded";

    private void Awake()
    {
        Load();
        UpdateUI();
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

        CheckGoals();

        Save();
        UpdateUI();
    }

    private void CheckGoals()
    {
        while (_currentProgress >= _currentGoal)
        {
            _currentProgress -= _currentGoal;

            int reward = CalculateReward(_currentGoal);

            _playerData.Cash += reward;
            _rewardedMoney += reward;

            _goalLevel++;
            _currentGoal = CalculateGoal(_goalLevel);
        }
    }

    private int CalculateGoal(int level)
    {
        float goal = _startGoal * Mathf.Pow(
            _growthMultiplier,
            level
        );

        return Mathf.Max(
            _startGoal,
            Mathf.RoundToInt(goal / _roundStep) * _roundStep
        );
    }

    private int CalculateReward(int goal)
    {
        return goal / 5;
    }

    private void Save()
    {
        PlayerPrefs.SetInt(ProgressKey, _currentProgress);
        PlayerPrefs.SetInt(LevelKey, _goalLevel);
        PlayerPrefs.SetInt(RewardedKey, _rewardedMoney);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        _currentProgress = PlayerPrefs.GetInt(ProgressKey, 0);
        _goalLevel = PlayerPrefs.GetInt(LevelKey, 0);
        _rewardedMoney = PlayerPrefs.GetInt(RewardedKey, 0);

        _currentGoal = CalculateGoal(_goalLevel);
    }

    private void UpdateUI()
    {
        if (_goalText != null)
            _goalText.text = $"{_currentProgress}/{_currentGoal}";

        if (_rewardText != null)
            _rewardText.text = CalculateReward(_currentGoal).ToString();
    }

    public void ClearSave()
    {
        PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.DeleteKey(LevelKey);
        PlayerPrefs.DeleteKey(RewardedKey);
        PlayerPrefs.Save();

        _currentProgress = 0;
        _goalLevel = 0;
        _rewardedMoney = 0;
        _currentGoal = CalculateGoal(_goalLevel);

        UpdateUI();
    }
}