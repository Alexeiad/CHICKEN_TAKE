using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A grey-box layout: art and story content live in BuildingMenuConfig.
// Uses a fixed design canvas that fits inside any screen aspect ratio.
public sealed class BuildingMenuView : MonoBehaviour
{
    [Serializable]
    public sealed class Section
    {
        public GameObject panel;
        public int tab;
        public bool story;
    }

    private BuildingMenuConfig config;
    [SerializeField] private BuildingKind kind;
    private MenuLanguage language;
    private Action close;
    [SerializeField] private RectTransform page;
    [SerializeField] private RectTransform frame;
    [SerializeField] private RawImage background;
    [SerializeField] private List<Section> sections = new List<Section>();
    [SerializeField] private RawImage tractorPicture;
    [SerializeField] private GameObject tractorPlaceholder;
    [SerializeField] private List<RawImage> storyPictures = new List<RawImage>();
    private readonly Dictionary<string, BuildingMenuConfig.LocalizedCopy> copies = new Dictionary<string, BuildingMenuConfig.LocalizedCopy>();
    private int tab;
    private bool storyOpen;
    private readonly BuildingQuestMusic music = new BuildingQuestMusic();
    private static readonly Color Panel = new Color(.10f, .115f, .13f, .97f);
    private static readonly Color Tile = new Color(.20f, .22f, .24f, .98f);
    private static readonly Color Accent = new Color(.76f, .81f, .64f);

    public bool StoryOpen => storyOpen;

