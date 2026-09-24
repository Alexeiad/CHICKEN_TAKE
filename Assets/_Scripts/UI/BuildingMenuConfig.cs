using System;
using TMPro;
using UnityEngine;

public enum BuildingKind { Barn, Hangar, Shop }

[CreateAssetMenu(fileName = "BuildingMenus", menuName = "Game/Building Menus")]
public sealed class BuildingMenuConfig : ScriptableObject
{
    [Serializable]
    public class LocalizedCopy
    {
        [TextArea(2, 8)] public string russian;
        [TextArea(2, 8)] public string english;
        [TextArea(2, 8)] public string german;
        public string Get(MenuLanguage language) => language == MenuLanguage.English && !string.IsNullOrEmpty(english)
            ? english : language == MenuLanguage.German && !string.IsNullOrEmpty(german) ? german : russian;
    }

    public TMP_FontAsset font;
    public PlayerDataSO playerData;
    [Header("Editable window prefabs")]
    public BuildingMenuView barnMenu;
    public BuildingMenuView hangarMenu;
    public BuildingMenuView shopMenu;
    public Texture2D barnBackground;
    public Texture2D hangarBackground;
    public Texture2D shopBackground;
    public Texture2D tractorImage;
    public LocalizedCopy tractorName = new LocalizedCopy { russian = "Куромобиль-3000", english = "Chickenmobile 3000", german = "Hühnermobil 3000" };
    [Tooltip("Display copy only. Does not change vehicle physics.")]
    public LocalizedCopy tractorDescription = new LocalizedCopy { russian = "Стартовый трактор\nДоступен с начала игры", english = "Starter tractor\nAvailable from the start", german = "Starttraktor\nVon Anfang an verfügbar" };
    public Texture2D alienStoryImage;
    public LocalizedCopy alienStory = new LocalizedCopy();
    [Tooltip("An existing 2D FMOD event; replace when final music is approved.")]
    public string alienMusicEvent = "event:/Menu_Inner";
    public string backgroundMusicEvent = "event:/Menu&Base_Music";

    public static MenuLanguage SavedLanguage
    {
        get
        {
            string value = PlayerPrefs.GetString("Menu.Language", "Russian");
            return Enum.TryParse(value, out MenuLanguage language) && Enum.IsDefined(typeof(MenuLanguage), language)
                ? language : MenuLanguage.Russian;
        }
    }
}
