using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Infrastructure.SaveSystem
{
    public sealed class JsonFileGateway
    {
        private const string UserFilePattern = "user{0}.json";
        private const string SettingsFileName = "settings.json";

        public string RootPath { get; }

        public JsonFileGateway()
        {
            RootPath = Path.Combine(Application.persistentDataPath, "Saves");
            Directory.CreateDirectory(RootPath);
        }

        public string GetSettingsPath()
            => Path.Combine(RootPath, SettingsFileName);

        public string GetUserPath(int index)
            => Path.Combine(RootPath, string.Format(UserFilePattern, index));

        public bool Exists(string path) => File.Exists(path);

        public void Write(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }

        public string Read(string path)
        {
            if (!File.Exists(path))
                return string.Empty;

            return File.ReadAllText(path);
        }

        public void Delete(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        public int CreateNextUserFile()
        {
            int index = 0;

            while (File.Exists(GetUserPath(index)))
                index++;

            Write(GetUserPath(index), "{}");
            return index;
        }

        public IReadOnlyList<SaveSlotInfo> EnumerateUserFiles()
        {
            var result = new List<SaveSlotInfo>();

            foreach (var file in Directory.GetFiles(RootPath, "user*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string number = name.Replace("user", "");

                if (!int.TryParse(number, out int index))
                    continue;

                var info = new FileInfo(file);

                result.Add(new SaveSlotInfo(
                    index,
                    file,
                    info.CreationTime,
                    info.LastWriteTime));
            }

            result.Sort((a, b) => a.Index.CompareTo(b.Index));
            return result;
        }
    }
}