    public static BuildingMenuView Create(BuildingMenuConfig config, BuildingKind kind, Action close)
    {
        BuildingMenuView prefab = kind == BuildingKind.Barn ? config.barnMenu
            : kind == BuildingKind.Hangar ? config.hangarMenu : config.shopMenu;
        if (prefab == null) throw new InvalidOperationException("Assign the building menu prefabs in BuildingMenus.asset.");
        BuildingMenuView view = Instantiate(prefab);
        view.config = config;
        view.close = close;
        view.language = BuildingMenuConfig.SavedLanguage;
        view.tab = 0;
        view.storyOpen = false;
        view.background.texture = kind == BuildingKind.Barn ? config.barnBackground
            : kind == BuildingKind.Hangar ? config.hangarBackground : config.shopBackground;
        if (view.tractorPicture != null)
        {
            view.tractorPicture.texture = config.tractorImage;
            view.tractorPicture.transform.parent.gameObject.SetActive(config.tractorImage != null);
        }
        if (view.tractorPlaceholder != null) view.tractorPlaceholder.SetActive(config.tractorImage == null);
        foreach (RawImage picture in view.storyPictures) picture.texture = config.alienStoryImage;
        foreach (RawImage picture in view.GetComponentsInChildren<RawImage>(true))
        {
            var fitter = picture.GetComponent<AspectRatioFitter>();
            if (fitter != null && picture.texture != null) fitter.aspectRatio = (float)picture.texture.width / picture.texture.height;
        }
        if (config.playerData != null) config.playerData.OnDataChanged += view.RefreshData;
        view.ShowPage();
        return view;
    }

#if UNITY_EDITOR
    // Editor-only factory for initial prefab creation. Gameplay never rebuilds the layout.
    public static BuildingMenuView BuildPrefab(BuildingMenuConfig config, BuildingKind kind)
    {
        var root = new GameObject("Building Menu - " + kind, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var view = root.AddComponent<BuildingMenuView>();
        view.config = config;
        view.kind = kind;
        view.language = MenuLanguage.Russian;
        try { view.Build(); }
        catch { DestroyImmediate(root); throw; }
        int count = kind == BuildingKind.Barn ? 1 : kind == BuildingKind.Hangar ? 2 : 3;
        for (int i = 0; i < count; i++) view.BakeSection(i, false);
        if (kind == BuildingKind.Shop) view.BakeSection(0, true);
        view.tab = 0;
        view.ShowPage();
        return view;
    }

    private void BakeSection(int selectedTab, bool isStory)
    {
        RectTransform holder = page;
        string name = isStory ? "AlienStoryPanel" : kind == BuildingKind.Barn ? "StatisticsPanel"
            : kind == BuildingKind.Hangar ? selectedTab == 0 ? "TractorsPanel" : "ModdingPanel"
            : selectedTab == 0 ? "SpecialQuestsPanel" : selectedTab == 1 ? "ItemsPanel" : "ConsumablesPanel";
        page = Rect(name, holder, 0, 0, 1600, 710);
        tab = selectedTab;
        if (isStory) BuildStory();
        else if (kind == BuildingKind.Barn) Barn();
        else if (kind == BuildingKind.Hangar) Hangar();
        else Shop();
        sections.Add(new Section { panel = page.gameObject, tab = selectedTab, story = isStory });
        page = holder;
    }
#endif

    private string T(string ru, string en, string de)
    {
        string value = language == MenuLanguage.English ? en : language == MenuLanguage.German ? de : ru;
        copies[value] = new BuildingMenuConfig.LocalizedCopy { russian = ru, english = en, german = de };
        return value;
    }

    private void Build()
    {
        var blocker = new GameObject("Modal backdrop", typeof(RectTransform), typeof(Image));
        var full = (RectTransform)blocker.transform;
        full.SetParent(transform, false);
        full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
        full.offsetMin = full.offsetMax = Vector2.zero;
        blocker.GetComponent<Image>().color = Color.black;
        frame = Rect("Design canvas", transform, 0, 0, 1600, 900);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
        frame.anchoredPosition = Vector2.zero;
        background = Rect("Building art", frame, 0, 0, 1600, 900).gameObject.AddComponent<RawImage>();
        background.texture = kind == BuildingKind.Barn ? config.barnBackground
            : kind == BuildingKind.Hangar ? config.hangarBackground : config.shopBackground;
        background.color = background.texture == null ? new Color(.15f, .17f, .19f) : Color.white;
        background.raycastTarget = false;
        Box(frame, 0, 0, 1600, 900, new Color(0, 0, 0, .35f));
        Box(frame, 30, 25, 1540, 110, Panel);
        Button(frame, T("Назад", "Back", "Zurück"), 50, 48, 165, 62, BuildingMenuButton.Command.Back);
        Label(frame, kind == BuildingKind.Barn ? T("АМБАР", "BARN", "SCHEUNE")
            : kind == BuildingKind.Hangar ? T("АНГАР", "HANGAR", "HANGAR") : T("МАГАЗИН", "SHOP", "LADEN"),
            280, 37, 1040, 45, 36, TextAlignmentOptions.Center);
        Label(frame, T("Ферма / здания", "Farm / buildings", "Bauernhof / Gebäude"),
            280, 83, 1040, 30, 20, TextAlignmentOptions.Center);
        Button(frame, T("Закрыть ×", "Close ×", "Schließen ×"), 1370, 48, 180, 62, BuildingMenuButton.Command.Close);
        page = Rect("Page", frame, 0, 150, 1600, 710);
        Label(frame, T("ESC — назад / закрыть", "ESC — back / close", "ESC — zurück / schließen"),
            50, 863, 1500, 27, 18, TextAlignmentOptions.Right);
    }

    private void RefreshData()
    {
        foreach (BuildingMenuBinding binding in GetComponentsInChildren<BuildingMenuBinding>(true))
            binding.Apply(config, language);
    }

    private void ShowPage()
    {
        foreach (Section section in sections)
            section.panel.SetActive(section.story == storyOpen && (storyOpen || section.tab == tab));
        RefreshData();
    }

    public void SelectTab(int selected) { tab = selected; ShowPage(); }
    public void CloseWindow() => close?.Invoke();

    public void Back()
    {
        if (storyOpen)
        {
            music.Stop(); storyOpen = false; ShowPage();
        }
        else if (tab != 0) SelectTab(0);
        else close?.Invoke();
    }

    private void Barn()
    {
        // The supplied barn art has baked-in labels; cover them between the panels too.
        Box(page, 45, 5, 1510, 685, Panel);
        Box(page, 45, 5, 735, 685, Panel);
        Box(page, 805, 5, 750, 685, Panel);
        Label(page, T("СТАТИСТИКА", "STATISTICS", "STATISTIK"), 75, 25, 670, 48, 30);
        var data = config.playerData;
        string Stat(int value) => data == null ? "—" : value.ToString("N0");
        string[] names = { T("Кур собрано", "Chickens collected", "Hühner gesammelt"),
            T("Кур продано", "Chickens sold", "Hühner verkauft"), T("Кур сейчас", "Chickens carried", "Hühner dabei"),
            T("Монет заработано", "Coins earned", "Münzen verdient"), T("Баланс", "Balance", "Guthaben"),
            T("Матчей сыграно", "Matches played", "Spiele gespielt"), T("Побед", "Wins", "Siege") };
        string[] values = { Stat(data != null ? data.TotalCollected : 0), Stat(data != null ? data.TotalSold : 0),
            Stat(data != null ? data.Chicken : 0), Stat(data != null ? data.TotalEarned : 0), Stat(data != null ? data.Cash : 0),
            data != null && data.HasMatchHistory ? Stat(data.MatchesPlayed) : "—",
            data != null && data.HasMatchHistory ? Stat(data.MatchesWon) : "—" };
        for (int i = 0; i < names.Length; i++)
        {
            Box(page, 65, 92 + i * 76, 695, 66, Tile);
            Label(page, names[i], 85, 102 + i * 76, 455, 44, 26);
            Label(page, values[i], 550, 102 + i * 76, 185, 44, 30, TextAlignmentOptions.Right, Accent).GetComponent<BuildingMenuBinding>().value = (BuildingMenuBinding.Value)((int)BuildingMenuBinding.Value.Collected + i);
        }
        Label(page, T("ДОСТИЖЕНИЯ", "ACHIEVEMENTS", "ERFOLGE"), 835, 25, 675, 48, 30);
        int collected = data != null ? data.TotalCollected : 0;
        Progress(T("Собери 100 кур", "Collect 100 chickens", "Sammle 100 Hühner"), collected, 100, 90);
        Progress(T("Собери 500 кур", "Collect 500 chickens", "Sammle 500 Hühner"), collected, 500, 205);
        Progress(T("Собери 1000 кур", "Collect 1,000 chickens", "Sammle 1.000 Hühner"), collected, 1000, 320);
        Progress(T("Сыграй 50 матчей", "Play 50 matches", "Spiele 50 Partien"), data != null ? data.MatchesPlayed : 0, 50, 435, BuildingMenuBinding.Value.MatchProgress);
        Progress(T("Выиграй 10 матчей", "Win 10 matches", "Gewinne 10 Partien"), data != null ? data.MatchesWon : 0, 10, 550, BuildingMenuBinding.Value.WinProgress);
    }

    private void Progress(string title, int value, int goal, float y, BuildingMenuBinding.Value source = BuildingMenuBinding.Value.CollectionProgress)
    {
        Box(page, 825, y, 710, 102, Tile);
        Label(page, title, 845, y + 8, 500, 36, 25);
        var counter = Label(page, value >= goal ? T("Готово", "Done", "Erledigt") : $"{value:N0} / {goal:N0}",
            1325, y + 8, 190, 36, 22, TextAlignmentOptions.Right, Accent);
        var track = Box(page, 845, y + 62, 670, 14, new Color(.08f, .09f, .10f));
        var fill = Box(track, 0, 0, 670, 14, Accent);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Clamp01((float)value / goal), 1);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        var binding = counter.GetComponent<BuildingMenuBinding>();
        binding.value = source; binding.goal = goal; binding.progressFill = fill;
    }

