using Infrastructure.SaveSystem;

namespace Infrastructure.Settings
{
    public sealed class GameSettingsSaveService
    {
        private readonly JsonFileGateway _gateway = new();
        private readonly ReflectionSerializer _serializer = new();

        public void Save(GameSettingsSO settings)
        {
            string json = _serializer.Serialize(settings);
            _gateway.Write(_gateway.GetSettingsPath(), json);
        }

        public void Load(GameSettingsSO settings)
        {
            string path = _gateway.GetSettingsPath();

            if (!_gateway.Exists(path))
                return;

            string json = _gateway.Read(path);
            _serializer.DeserializeInto(json, settings);
        }
    }
}