using Infrastructure.SaveSystem;

namespace Infrastructure.Profile
{
    public sealed class UserProfileSaveService
    {
        private readonly JsonFileGateway _gateway = new();
        private readonly ReflectionSerializer _serializer = new();

        public void Save(int slotIndex, UserProfileSO profile)
        {
            string json = _serializer.Serialize(profile);
            _gateway.Write(_gateway.GetUserPath(slotIndex), json);
        }

        public void Load(int slotIndex, UserProfileSO profile)
        {
            string path = _gateway.GetUserPath(slotIndex);

            if (!_gateway.Exists(path))
                return;

            string json = _gateway.Read(path);
            _serializer.DeserializeInto(json, profile);
        }

        public void Delete(int slotIndex)
        {
            _gateway.Delete(_gateway.GetUserPath(slotIndex));
        }
    }
}