    private void Hangar()
    {
        Button(page, T("Тракторы", "Tractors", "Traktoren"), 45, 0, 745, 58, BuildingMenuButton.Command.Tab, true, tab == 0, 0);
        Button(page, T("Моддинг", "Modding", "Modding"), 805, 0, 750, 58, BuildingMenuButton.Command.Tab, true, tab == 1, 1);
        if (tab == 1) { ComingSoon(); return; }
        Box(page, 45, 76, 950, 610, Panel);
        Box(page, 1015, 76, 540, 610, Panel);
        Label(page, config.tractorName.Get(language), 80, 94, 880, 58, 36, TextAlignmentOptions.Center).GetComponent<BuildingMenuBinding>().value = BuildingMenuBinding.Value.TractorName;
        tractorPicture = Picture(page, config.tractorImage, 100, 165, 840, 370);
        tractorPicture.transform.parent.gameObject.SetActive(config.tractorImage != null);
        tractorPlaceholder = Rect("Tractor placeholder", page, 0, 0, 1600, 710).gameObject;
        {
            // Neutral tractor silhouette until a separate UI illustration is supplied.
            Box(tractorPlaceholder.transform, 310, 245, 160, 185, new Color(.42f, .44f, .46f));
            Box(tractorPlaceholder.transform, 328, 260, 124, 88, new Color(.18f, .21f, .24f));
            Box(tractorPlaceholder.transform, 465, 330, 240, 100, new Color(.52f, .54f, .56f));
            Box(tractorPlaceholder.transform, 640, 245, 22, 85, new Color(.38f, .40f, .42f));
            Box(tractorPlaceholder.transform, 270, 405, 160, 125, new Color(.08f, .09f, .10f));
            Box(tractorPlaceholder.transform, 635, 418, 100, 112, new Color(.08f, .09f, .10f));
            Label(tractorPlaceholder.transform, "01", 465, 340, 180, 60, 42, TextAlignmentOptions.Center);
        }
        tractorPlaceholder.SetActive(config.tractorImage == null);
        Label(page, T("ВЫБРАН", "SELECTED", "AUSGEWÄHLT"), 260, 570, 520, 60, 30, TextAlignmentOptions.Center, Accent);
        Label(page, config.tractorDescription.Get(language), 1045, 110, 480, 140, 28).GetComponent<BuildingMenuBinding>().value = BuildingMenuBinding.Value.TractorDescription;
        string[] specs = { T("Скорость", "Speed", "Geschwindigkeit"), T("Вместимость", "Capacity", "Kapazität"),
            T("Ускорение", "Acceleration", "Beschleunigung"), T("Управляемость", "Handling", "Lenkung") };
        for (int i = 0; i < specs.Length; i++)
        {
            Label(page, specs[i], 1045, 270 + i * 59, 380, 46, 24);
            Label(page, "—", 1430, 270 + i * 59, 80, 46, 24, TextAlignmentOptions.Right);
        }
        Button(page, T("Перейти к моддингу", "Open modding", "Zum Modding"), 1045, 568, 480, 68, BuildingMenuButton.Command.Tab, argument: 1);
    }

