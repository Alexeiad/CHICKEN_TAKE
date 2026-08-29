using UnityEngine;

namespace Infrastructure.Settings
{
    public sealed class GameSettingsLoader : MonoBehaviour
    {
        [SerializeField] private GameSettingsSO settings;

        private GameSettingsSaveService _service;

        private void Awake()
        {
            _service = new GameSettingsSaveService();
            _service.Load(settings);
        }
    }
}