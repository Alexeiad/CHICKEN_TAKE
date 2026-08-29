using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CameraSettingsUI : MonoBehaviour
{
    private const float FirstPersonDistance = 0.01f;
    private const float ThirdPersonDefaultDistance = 3f;

    [SerializeField] private GameSettingsSO _settings;

    [Header("Sliders")]
    [SerializeField] private Slider _sensitivitySlider;
    [SerializeField] private Slider _distanceSlider;

    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown _keyDropdown;
    [SerializeField] private TMP_Dropdown _cameraModeDropdown;

    private readonly KeyCode[] _availableKeys =
    {
        KeyCode.V,
        KeyCode.C,
        KeyCode.Q
    };

    private bool _isUpdating;

    private void Awake()
    {
        // Слайдеры
        _sensitivitySlider.SetValueWithoutNotify(_settings.Camera.Sensitivity);
        _distanceSlider.SetValueWithoutNotify(_settings.Camera.Distance);

        _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        _distanceSlider.onValueChanged.AddListener(OnDistanceChanged);

        // Клавиши
        _keyDropdown.ClearOptions();
        _keyDropdown.AddOptions(new List<string> { "V", "C", "Q" });

        int keyIndex = Array.IndexOf(_availableKeys, _settings.Camera.Key);
        if (keyIndex < 0)
            keyIndex = 0;

        _keyDropdown.SetValueWithoutNotify(keyIndex);
        _keyDropdown.onValueChanged.AddListener(OnKeyChanged);

        
        _cameraModeDropdown.SetValueWithoutNotify((int)_settings.Camera.Mode);
        _cameraModeDropdown.onValueChanged.AddListener(OnCameraModeChanged);

        // Первичная синхронизация
        SyncModeWithDistance(_settings.Camera.Distance);
    }

    private void OnDestroy()
    {
        _sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
        _distanceSlider.onValueChanged.RemoveListener(OnDistanceChanged);

        _keyDropdown.onValueChanged.RemoveListener(OnKeyChanged);
        _cameraModeDropdown.onValueChanged.RemoveListener(OnCameraModeChanged);
    }

    private void OnSensitivityChanged(float value)
    {
        _settings.Camera.Sensitivity = value;
    }

    private void OnDistanceChanged(float value)
    {
        if (_isUpdating)
            return;

        _settings.Camera.Distance = value;
        _settings.Camera.MaxDistance = value;
        SyncModeWithDistance(value);
    }

    private void OnKeyChanged(int index)
    {
        _settings.Camera.Key = _availableKeys[index];
    }

    private void OnCameraModeChanged(int index)
    {
        if (_isUpdating)
            return;

        _isUpdating = true;

        CameraMode mode = (CameraMode)index;
        _settings.Camera.Mode = mode;

        if (mode == CameraMode.FirstPerson)
        {
            _settings.Camera.Distance = FirstPersonDistance;
            _distanceSlider.SetValueWithoutNotify(FirstPersonDistance);
        }
        else
        {
            if (_settings.Camera.Distance <= FirstPersonDistance)
            {
                _settings.Camera.Distance = ThirdPersonDefaultDistance;
                _distanceSlider.SetValueWithoutNotify(ThirdPersonDefaultDistance);
            }
        }

        _isUpdating = false;
    }

    private void SyncModeWithDistance(float distance)
    {
        _isUpdating = true;

        CameraMode mode = distance <= FirstPersonDistance
            ? CameraMode.FirstPerson
            : CameraMode.ThirdPerson;

        _settings.Camera.Mode = mode;
        _cameraModeDropdown.SetValueWithoutNotify((int)mode);

        _isUpdating = false;
    }
}