    private void ComingSoon()
    {
        Box(page, 45, 76, 1510, 610, Panel);
        Label(page, T("В РАЗРАБОТКЕ", "IN DEVELOPMENT", "IN ENTWICKLUNG"), 200, 220, 1200, 90, 52, TextAlignmentOptions.Center);
        Label(page, T("Этот раздел появится позже", "This section is coming later", "Dieser Bereich kommt später"),
            200, 330, 1200, 70, 30, TextAlignmentOptions.Center);
        Button(page, T("Назад", "Back", "Zurück"), 610, 450, 380, 72, BuildingMenuButton.Command.Tab);
    }

    private void Shop()
    {
        string[] tabs = { T("Спецзадания", "Special quests", "Spezialaufträge"), T("Предметы", "Items", "Gegenstände"),
            T("Расходники", "Consumables", "Verbrauchsartikel") };
        for (int i = 0; i < tabs.Length; i++)
        {
            int selected = i;
            Button(page, tabs[i], 45 + i * 510, 0, 490, 58, BuildingMenuButton.Command.Tab, true, tab == i, selected);
        }
        if (tab != 0) { ComingSoon(); return; }
        string[] titles = { T("Инопланетная курица", "Alien chicken", "Alien-Huhn"), T("Курица из Матрицы", "Matrix chicken", "Matrix-Huhn"),
            T("Золотая Хохлатка", "Golden chicken", "Goldenes Huhn"), T("Курица-зомби", "Zombie chicken", "Zombie-Huhn"),
            T("Курица-ниндзя", "Ninja chicken", "Ninja-Huhn"), "???" };
        for (int i = 0; i < titles.Length; i++)
        {
            float x = 45 + i % 3 * 510; float y = 80 + i / 3 * 307;
            var card = Box(page, x, y, 490, 290, Panel);
            Label(card, titles[i], 22, 14, 446, 47, 28, TextAlignmentOptions.Center);
            if (i == 0)
            {
                storyPictures.Add(Picture(card, config.alienStoryImage, 24, 72, 150, 120));
                Label(card, T("Необычный гость на ферме.\nУзнай, с чего всё началось.",
                    "An unusual visitor on the farm.\nDiscover how it all began.", "Ein seltsamer Gast auf dem Hof.\nErfahre, wie alles begann."),
                    193, 70, 272, 132, 23);
                // Both the card surface and the visible button open the same story.
                var clickable = card.gameObject.AddComponent<Button>();
                clickable.targetGraphic = card.GetComponent<Image>();
                var action = card.gameObject.AddComponent<BuildingMenuButton>(); action.menu = this; action.command = BuildingMenuButton.Command.Story;
            }
            else Label(card, T("Откроется позже", "Available later", "Später verfügbar"), 25, 88, 440, 100, 26, TextAlignmentOptions.Center);
            Button(card, i == 0 ? T("Смотреть историю", "View story", "Geschichte ansehen") : T("Недоступно", "Locked", "Gesperrt"),
                22, 217, 446, 54, BuildingMenuButton.Command.Story, i == 0, i == 0);
        }
    }

