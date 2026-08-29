#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TestMenuSceneBuilder
{
    private const string ScenePath = "Assets/_Scripts/Anton/TestMenu.unity";
    private static readonly Color ButtonColor = new Color32(145, 145, 145, 255);

    [MenuItem("Tools/Anton/Rebuild Test Menu")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var canvasObject = new GameObject("Test Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var background = CreateUiObject("Background", canvasObject.transform, typeof(Image));
        Stretch(background.GetComponent<RectTransform>());
        background.GetComponent<Image>().color = new Color32(45, 45, 45, 255);

        var controllerObject = new GameObject("Test Menu Controller", typeof(TestMenuController));
        var controller = controllerObject.GetComponent<TestMenuController>();

        var main = CreatePanel("Main Menu", background.transform);
        AddTitle(main, "ГЛАВНОЕ МЕНЮ");
        AddButton(main, "Старт", 150, controller.StartGame);
        AddButton(main, "Выбор ячейки сохранения", 50, controller.ShowSaveSlots);
        AddButton(main, "Авторы", -50, controller.ShowAuthors);
        AddButton(main, "Настройки", -150, controller.ShowSettings);
        AddButton(main, "Выход", -250, controller.ExitGame);
        AddLabel(main, "Версия игры: " + Application.version, new Vector2(0, -355), 26);

        var saves = CreatePanel("Save Slots", background.transform);
        AddTitle(saves, "ВЫБОР ЯЧЕЙКИ СОХРАНЕНИЯ");
        AddIntButton(saves, "Ячейка 1", 120, controller.SelectSaveSlot, 1);
        AddIntButton(saves, "Ячейка 2", 20, controller.SelectSaveSlot, 2);
        AddIntButton(saves, "Ячейка 3", -80, controller.SelectSaveSlot, 3);
        AddButton(saves, "Вернуться", -230, controller.ShowMain);

        var authors = CreatePanel("Authors", background.transform);
        AddTitle(authors, "АВТОРЫ");
        AddLabel(authors, "Список авторов\n(аватары и контактные данные будут добавлены позже)", new Vector2(0, 80), 32);
        AddButton(authors, "Вернуться", -230, controller.ShowMain);

        var settings = CreatePanel("Settings", background.transform);
        AddTitle(settings, "НАСТРОЙКИ");
        AddButton(settings, "Звук", 150, controller.ShowSound);
        AddButton(settings, "Графика", 50, controller.ShowGraphics);
        AddPlaceholder(settings, "Чувствительность мыши", -50, controller);
        AddPlaceholder(settings, "Регулятор ФПС", -150, controller);
        AddButton(settings, "Вернуться", -280, controller.ShowMain);

        var sound = CreatePanel("Sound", background.transform);
        AddTitle(sound, "ЗВУК");
        AddPlaceholder(sound, "Громкость общая", 120, controller);
        AddPlaceholder(sound, "Громкость музыки", 20, controller);
        AddPlaceholder(sound, "Громкость звуков", -80, controller);
        AddButton(sound, "Вернуться", -230, controller.ShowSettings);

        var graphics = CreatePanel("Graphics", background.transform);
        AddTitle(graphics, "ГРАФИКА");
        AddPlaceholder(graphics, "Настройки яркости экрана", 120, controller);
        AddPlaceholder(graphics, "Настройки разрешения экрана", 20, controller);
        AddPlaceholder(graphics, "Качество графики", -80, controller);
        AddButton(graphics, "Вернуться", -230, controller.ShowSettings);

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("mainPanel").objectReferenceValue = main.gameObject;
        serialized.FindProperty("savePanel").objectReferenceValue = saves.gameObject;
        serialized.FindProperty("authorsPanel").objectReferenceValue = authors.gameObject;
        serialized.FindProperty("settingsPanel").objectReferenceValue = settings.gameObject;
        serialized.FindProperty("soundPanel").objectReferenceValue = sound.gameObject;
        serialized.FindProperty("graphicsPanel").objectReferenceValue = graphics.gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        saves.gameObject.SetActive(false);
        authors.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);
        sound.gameObject.SetActive(false);
        graphics.gameObject.SetActive(false);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Test menu rebuilt: " + ScenePath);
    }

    private static RectTransform CreatePanel(string name, Transform parent)
    {
        var panel = CreateUiObject(name, parent);
        var rect = panel.GetComponent<RectTransform>();
        Stretch(rect);
        return rect;
    }

    private static void AddTitle(Transform parent, string text) => AddLabel(parent, text, new Vector2(0, 330), 48);

    private static void AddPlaceholder(Transform parent, string text, float y, TestMenuController controller)
    {
        var button = CreateButton(parent, text, y);
        UnityEventTools.AddStringPersistentListener(button.onClick, controller.ButtonPlaceholder, text);
    }

    private static void AddButton(Transform parent, string text, float y, UnityEngine.Events.UnityAction action)
    {
        var button = CreateButton(parent, text, y);
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    private static void AddIntButton(Transform parent, string text, float y,
        UnityEngine.Events.UnityAction<int> action, int value)
    {
        var button = CreateButton(parent, text, y);
        UnityEventTools.AddIntPersistentListener(button.onClick, action, value);
    }

    private static Button CreateButton(Transform parent, string text, float y)
    {
        var buttonObject = CreateUiObject(text, parent, typeof(Image), typeof(Button));
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520, 76);
        rect.anchoredPosition = new Vector2(0, y);
        buttonObject.GetComponent<Image>().color = ButtonColor;
        var button = buttonObject.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color32(175, 175, 175, 255);
        colors.pressedColor = new Color32(110, 110, 110, 255);
        button.colors = colors;
        AddLabel(buttonObject.transform, text, Vector2.zero, 30, true);
        return button;
    }

    private static void AddLabel(Transform parent, string value, Vector2 position, int fontSize, bool stretch = false)
    {
        var labelObject = CreateUiObject("Text", parent, typeof(Text));
        var rect = labelObject.GetComponent<RectTransform>();
        if (stretch)
            Stretch(rect);
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1000, 160);
            rect.anchoredPosition = position;
        }
        var label = labelObject.GetComponent<Text>();
        label.text = value;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        foreach (var component in components)
            gameObject.AddComponent(component);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
#endif
