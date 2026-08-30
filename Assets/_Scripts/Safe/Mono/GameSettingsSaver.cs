using UnityEngine;

namespace Infrastructure.Settings
{
    public sealed class GameSettingsSaver : MonoBehaviour
    {
        [SerializeField] private GameSettingsSO settings;

        private GameSettingsSaveService _service;

        private void Awake()
        {
            _service = new GameSettingsSaveService();
        }

        public void Save()
        {
            _service.Save(settings);
        }
    }
}