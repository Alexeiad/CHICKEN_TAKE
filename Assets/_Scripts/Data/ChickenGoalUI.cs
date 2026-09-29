using TMPro;
using UnityEngine;

public class ChickenGoalUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerZoneEventSO _zoneEvent;
    [SerializeField] private TextMeshProUGUI _goalText;
    [SerializeField] private GameObject _victoryCanvas;

    [Header("Goal")]
    [Tooltip("Стартовая цель. Используется только при сбросе/нулевом прогрессе")]
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
    private string GoalKey => $"{_saveKey}_TargetGoal";

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

        bool goalReached = false;

        while (_currentProgress >= _currentGoal)
        {
            _goalLevel++;
            _currentGoal = CalculateGoal(_goalLevel);
            goalReached = true;
        }

        Save();
        UpdateText();

        if (goalReached)
        {
            ShowVictory();
        }
    }

    private void ShowVictory()
    {
        if (_victoryCanvas != null)
            _victoryCanvas.SetActive(true);

        SetCameraScriptsActive(false);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        if (_victoryCanvas != null)
            _victoryCanvas.SetActive(false);

        SetCameraScriptsActive(true);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ResetSave()
    {
        PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.DeleteKey(LevelKey);
        PlayerPrefs.DeleteKey(GoalKey);
        PlayerPrefs.Save();

        var allGoals = FindObjectsOfType<ChickenGoalUI>();
        foreach (var goalUI in allGoals)
        {
            goalUI.Load();
            goalUI.UpdateText();
        }
    }

    private void SetCameraScriptsActive(bool state)
    {
        if (Camera.main == null)
            return;

        foreach (var script in Camera.main.GetComponents<MonoBehaviour>())
        {
            if (script != this)
            {
                script.enabled = state;
            }
        }
    }

    private int CalculateGoal(int level)
    {
        float goal = _startGoal * Mathf.Pow(_growthMultiplier, level);
        int roundedGoal = Mathf.RoundToInt(goal / _roundStep) * _roundStep;
        return Mathf.Max(_startGoal, roundedGoal);
    }

    private void Save()
    {
        PlayerPrefs.SetInt(ProgressKey, _currentProgress);
        PlayerPrefs.SetInt(LevelKey, _goalLevel);
        PlayerPrefs.SetInt(GoalKey, _currentGoal);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        _currentProgress = PlayerPrefs.GetInt(ProgressKey, 0);
        _goalLevel = PlayerPrefs.GetInt(LevelKey, 0);

        int defaultGoal = CalculateGoal(_goalLevel);
        _currentGoal = PlayerPrefs.GetInt(GoalKey, defaultGoal);
    }

    private void UpdateText()
    {
        if (_goalText != null)
            _goalText.text = $"{_currentProgress}/{_currentGoal}";
    }
}