    public void OpenStory()
    {
        if (storyOpen || kind != BuildingKind.Shop) return;
        storyOpen = true;
        ShowPage();
        foreach (ScrollRect scroll in GetComponentsInChildren<ScrollRect>()) scroll.verticalNormalizedPosition = 1;
        music.Play(config);
    }

    private void BuildStory()
    {
        Box(page, 45, 0, 1510, 695, Panel);
        storyPictures.Add(Picture(page, config.alienStoryImage, 65, 20, 715, 655));
        Label(page, T("ИНОПЛАНЕТНАЯ КУРИЦА", "ALIEN CHICKEN", "ALIEN-HUHN"), 815, 28, 700, 80, 34);
        var viewport = Box(page, 815, 128, 700, 437, new Color(0, 0, 0, 0));
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35;
        var body = Label(viewport, config.alienStory.Get(language), 0, 0, 670, 437, 28);
        body.GetComponent<BuildingMenuBinding>().value = BuildingMenuBinding.Value.Story; body.enableAutoSizing = false;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.raycastTarget = true;
        body.ForceMeshUpdate();
        body.rectTransform.sizeDelta = new Vector2(670, Mathf.Max(437, body.preferredHeight + 15));
        scroll.content = body.rectTransform;
        scroll.viewport = viewport;
        Button(page, T("Вернуться в магазин", "Return to shop", "Zurück zum Laden"), 815, 603, 700, 66, BuildingMenuButton.Command.Back);
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform Box(Transform parent, float x, float y, float w, float h, Color color)
    {
        var rect = Rect("Panel", parent, x, y, w, h);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private TextMeshProUGUI Label(Transform parent, string text, float x, float y, float w, float h, float size,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, Color? color = null)
    {
        var label = Rect(text, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = config.font != null ? config.font : TMP_Settings.defaultFontAsset;
        label.text = text; label.fontSize = size;
        label.enableAutoSizing = true; label.fontSizeMin = size * .72f; label.fontSizeMax = size;
        label.alignment = align; label.color = color ?? Color.white; label.raycastTarget = false;
        var binding = label.gameObject.AddComponent<BuildingMenuBinding>();
        binding.label = label;
        binding.translation = copies.TryGetValue(text, out var copy) ? copy
            : new BuildingMenuConfig.LocalizedCopy { russian = text, english = text, german = text };
        return label;
    }

    private Button Button(Transform parent, string text, float x, float y, float w, float h, BuildingMenuButton.Command command,
        bool enabled = true, bool selected = false, int argument = 0)
    {
        var rect = Box(parent, x, y, w, h, selected ? Accent : Tile);
        rect.name = text;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.interactable = enabled;
        var action = rect.gameObject.AddComponent<BuildingMenuButton>(); action.menu = this; action.command = command; action.tab = argument;
        Label(rect, text, 12, 4, w - 24, h - 8, 24, TextAlignmentOptions.Center,
            selected ? new Color(.1f, .12f, .1f) : enabled ? Color.white : new Color(.55f, .57f, .59f));
        return button;
    }

    private static RawImage Picture(Transform parent, Texture texture, float x, float y, float w, float h)
    {
        var container = Rect("Illustration frame", parent, x, y, w, h);
        var rect = Rect("Illustration", container, 0, 0, w, h);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(.5f, .5f);
        var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture; image.raycastTarget = false;
        var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = texture != null ? (float)texture.width / texture.height : 1;
        return image;
    }
    private void OnDisable() => music.Stop();
    private void OnDestroy()
    {
        music.Stop();
        if (config != null && config.playerData != null) config.playerData.OnDataChanged -= RefreshData;
    }
}
