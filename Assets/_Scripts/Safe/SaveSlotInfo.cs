using System;

namespace Infrastructure.SaveSystem
{
    public readonly struct SaveSlotInfo
    {
        public readonly int Index;
        public readonly string Path;
        public readonly DateTime CreatedAt;
        public readonly DateTime UpdatedAt;

        public SaveSlotInfo(int index, string path, DateTime createdAt, DateTime updatedAt)
        {
            Index = index;
            Path = path;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }
    }
}