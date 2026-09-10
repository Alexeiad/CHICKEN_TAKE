using System;
using System.Collections.Generic;
using UnityEngine;

public enum MenuLanguage { Russian, English, German }

[CreateAssetMenu(fileName = "MenuTranslations", menuName = "Localization/Menu Translations")]
public sealed class MenuTranslationsSO : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string key;
        [TextArea] public string russian;
        [TextArea] public string english;
        [TextArea] public string german;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    private Dictionary<string, Entry> lookup;

    private void OnEnable() => lookup = null;
    private void OnValidate() => lookup = null;

    public string GetText(string key, MenuLanguage language)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (lookup == null)
        {
            lookup = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (Entry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                if (lookup.ContainsKey(entry.key))
                    Debug.LogWarning($"Duplicate translation key: {entry.key}", this);
                else lookup.Add(entry.key, entry);
            }
        }

        if (!lookup.TryGetValue(key, out Entry value)) return key;
        string text = language == MenuLanguage.English ? value.english
            : language == MenuLanguage.German ? value.german : value.russian;
        return !string.IsNullOrEmpty(text) ? text
            : !string.IsNullOrEmpty(value.russian) ? value.russian : key;
    }
}
