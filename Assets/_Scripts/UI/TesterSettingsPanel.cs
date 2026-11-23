using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Окно для тестировщиков: показывает поля и свойства GameSettingsSO, VehiclesSettings и PlayerDataSO,
/// меняет значения "вживую", сохраняет их и умеет копировать всё в буфер (JSON).
/// Окно можно перетаскивать за шапку, закрывать кнопкой "✕" и переключать язык (RU / EN).
/// </summary>
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public class TesterSettingsPanel : MonoBehaviour
{
    public enum PanelLanguage { Ru, En }

    /// <summary>true, пока окно открыто — можно проверять в камере/управлении игрока.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Вызывается после любого изменения значения.</summary>
    public event Action SettingsChanged;

    [Header("Что редактируем")]
    [SerializeField] private GameSettingsSO gameSettings;
    [SerializeField] private VehiclesSettings vehicleSettings;
    [SerializeField] private PlayerDataSO playerData;

    [Header("Окно")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private bool startOpened = false;
    [SerializeField] private PanelLanguage defaultLanguage = PanelLanguage.Ru;
    [SerializeField] private float windowWidth = 800f;
    [SerializeField] private float windowHeight = 680f;
    [SerializeField] private bool clampToScreen = true;
    [Tooltip("Показывать курсор и разблокировать его, пока окно открыто")]
    [SerializeField] private bool unlockCursorWhenOpen = true;
    [Tooltip("Ставить игру на паузу (timeScale = 0), пока окно открыто")]
    [SerializeField] private bool pauseGameWhenOpen = false;

    [Header("Сохранение")]
    [SerializeField] private bool saveBetweenSessions = true;
    [Tooltip("Поменяй (v1 -> v2), чтобы у всех тестеров сбросились старые сохранённые значения")]
    [SerializeField] private string saveVersion = "v1";

    // ---------- внутренние типы ----------
    private sealed class Acc
    {
        public Func<object> Get;
        public Action<object> Set;
    }

    private sealed class Source
    {
        public string SectionKey;
        public ScriptableObject So;
    }

    private sealed class Tab
    {
        public string Title;
        public Acc Acc;
        public Type Type;
        public Image Bg;
    }

    private sealed class TitleDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform _target;
        private Canvas _canvas;
        private bool _clamp;

        public void Init(RectTransform target, Canvas canvas, bool clamp)
        {
            _target = target;
            _canvas = canvas;
            _clamp = clamp;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_target != null) _target.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_target == null) return;
            float scale = _canvas != null && _canvas.scaleFactor > 0.0001f ? _canvas.scaleFactor : 1f;
            _target.anchoredPosition += eventData.delta / scale;
            if (_clamp) ClampToParent();
        }

        private void ClampToParent()
        {
            var parent = _target.parent as RectTransform;
            if (parent == null) return;

            Vector2 parentSize = parent.rect.size;
            Vector2 winSize = _target.rect.size;
            Vector2 pivot = _target.pivot;

            float minX = -parentSize.x * 0.5f + winSize.x * pivot.x;
            float maxX = parentSize.x * 0.5f - winSize.x * (1f - pivot.x);
            float minY = -parentSize.y * 0.5f + winSize.y * pivot.y;
            float maxY = parentSize.y * 0.5f - winSize.y * (1f - pivot.y);

            if (minX > maxX) minX = maxX = 0f;
            if (minY > maxY) minY = maxY = 0f;

            Vector2 pos = _target.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            _target.anchoredPosition = pos;
        }
    }

    // ---------- состояние ----------
    private const float TitleH = 38f;
    private const float BottomH = 52f;
    private const float TabsW = 170f;
    private const string LangPrefKey = "TesterPanel_Lang";

    private static readonly Color ColText = new Color(0.92f, 0.93f, 0.95f);
    private static readonly Color ColHint = new Color(0.55f, 0.58f, 0.64f);
    private static readonly Color ColHeader = new Color(0.55f, 0.8f, 1f);
    private static readonly Color ColInput = new Color(0.16f, 0.17f, 0.21f);
    private static readonly Color ColBtn = new Color(0.22f, 0.24f, 0.3f);
    private static readonly Color ColAccent = new Color(0.2f, 0.45f, 0.8f);
    private static readonly Color ColClose = new Color(0.75f, 0.2f, 0.22f);
    private static readonly Color ColOn = new Color(0.2f, 0.55f, 0.3f);
    private static readonly Color ColOff = new Color(0.35f, 0.25f, 0.25f);

    /// <summary>Точные переводы для конкретных полей/заголовков (приоритет выше автоперевода).</summary>
    private static readonly Dictionary<string, (string ru, string en)> FieldTranslations =
        new Dictionary<string, (string ru, string en)>(StringComparer.OrdinalIgnoreCase)
        {
            { "fov", ("Угол обзора (FOV)", "Field of View (FOV)") },
            { "fieldOfView", ("Угол обзора (FOV)", "Field of View (FOV)") },
            { "totalCollected", ("Всего собрано", "Total Collected") },
            { "totalSold", ("Всего продано", "Total Sold") },
            { "totalEarned", ("Всего заработано", "Total Earned") },
            { "matchesPlayed", ("Сыграно матчей", "Matches Played") },
            { "matchesWon", ("Выиграно матчей", "Matches Won") },
            { "hasMatchHistory", ("Есть история матчей", "Has Match History") },
            { "cash", ("Деньги", "Cash") },
            { "chicken", ("Курицы", "Chicken") },
        };

    /// <summary>Словарь частых слов в именах переменных для автоперевода любых составных имён.</summary>
    private static readonly Dictionary<string, (string ru, string en)> WordMap =
        new Dictionary<string, (string ru, string en)>(StringComparer.OrdinalIgnoreCase)
        {
            { "camera", ("камера", "Camera") },
            { "cam", ("камера", "Cam") },
            { "player", ("игрок", "Player") },
            { "character", ("персонаж", "Character") },
            { "vehicle", ("машина", "Vehicle") },
            { "vehicles", ("машины", "Vehicles") },
            { "car", ("авто", "Car") },
            { "cars", ("авто", "Cars") },
            { "wheel", ("колесо", "Wheel") },
            { "wheels", ("колёса", "Wheels") },
            { "tire", ("шина", "Tire") },
            { "tires", ("шины", "Tires") },
            { "suspension", ("подвеска", "Suspension") },
            { "engine", ("двигатель", "Engine") },
            { "motor", ("мотор", "Motor") },
            { "gear", ("передача", "Gear") },
            { "gears", ("передачи", "Gears") },
            { "drift", ("дрифт", "Drift") },
            { "nitro", ("нитро", "Nitro") },
            { "boost", ("ускорение", "Boost") },
            { "brake", ("тормоз", "Brake") },
            { "brakes", ("тормоза", "Brakes") },
            { "handbrake", ("ручник", "Handbrake") },
            { "steer", ("руль", "Steer") },
            { "steering", ("руление", "Steering") },
            { "turn", ("поворот", "Turn") },
            { "rotate", ("вращение", "Rotate") },
            { "rotation", ("вращение", "Rotation") },
            { "move", ("движение", "Move") },
            { "movement", ("движение", "Movement") },
            { "walk", ("ходьба", "Walk") },
            { "run", ("бег", "Run") },
            { "sprint", ("спринт", "Sprint") },
            { "jump", ("прыжок", "Jump") },
            { "crouch", ("присед", "Crouch") },
            { "dash", ("рывок", "Dash") },
            { "gravity", ("гравитация", "Gravity") },
            { "ground", ("земля", "Ground") },
            { "grounded", ("на земле", "Grounded") },
            { "air", ("воздух", "Air") },
            { "speed", ("скорость", "Speed") },
            { "velocity", ("скорость", "Velocity") },
            { "accel", ("разгон", "Accel") },
            { "acceleration", ("ускорение", "Acceleration") },
            { "decel", ("торможение", "Decel") },
            { "deceleration", ("замедление", "Deceleration") },
            { "force", ("сила", "Force") },
            { "torque", ("момент", "Torque") },
            { "power", ("мощность", "Power") },
            { "mass", ("масса", "Mass") },
            { "weight", ("вес", "Weight") },
            { "drag", ("сопротивление", "Drag") },
            { "friction", ("трение", "Friction") },
            { "damping", ("демпфирование", "Damping") },
            { "stiffness", ("жёсткость", "Stiffness") },
            { "spring", ("пружина", "Spring") },
            { "damper", ("амортизатор", "Damper") },
            { "min", ("мин.", "Min") },
            { "max", ("макс.", "Max") },
            { "minimum", ("минимум", "Minimum") },
            { "maximum", ("максимум", "Maximum") },
            { "limit", ("лимит", "Limit") },
            { "clamp", ("ограничение", "Clamp") },
            { "radius", ("радиус", "Radius") },
            { "distance", ("дистанция", "Distance") },
            { "range", ("дистанция", "Range") },
            { "height", ("высота", "Height") },
            { "width", ("ширина", "Width") },
            { "length", ("длина", "Length") },
            { "size", ("размер", "Size") },
            { "scale", ("масштаб", "Scale") },
            { "offset", ("смещение", "Offset") },
            { "position", ("позиция", "Position") },
            { "pos", ("позиция", "Pos") },
            { "angle", ("угол", "Angle") },
            { "pitch", ("тангаж", "Pitch") },
            { "yaw", ("рыскание", "Yaw") },
            { "roll", ("крен", "Roll") },
            { "sensitivity", ("чувствительность", "Sensitivity") },
            { "sens", ("чувствительность", "Sens") },
            { "smooth", ("плавность", "Smooth") },
            { "smoothness", ("плавность", "Smoothness") },
            { "smoothing", ("сглаживание", "Smoothing") },
            { "time", ("время", "Time") },
            { "duration", ("длительность", "Duration") },
            { "delay", ("задержка", "Delay") },
            { "cooldown", ("перезарядка", "Cooldown") },
            { "interval", ("интервал", "Interval") },
            { "rate", ("частота", "Rate") },
            { "amount", ("значение", "Amount") },
            { "value", ("значение", "Value") },
            { "count", ("количество", "Count") },
            { "multiplier", ("множитель", "Multiplier") },
            { "factor", ("коэффициент", "Factor") },
            { "ratio", ("отношение", "Ratio") },
            { "curve", ("кривая", "Curve") },
            { "mode", ("режим", "Mode") },
            { "type", ("тип", "Type") },
            { "state", ("состояние", "State") },
            { "enabled", ("включено", "Enabled") },
            { "enable", ("включить", "Enable") },
            { "active", ("активно", "Active") },
            { "use", ("использовать", "Use") },
            { "allow", ("разрешить", "Allow") },
            { "auto", ("авто", "Auto") },
            { "manual", ("ручной", "Manual") },
            { "invert", ("инверсия", "Invert") },
            { "inverted", ("инвертировано", "Inverted") },
            { "lock", ("блокировка", "Lock") },
            { "unlock", ("разблокировка", "Unlock") },
            { "follow", ("следование", "Follow") },
            { "look", ("взгляд", "Look") },
            { "aim", ("прицел", "Aim") },
            { "target", ("цель", "Target") },
            { "shake", ("тряска", "Shake") },
            { "fov", ("FOV", "FOV") },
            { "zoom", ("зум", "Zoom") },
            { "deadzone", ("мёртвая зона", "Deadzone") },
            { "input", ("ввод", "Input") },
            { "mouse", ("мышь", "Mouse") },
            { "keyboard", ("клавиатура", "Keyboard") },
            { "gamepad", ("геймпад", "Gamepad") },
            { "key", ("клавиша", "Key") },
            { "button", ("кнопка", "Button") },
            { "axis", ("ось", "Axis") },
            { "sound", ("звук", "Sound") },
            { "audio", ("аудио", "Audio") },
            { "volume", ("громкость", "Volume") },
            { "ui", ("интерфейс", "UI") },
            { "hud", ("HUD", "HUD") },
            { "debug", ("отладка", "Debug") },
            { "gizmo", ("гизмо", "Gizmo") },
            { "gizmos", ("гизмо", "Gizmos") },
            { "show", ("показывать", "Show") },
            { "hide", ("скрывать", "Hide") },
            { "layer", ("слой", "Layer") },
            { "mask", ("маска", "Mask") },
            { "settings", ("настройки", "Settings") },
            { "config", ("конфиг", "Config") },
            { "general", ("общие", "General") },
            { "main", ("основные", "Main") },
            { "common", ("общие", "Common") },
            { "physics", ("физика", "Physics") },
            { "health", ("здоровье", "Health") },
            { "hp", ("HP", "HP") },
            { "damage", ("урон", "Damage") },
            { "armor", ("броня", "Armor") },
            { "stamina", ("выносливость", "Stamina") },
            { "fuel", ("топливо", "Fuel") },
            { "energy", ("энергия", "Energy") },
            { "forward", ("вперёд", "Forward") },
            { "backward", ("назад", "Backward") },
            { "back", ("назад", "Back") },
            { "left", ("влево", "Left") },
            { "right", ("вправо", "Right") },
            { "up", ("вверх", "Up") },
            { "down", ("вниз", "Down") },
            { "horizontal", ("горизонталь", "Horizontal") },
            { "vertical", ("вертикаль", "Vertical") },
            { "total", ("всего", "Total") },
            { "collected", ("собрано", "Collected") },
            { "sold", ("продано", "Sold") },
            { "earned", ("заработано", "Earned") },
            { "matches", ("матчей", "Matches") },
            { "match", ("матч", "Match") },
            { "played", ("сыграно", "Played") },
            { "won", ("выиграно", "Won") },
            { "has", ("есть", "Has") },
            { "history", ("история", "History") },
            { "cash", ("деньги", "Cash") },
            { "chicken", ("курицы", "Chicken") },
            { "x", ("X", "X") },
            { "y", ("Y", "Y") },
            { "z", ("Z", "Z") }
        };

    private readonly List<Source> _sources = new List<Source>();
    private readonly Dictionary<ScriptableObject, string> _defaults = new Dictionary<ScriptableObject, string>();
    private readonly List<Tab> _tabs = new List<Tab>();

    private PanelLanguage _lang;
    private Canvas _canvas;
    private Font _font;
    private RectTransform _window;
    private RectTransform _content;
    private ScrollRect _scroll;
    private Text _status;
    private Text _resetLabel;
    private string _statusKey;
    private int _statusChars;
    private int _current;
    private bool _dirty;
    private bool _resetArmed;
    private float _resetArmedUntil;

    private CursorLockMode _prevLock;
    private bool _prevVisible;
    private float _prevTimeScale = 1f;
    private bool _stateSaved;

    // =====================================================================
    //  Локализация RU / EN + автоперевод любых имён
    // =====================================================================
    private string L(string key)
    {
        bool ru = _lang == PanelLanguage.Ru;
        switch (key)
        {
            case "title": return ru ? "Тестовые настройки" : "Tester Settings";
            case "lang_btn": return ru ? "EN" : "RU";
            case "copy": return ru ? "Копировать всё (JSON)" : "Copy All (JSON)";
            case "reset": return ru ? "Сбросить всё" : "Reset All";
            case "reset_confirm": return ru ? "Точно сбросить?" : "Are you sure?";
            case "close": return ru ? "Закрыть" : "Close";
            case "on": return ru ? "ВКЛ" : "ON";
            case "off": return ru ? "ВЫКЛ" : "OFF";
            case "sec_game": return ru ? "Игра" : "Game";
            case "sec_vehicles": return ru ? "Машины" : "Vehicles";
            case "sec_player": return ru ? "Данные игрока" : "Player Data";
            case "copied":
                return ru
                ? $"Скопировано в буфер ({_statusChars} симв.)"
                : $"Copied to clipboard ({_statusChars} chars)";
            case "reset_done":
                return ru
                ? "Сброшено к значениям из ассетов"
                : "Reset to asset defaults";
            case "changed_saved":
                return ru
                ? "Изменено и сохранено"
                : "Changed & saved";
            case "changed_applied":
                return ru
                ? "Применено"
                : "Applied";
            default: return key;
        }
    }

    private string TranslateMember(string rawNameOrText, string fallbackFieldName = null)
    {
        if (string.IsNullOrEmpty(rawNameOrText)) return "";

        if (FieldTranslations.TryGetValue(rawNameOrText, out var pair))
            return _lang == PanelLanguage.Ru ? pair.ru : pair.en;

        if (!string.IsNullOrEmpty(fallbackFieldName) && FieldTranslations.TryGetValue(fallbackFieldName, out var fieldPair))
            return _lang == PanelLanguage.Ru ? fieldPair.ru : fieldPair.en;

        string nicified = Nicify(rawNameOrText);
        var tokens = nicified.Split(new[] { ' ', '-', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return nicified;

        var sb = new StringBuilder();
        for (int i = 0; i < tokens.Length; i++)
        {
            string tok = tokens[i];
            string mapped = WordMap.TryGetValue(tok, out var wPair)
                ? (_lang == PanelLanguage.Ru ? wPair.ru : wPair.en)
                : tok;

            if (i > 0) sb.Append(' ');
            sb.Append(mapped);
        }

        string result = sb.ToString().Trim();
        if (result.Length == 0) return nicified;
        return char.ToUpper(result[0], _lang == PanelLanguage.Ru ? new CultureInfo("ru-RU") : CultureInfo.InvariantCulture)
               + result.Substring(1);
    }

    private void ToggleLanguage()
    {
        _lang = _lang == PanelLanguage.Ru ? PanelLanguage.En : PanelLanguage.Ru;
        PlayerPrefs.SetInt(LangPrefKey, (int)_lang);
        PlayerPrefs.Save();
        RebuildUIKeepPosition();
    }

    private void RebuildUIKeepPosition()
    {
        if (_window == null) return;
        Vector2 savedPos = _window.anchoredPosition;
        bool wasOpen = IsOpen;
        int savedTab = _current;
        Destroy(_window.gameObject);
        BuildUI();
        _window.anchoredPosition = savedPos;
        if (_tabs.Count > 0) ShowTab(savedTab);
        RefreshStatusText();
        SetOpen(wasOpen);
    }

    // =====================================================================
    //  Жизненный цикл
    // =====================================================================
    private void Awake()
    {
        _lang = PlayerPrefs.HasKey(LangPrefKey)
            ? (PanelLanguage)Mathf.Clamp(PlayerPrefs.GetInt(LangPrefKey), 0, 1)
            : defaultLanguage;

        _font = LoadFont();

        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (gameSettings != null) _sources.Add(new Source { SectionKey = "sec_game", So = gameSettings });
        if (vehicleSettings != null) _sources.Add(new Source { SectionKey = "sec_vehicles", So = vehicleSettings });
        if (playerData != null) _sources.Add(new Source { SectionKey = "sec_player", So = playerData });
        if (_sources.Count == 0)
            Debug.LogWarning("[TesterSettingsPanel] Не назначены GameSettingsSO / VehiclesSettings / PlayerDataSO.", this);

        foreach (var s in _sources)
            _defaults[s.So] = JsonUtility.ToJson(s.So);

        if (saveBetweenSessions) LoadSaved();

        EnsureEventSystem();
        BuildUI();
        SetOpen(startOpened);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) SetOpen(!IsOpen);

        if (_resetArmed && Time.unscaledTime > _resetArmedUntil) DisarmReset();
    }

    private void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;
        if (saveBetweenSessions) Save();
    }

    private void OnDestroy()
    {
        if (IsOpen) SetOpen(false);
        IsOpen = false;
    }

    private void SetOpen(bool open)
    {
        IsOpen = open;
        if (_window != null)
        {
            _window.gameObject.SetActive(open);
            if (open) _window.SetAsLastSibling();
        }

        if (open)
        {
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
            _prevTimeScale = Time.timeScale;
            _stateSaved = true;

            if (unlockCursorWhenOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (pauseGameWhenOpen) Time.timeScale = 0f;
        }
        else if (_stateSaved)
        {
            _stateSaved = false;
            if (unlockCursorWhenOpen)
            {
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
            }
            if (pauseGameWhenOpen) Time.timeScale = _prevTimeScale;
        }
    }

    // =====================================================================
    //  Сохранение / загрузка / экспорт
    // =====================================================================
    private string Key(ScriptableObject so) => "TesterPanel_" + saveVersion + "_" + so.GetType().Name;

    private void Save()
    {
        foreach (var s in _sources)
        {
            if (s.So is GameDataSO gameData)
                gameData.Save();
            PlayerPrefs.SetString(Key(s.So), JsonUtility.ToJson(s.So));
        }
        PlayerPrefs.Save();
    }

    private void LoadSaved()
    {
        foreach (var s in _sources)
        {
            var k = Key(s.So);
            if (!PlayerPrefs.HasKey(k)) continue;
            try
            {
                JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(k), s.So);
                if (s.So is GameDataSO gameData)
                    gameData.Load();
            }
            catch (Exception e) { Debug.LogWarning("[TesterSettingsPanel] Не удалось загрузить " + k + ": " + e.Message); }
        }
    }

    private void NotifySoChanged(ScriptableObject so)
    {
        if (so is GameDataSO gameData)
        {
            gameData.Save();
            gameData.OnDataChanged?.Invoke();
        }
    }

    private string BuildExport()
    {
        var sb = new StringBuilder();
        foreach (var s in _sources)
        {
            sb.Append("### ").AppendLine(s.So.GetType().Name);
            sb.AppendLine(JsonUtility.ToJson(s.So, true));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private void CopyAll()
    {
        var text = BuildExport();
        GUIUtility.systemCopyBuffer = text;
        SetStatusKey("copied", text.Length);
    }

    private void ResetClicked()
    {
        if (!_resetArmed)
        {
            _resetArmed = true;
            _resetArmedUntil = Time.unscaledTime + 3f;
            if (_resetLabel != null) _resetLabel.text = L("reset_confirm");
            return;
        }

        DisarmReset();
        foreach (var s in _sources)
        {
            JsonUtility.FromJsonOverwrite(_defaults[s.So], s.So);
            PlayerPrefs.DeleteKey(Key(s.So));
            NotifySoChanged(s.So);
        }
        PlayerPrefs.Save();
        ShowTab(_current);
        SettingsChanged?.Invoke();
        SetStatusKey("reset_done");
    }

    private void DisarmReset()
    {
        _resetArmed = false;
        if (_resetLabel != null) _resetLabel.text = L("reset");
    }

    private void Changed()
    {
        _dirty = true;
        SettingsChanged?.Invoke();
        SetStatusKey(saveBetweenSessions ? "changed_saved" : "changed_applied");
    }

    private void SetStatusKey(string key, int charsCount = 0)
    {
        _statusKey = key;
        _statusChars = charsCount;
        RefreshStatusText();
    }

    private void RefreshStatusText()
    {
        if (_status == null) return;
        _status.text = string.IsNullOrEmpty(_statusKey) ? "" : L(_statusKey);
    }

    // =====================================================================
    //  Построение окна в стиле Windows
    // =====================================================================
    private void BuildUI()
    {
        _window = NewRT("Window", transform);
        _window.anchorMin = new Vector2(0.5f, 0.5f);
        _window.anchorMax = new Vector2(0.5f, 0.5f);
        _window.pivot = new Vector2(0.5f, 0.5f);
        _window.sizeDelta = new Vector2(windowWidth, windowHeight);
        _window.anchoredPosition = Vector2.zero;
        Img(_window, new Color(0.07f, 0.08f, 0.1f, 0.98f));

        // --- шапка окна (перетаскиваемая) ---
        var title = NewRT("TitleBar", _window);
        SetRect(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -TitleH), Vector2.zero);
        Img(title, new Color(0.11f, 0.12f, 0.16f, 1f));

        var drag = title.gameObject.AddComponent<TitleDragHandle>();
        drag.Init(_window, _canvas, clampToScreen);

        var th = HLG(title, 6, new RectOffset(10, 4, 4, 4));
        th.childForceExpandHeight = true;

        var tt = Txt(title, L("title") + "  [" + toggleKey + "]", 15, ColText, TextAnchor.MiddleLeft, FontStyle.Bold);
        Le(tt, minW: 160f, flexW: 1f);

        _status = Txt(title, "", 13, new Color(0.5f, 0.9f, 0.6f), TextAnchor.MiddleRight);
        Le(_status, minW: 80f, flexW: 0f);
        RefreshStatusText();

        var langBtn = AddButton(title, L("lang_btn"), ColBtn, ToggleLanguage, TextAnchor.MiddleCenter);
        Le(langBtn, minW: 36f, prefW: 36f, flexW: 0f);

        var closeTopBtn = AddButton(title, "✕", ColClose, () => SetOpen(false), TextAnchor.MiddleCenter);
        Le(closeTopBtn, minW: TitleH - 8f, prefW: TitleH - 8f, flexW: 0f);

        // --- нижняя панель ---
        var bottom = NewRT("Bottom", _window);
        SetRect(bottom, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, BottomH));
        Img(bottom, new Color(0.1f, 0.11f, 0.14f, 1f));
        var hlg = HLG(bottom, 8, new RectOffset(8, 8, 8, 8));
        hlg.childForceExpandHeight = true;

        var copyBtn = AddButton(bottom, L("copy"), ColAccent, CopyAll);
        Le(copyBtn, flexW: 2f);
        var resetBtn = AddButton(bottom, _resetArmed ? L("reset_confirm") : L("reset"), ColBtn, ResetClicked);
        Le(resetBtn, flexW: 1f);
        _resetLabel = resetBtn.GetComponentInChildren<Text>();
        var closeBtn = AddButton(bottom, L("close"), ColBtn, () => SetOpen(false));
        Le(closeBtn, flexW: 1f);

        // --- вкладки ---
        var tabsRT = NewRT("Tabs", _window);
        SetRect(tabsRT, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, BottomH), new Vector2(TabsW, -TitleH));
        Img(tabsRT, new Color(0.09f, 0.1f, 0.12f, 1f));
        VLG(tabsRT, 4, new RectOffset(6, 6, 6, 6));

        _tabs.Clear();
        foreach (var s in _sources)
        {
            var lbl = Txt(tabsRT, L(s.SectionKey).ToUpperInvariant(), 12, ColHint, TextAnchor.MiddleLeft, FontStyle.Bold);
            Le(lbl, minH: 24f);

            var so = s.So;
            bool addedNestedTabs = false;

            foreach (var f in so.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!IsNestedSerializable(f.FieldType) || f.IsNotSerialized) continue;
                addedNestedTabs = true;

                var header = f.GetCustomAttribute<HeaderAttribute>();
                var field = f;
                var tab = new Tab
                {
                    Title = header != null
                        ? TranslateMember(header.header, field.Name)
                        : TranslateMember(field.Name),
                    Type = f.FieldType,
                    Acc = new Acc
                    {
                        Get = () => field.GetValue(so),
                        Set = v => { field.SetValue(so, v); NotifySoChanged(so); }
                    }
                };

                int idx = _tabs.Count;
                var btn = AddButton(tabsRT, tab.Title, ColBtn, () => ShowTab(idx), TextAnchor.MiddleLeft);
                Le(btn, minH: 44f);
                tab.Bg = btn.targetGraphic as Image;
                _tabs.Add(tab);
            }

            if (!addedNestedTabs)
            {
                int tabIdx = _tabs.Count;
                var rootTab = new Tab
                {
                    Title = L(s.SectionKey),
                    Type = so.GetType(),
                    Acc = new Acc
                    {
                        Get = () => so,
                        Set = _ => NotifySoChanged(so)
                    }
                };
                var btn = AddButton(tabsRT, rootTab.Title, ColBtn, () => ShowTab(tabIdx), TextAnchor.MiddleLeft);
                Le(btn, minH: 44f);
                rootTab.Bg = btn.targetGraphic as Image;
                _tabs.Add(rootTab);
            }
        }

        // --- прокручиваемая область ---
        var scrollRT = NewRT("Scroll", _window);
        SetRect(scrollRT, new Vector2(0, 0), new Vector2(1, 1), new Vector2(TabsW + 4, BottomH), new Vector2(0, -TitleH));
        Img(scrollRT, new Color(0, 0, 0, 0.001f));

        var viewport = NewRT("Viewport", scrollRT);
        Fill(viewport, 6, 6, 6, 6);
        viewport.gameObject.AddComponent<RectMask2D>();

        _content = NewRT("Content", viewport);
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = new Vector2(1, 1);
        _content.pivot = new Vector2(0.5f, 1);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = Vector2.zero;
        VLG(_content, 4, new RectOffset(4, 4, 4, 12));
        var csf = _content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scroll = scrollRT.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.viewport = viewport;
        _scroll.content = _content;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 35f;

        if (_tabs.Count > 0) ShowTab(0);
    }

    private void ShowTab(int index)
    {
        if (_tabs.Count == 0) return;
        _current = Mathf.Clamp(index, 0, _tabs.Count - 1);

        for (int i = _content.childCount - 1; i >= 0; i--)
        {
            var c = _content.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }

        for (int i = 0; i < _tabs.Count; i++)
            if (_tabs[i].Bg != null) _tabs[i].Bg.color = i == _current ? ColAccent : ColBtn;

        var tab = _tabs[_current];
        BuildFields(_content, tab.Acc, tab.Type, 0);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        _scroll.verticalNormalizedPosition = 1f;
    }

    // =====================================================================
    //  Рефлексия: поля и свойства -> виджеты
    // =====================================================================
    private void BuildFields(Transform parent, Acc obj, Type type, int depth)
    {
        foreach (var f in GetFields(type))
        {
            var header = f.GetCustomAttribute<HeaderAttribute>();
            if (header != null) AddHeader(parent, TranslateMember(header.header, f.Name));

            var tip = f.GetCustomAttribute<TooltipAttribute>();
            string tipText = tip != null ? TranslateMember(tip.tooltip) : null;
            AddMember(parent, FieldAcc(obj, f), f.FieldType, TranslateMember(f.Name), tipText, f, depth);
        }

        foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0) continue;
            if (!IsLeaf(p.PropertyType)) continue;
            if (p.GetCustomAttribute<HideInInspector>() != null) continue;

            var header = p.GetCustomAttribute<HeaderAttribute>();
            if (header != null) AddHeader(parent, TranslateMember(header.header, p.Name));

            var tip = p.GetCustomAttribute<TooltipAttribute>();
            string tipText = tip != null ? TranslateMember(tip.tooltip) : null;

            var propAcc = new Acc
            {
                Get = () => p.GetValue(obj.Get()),
                Set = v =>
                {
                    var target = obj.Get();
                    p.SetValue(target, v);
                    obj.Set(target);
                }
            };

            AddMember(parent, propAcc, p.PropertyType, TranslateMember(p.Name), tipText, null, depth);
        }
    }

    private void AddMember(Transform parent, Acc acc, Type type, string label, string tip, FieldInfo f, int depth)
    {
        if (IsLeaf(type))
        {
            AddLeaf(parent, acc, type, label, tip, f);
            return;
        }

        if (!type.IsValueType && acc.Get() == null) return;

        if (type.IsArray || IsList(type))
        {
            var list = (IList)acc.Get();
            var et = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            if (!IsLeaf(et) && !IsNestedSerializable(et)) return;

            AddFoldout(parent, label + " [" + list.Count + "]", depth == 0, out var body);
            for (int i = 0; i < list.Count; i++)
                AddMember(body, ElemAcc(acc, i), et, label + " [" + i + "]", null, null, depth + 1);
            return;
        }

        if (IsNestedSerializable(type))
        {
            AddFoldout(parent, label, depth == 0, out var body);
            BuildFields(body, acc, type, depth + 1);
        }
    }

    private void AddLeaf(Transform parent, Acc a, Type t, string label, string tip, FieldInfo f)
    {
        AddRow(parent, label, tip, out var row);

        RangeAttribute range = f != null ? f.GetCustomAttribute<RangeAttribute>() : null;
        MinAttribute min = f != null ? f.GetCustomAttribute<MinAttribute>() : null;

        if (t == typeof(int))
            AddNumber(row, a, true, o => Convert.ToDouble(o), d => (object)(int)Math.Round(d), range, min, 90f);
        else if (t == typeof(float))
            AddNumber(row, a, false, o => (double)(float)o, d => (object)(float)d, range, min, 90f);
        else if (t == typeof(LayerMask))
            AddNumber(row, a, true, o => ((LayerMask)o).value, d => (object)(LayerMask)(int)Math.Round(d), null, null, 110f);
        else if (t == typeof(bool))
            AddBool(row, a);
        else if (t == typeof(string))
            AddString(row, a);
        else if (t.IsEnum)
            AddEnum(row, a, t);
        else if (t == typeof(Vector2))
            AddVector(row, a, 2);
        else if (t == typeof(Vector3))
            AddVector(row, a, 3);
    }

    // ---------- числа ----------
    private void AddNumber(RectTransform row, Acc a, bool integer, Func<object, double> toD, Func<double, object> fromD,
                           RangeAttribute range, MinAttribute min, float fieldWidth)
    {
        double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
        if (range != null) { lo = range.min; hi = range.max; }
        else if (min != null) { lo = min.min; }

        var input = AddInput(row, fieldWidth);
        Slider slider = null;
        if (range != null) slider = AddSlider(row, (float)lo, (float)hi, integer, 140f);

        input.SetTextWithoutNotify(Fmt(toD(a.Get()), integer));
        if (slider != null) slider.SetValueWithoutNotify((float)toD(a.Get()));

        void Apply(double v, bool fromSlider)
        {
            if (integer) v = Math.Round(v);
            if (v < lo) v = lo;
            if (v > hi) v = hi;
            a.Set(fromD(v));
            Changed();
            if (slider != null && !fromSlider) slider.SetValueWithoutNotify((float)v);
            if (fromSlider) input.SetTextWithoutNotify(Fmt(v, integer));
        }

        input.onValueChanged.AddListener(s => { if (TryParseD(s, out var v)) Apply(v, false); });
        input.onEndEdit.AddListener(s => input.SetTextWithoutNotify(Fmt(toD(a.Get()), integer)));
        if (slider != null) slider.onValueChanged.AddListener(v => Apply(v, true));
    }

    private void AddVector(RectTransform row, Acc a, int dims)
    {
        string[] names = { "X", "Y", "Z" };
        for (int i = 0; i < dims; i++)
        {
            var l = Txt(row, names[i], 13, ColHint, TextAnchor.MiddleCenter);
            Le(l, prefW: 14f, minW: 14f, flexW: 0f);

            int idx = i;
            var comp = new Acc
            {
                Get = () => dims == 2 ? ((Vector2)a.Get())[idx] : ((Vector3)a.Get())[idx],
                Set = o =>
                {
                    if (dims == 2) { var v = (Vector2)a.Get(); v[idx] = (float)o; a.Set(v); }
                    else { var v = (Vector3)a.Get(); v[idx] = (float)o; a.Set(v); }
                }
            };
            AddNumber(row, comp, false, o => (double)(float)o, d => (object)(float)d, null, null, 62f);
        }
    }

    // ---------- bool / string / enum ----------
    private void AddBool(RectTransform row, Acc a)
    {
        var btn = AddButton(row, "", ColBtn, null);
        Le(btn, prefW: 90f, minW: 90f, flexW: 0f, minH: 28f);
        var txt = btn.GetComponentInChildren<Text>();
        var img = btn.targetGraphic as Image;

        void Refresh()
        {
            bool v = (bool)a.Get();
            txt.text = v ? L("on") : L("off");
            img.color = v ? ColOn : ColOff;
        }

        Refresh();
        btn.onClick.AddListener(() => { a.Set(!(bool)a.Get()); Refresh(); Changed(); });
    }

    private void AddString(RectTransform row, Acc a)
    {
        var input = AddInput(row, 220f);
        input.SetTextWithoutNotify((string)a.Get() ?? "");
        input.onValueChanged.AddListener(s => { a.Set(s); Changed(); });
    }

    private void AddEnum(RectTransform row, Acc a, Type t)
    {
        var vals = Enum.GetValues(t);

        if (vals.Length <= 12)
        {
            var btn = AddButton(row, a.Get().ToString(), ColBtn, null);
            Le(btn, prefW: 160f, minW: 160f, flexW: 0f, minH: 28f);
            var label = btn.GetComponentInChildren<Text>();
            btn.onClick.AddListener(() =>
            {
                int idx = Array.IndexOf(vals, a.Get());
                a.Set(vals.GetValue((idx + 1) % vals.Length));
                label.text = a.Get().ToString();
                Changed();
            });
            return;
        }

        var input = AddInput(row, 160f);
        input.SetTextWithoutNotify(a.Get().ToString());
        input.onEndEdit.AddListener(s =>
        {
            try
            {
                var v = Enum.Parse(t, s.Trim(), true);
                if (Enum.IsDefined(t, v)) { a.Set(v); Changed(); }
            }
            catch { }
            input.SetTextWithoutNotify(a.Get().ToString());
        });
    }

    // =====================================================================
    //  Аксессоры
    // =====================================================================
    private static Acc FieldAcc(Acc p, FieldInfo f)
    {
        return new Acc
        {
            Get = () => f.GetValue(p.Get()),
            Set = v =>
            {
                var o = p.Get();
                f.SetValue(o, v);
                p.Set(o);
            }
        };
    }

    private static Acc ElemAcc(Acc p, int i)
    {
        return new Acc
        {
            Get = () => ((IList)p.Get())[i],
            Set = v => ((IList)p.Get())[i] = v
        };
    }

    private static IEnumerable<FieldInfo> GetFields(Type t)
    {
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.IsNotSerialized || f.IsLiteral) continue;
            if (f.GetCustomAttribute<HideInInspector>() != null) continue;
            if (f.IsPublic || f.GetCustomAttribute<SerializeField>() != null) yield return f;
        }
    }

    private static bool IsLeaf(Type t)
    {
        return t == typeof(int) || t == typeof(float) || t == typeof(bool) || t == typeof(string)
            || t.IsEnum || t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(LayerMask);
    }

    private static bool IsList(Type t)
    {
        return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
    }

    private static bool IsNestedSerializable(Type t)
    {
        if (t.IsPrimitive || t.IsEnum || t == typeof(string)) return false;
        if (t.Namespace != null && t.Namespace.StartsWith("UnityEngine")) return false;
        if (!(t.IsClass || t.IsValueType)) return false;
        return t.IsDefined(typeof(SerializableAttribute), false);
    }

    private static string Nicify(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        name = name.Replace("_", " ").Trim();
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c))
            {
                char prev = name[i - 1];
                bool nextLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (char.IsLower(prev) || (char.IsUpper(prev) && nextLower)) sb.Append(' ');
            }
            sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }
        return sb.ToString();
    }

    private static string Fmt(double v, bool integer)
    {
        return v.ToString(integer ? "0" : "0.######", CultureInfo.InvariantCulture);
    }

    private static bool TryParseD(string s, out double v)
    {
        s = (s ?? "").Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
               && !double.IsNaN(v) && !double.IsInfinity(v);
    }

    // =====================================================================
    //  Мелкие UI-хелперы
    // =====================================================================
    private void AddHeader(Transform parent, string text)
    {
        var t = Txt(parent, text, 15, ColHeader, TextAnchor.LowerLeft, FontStyle.Bold);
        Le(t, minH: 30f);
    }

    private void AddRow(Transform parent, string label, string tip, out RectTransform row)
    {
        var field = NewRT(label, parent);
        var v = VLG(field, 0, new RectOffset(0, 0, 0, 0));
        v.childForceExpandHeight = false;

        row = NewRT("Row", field);
        HLG(row, 6, new RectOffset(0, 0, 0, 0));
        Le(row, minH: 30f);

        var l = Txt(row, label, 15, ColText, TextAnchor.MiddleLeft);
        Le(l, minW: 150f, flexW: 1f);

        if (!string.IsNullOrEmpty(tip))
        {
            var h = Txt(field, tip, 12, ColHint, TextAnchor.UpperLeft);
            h.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
    }

    private void AddFoldout(Transform parent, string label, bool open, out RectTransform body)
    {
        var box = NewRT(label, parent);
        var v = VLG(box, 2, new RectOffset(0, 0, 0, 0));
        v.childForceExpandHeight = false;

        string Caption(bool o) => (o ? "[-]  " : "[+]  ") + label;

        var head = AddButton(box, Caption(open), new Color(0.14f, 0.16f, 0.2f), null, TextAnchor.MiddleLeft);
        Le(head, minH: 30f);
        var headText = head.GetComponentInChildren<Text>();

        var bodyRT = NewRT("Body", box);
        VLG(bodyRT, 3, new RectOffset(14, 0, 2, 6)).childForceExpandHeight = false;
        bodyRT.gameObject.SetActive(open);

        head.onClick.AddListener(() =>
        {
            bool o = !bodyRT.gameObject.activeSelf;
            bodyRT.gameObject.SetActive(o);
            headText.text = Caption(o);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        });

        body = bodyRT;
    }

    private InputField AddInput(Transform row, float width)
    {
        var rt = NewRT("Input", row);
        var img = Img(rt, ColInput);
        Le(rt, minW: width, prefW: width, flexW: 0f, minH: 28f);

        var text = Txt(rt, "", 15, ColText, TextAnchor.MiddleLeft);
        Fill(text.rectTransform, 6, 2, 6, 2);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.supportRichText = false;

        var input = rt.gameObject.AddComponent<InputField>();
        input.targetGraphic = img;
        input.textComponent = text;
        input.lineType = InputField.LineType.SingleLine;
        input.contentType = InputField.ContentType.Standard;
        return input;
    }

    private Slider AddSlider(Transform row, float min, float max, bool whole, float width)
    {
        var root = NewRT("Slider", row);
        Le(root, minW: width, prefW: width, flexW: 0f, minH: 22f);

        var bg = NewRT("Background", root);
        SetRect(bg, new Vector2(0, 0.3f), new Vector2(1, 0.7f), Vector2.zero, Vector2.zero);
        Img(bg, ColInput);

        var fillArea = NewRT("Fill Area", root);
        SetRect(fillArea, new Vector2(0, 0.3f), new Vector2(1, 0.7f), new Vector2(0, 0), new Vector2(0, 0));
        var fill = NewRT("Fill", fillArea);
        fill.anchorMin = new Vector2(0, 0);
        fill.anchorMax = new Vector2(1, 1);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        Img(fill, ColAccent);

        var handleArea = NewRT("Handle Slide Area", root);
        SetRect(handleArea, new Vector2(0, 0), new Vector2(1, 1), new Vector2(7, 0), new Vector2(-7, 0));
        var handle = NewRT("Handle", handleArea);
        handle.anchorMin = new Vector2(0, 0);
        handle.anchorMax = new Vector2(0, 1);
        handle.sizeDelta = new Vector2(14, 0);
        var handleImg = Img(handle, Color.white);

        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill;
        s.handleRect = handle;
        s.targetGraphic = handleImg;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = whole;
        return s;
    }

    private Button AddButton(Transform parent, string label, Color bg, UnityAction onClick, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var rt = NewRT(label, parent);
        var img = Img(rt, bg);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;

        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.88f, 1f);
        cb.pressedColor = new Color(0.65f, 0.68f, 0.8f);
        cb.selectedColor = Color.white;
        b.colors = cb;

        var t = Txt(rt, label, 15, ColText, align);
        Fill(t.rectTransform, 8, 0, 8, 0);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;

        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }

    private Text Txt(Transform parent, string s, int size, Color c, TextAnchor anchor, FontStyle style = FontStyle.Normal)
    {
        var rt = NewRT("Text", parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = _font;
        t.fontSize = size;
        t.color = c;
        t.alignment = anchor;
        t.fontStyle = style;
        t.text = s;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static Image Img(RectTransform rt, Color c)
    {
        var i = rt.gameObject.AddComponent<Image>();
        i.color = c;
        return i;
    }

    private static RectTransform NewRT(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
    }

    private static void Fill(RectTransform rt, float l, float b, float r, float t)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    private static VerticalLayoutGroup VLG(RectTransform rt, float spacing, RectOffset pad)
    {
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = pad;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return v;
    }

    private static HorizontalLayoutGroup HLG(RectTransform rt, float spacing, RectOffset pad)
    {
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.padding = pad;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        return h;
    }

    private static LayoutElement Le(Component c, float minW = -1f, float prefW = -1f, float flexW = -1f, float minH = -1f)
    {
        var le = c.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        if (minW >= 0f) le.minWidth = minW;
        if (prefW >= 0f) le.preferredWidth = prefW;
        if (flexW >= 0f) le.flexibleWidth = flexW;
        if (minH >= 0f) le.minHeight = minH;
        return le;
    }

    private static Font LoadFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return f;
    }

    private static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        var existing = FindFirstObjectByType<EventSystem>();
#else
        var existing = FindObjectOfType<EventSystem>();
#endif
        if (existing != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}