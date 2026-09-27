using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class InformationPointUI : MonoBehaviour
{
    [SerializeField] private GameObject _canvas;
    [SerializeField] private float _interactionDistance = 5f;
    [SerializeField] private float _openDelay = .5f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.F;
    [SerializeField] private BuildingMenuConfig _buildingMenus;
    [SerializeField] private BuildingKind _building;

    [Inject(Optional = true)]
    private IEntityRegistry<IEntity> _registry;

    private static InformationPointUI active;
    private static int lastClosedFrame = -1;

    public static bool BlocksGameplayInput =>
        active != null || lastClosedFrame == Time.frameCount;

    public bool IsOpen => active == this;

    private Player player;
    private BuildingMenuView view;
    private GameObject prompt;

    private float nearbyTime;
    private float savedTimeScale;

    private CursorLockMode savedLock;
    private bool savedVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active = null;
        lastClosedFrame = -1;
    }

    private void Awake()
    {
        if (_canvas != null)
            _canvas.SetActive(false);
    }

    private void Update()
    {
        if (IsOpen)
        {
            HandleOpenState();
            return;
        }

        if (BlocksGameplayInput || Time.timeScale <= 0f)
        {
            HideInteraction();
            return;
        }

        UpdatePlayer();

        if (player == null)
        {
            HideInteraction();
            return;
        }

        if (!player.gameObject.activeInHierarchy)
        {
            HideInteraction();
            return;
        }

        if (player.State == Player.VehicleState.InVehicle)
        {
            HideInteraction();
            return;
        }

        UpdateInteraction();
    }

    private void HandleOpenState()
    {
        if (!Input.GetKeyDown(_interactionKey) &&
            !Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        if (view != null)
        {
            view.Back();
            return;
        }

        Close();
    }

    private void UpdatePlayer()
    {
        if (player != null)
            return;

        player = GetPlayer();
    }

    private Player GetPlayer()
    {
        if (_registry == null)
            return null;

        return _registry.AllEntities
            .OfType<Player>()
            .FirstOrDefault(currentPlayer => currentPlayer != null);
    }

    private void UpdateInteraction()
    {
        bool nearby = IsPlayerNearby();

        if (nearby)
            nearbyTime += Time.unscaledDeltaTime;
        else
            nearbyTime = 0f;

        ShowPrompt(nearby);

        if (!nearby)
            return;

        if (nearbyTime < _openDelay)
            return;

        if (Input.GetKeyDown(_interactionKey))
            Open();
    }

    private bool IsPlayerNearby()
    {
        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;

        return delta.sqrMagnitude <=
               _interactionDistance * _interactionDistance;
    }

    public void Open()
    {
        if (BlocksGameplayInput)
            return;

        if (!isActiveAndEnabled)
            return;

        if (_buildingMenus == null && _canvas == null)
            return;

        if (player == null)
            return;

        if (!player.gameObject.activeInHierarchy)
            return;

        if (player.State == Player.VehicleState.InVehicle)
            return;

        SaveGameState();

        active = this;

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ShowPrompt(false);

        try
        {
            if (_buildingMenus != null)
            {
                view = BuildingMenuView.Create(
                    _buildingMenus,
                    _building,
                    Close);
            }
            else
            {
                _canvas.SetActive(true);
            }
        }
        catch
        {
            Close();
            throw;
        }
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        DestroyView();
        DisableCanvas();
        RestoreGameState();

        active = null;
        lastClosedFrame = Time.frameCount;
        nearbyTime = 0f;
    }

    private void SaveGameState()
    {
        savedTimeScale = Time.timeScale;
        savedLock = Cursor.lockState;
        savedVisible = Cursor.visible;
    }

    private void RestoreGameState()
    {
        Time.timeScale = savedTimeScale;
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
    }

    private void DestroyView()
    {
        if (view == null)
            return;

        view.gameObject.SetActive(false);
        Destroy(view.gameObject);

        view = null;
    }

    private void DisableCanvas()
    {
        if (_canvas != null)
            _canvas.SetActive(false);
    }

    private void HideInteraction()
    {
        nearbyTime = 0f;
        ShowPrompt(false);
    }

    private void ShowPrompt(bool show)
    {
        if (!show)
        {
            if (prompt != null)
                prompt.SetActive(false);

            return;
        }

        if (prompt == null)
            CreatePrompt();

        prompt.SetActive(true);
    }

    private void CreatePrompt()
    {
        prompt = new GameObject(
            "Building interaction prompt",
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = prompt.GetComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = prompt.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution = new Vector2(1600, 900);

        GameObject panel = new GameObject(
            "Prompt",
            typeof(RectTransform),
            typeof(Image));

        RectTransform rect = (RectTransform)panel.transform;

        rect.SetParent(prompt.transform, false);

        rect.anchorMin = new Vector2(.5f, 0f);
        rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, 0f);

        rect.anchoredPosition = new Vector2(0f, 40f);
        rect.sizeDelta = new Vector2(600f, 65f);

        Image image = panel.GetComponent<Image>();

        image.color = new Color(.1f, .1f, .1f, .9f);
        image.raycastTarget = false;

        TextMeshProUGUI label = CreatePromptLabel(rect);

        if (_buildingMenus != null)
            label.font = _buildingMenus.font;

        label.text = GetPromptText();
    }

    private TextMeshProUGUI CreatePromptLabel(RectTransform parent)
    {
        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));

        TextMeshProUGUI label =
            labelObject.GetComponent<TextMeshProUGUI>();

        RectTransform rect = label.rectTransform;

        rect.SetParent(parent, false);

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = new Vector2(12f, 5f);
        rect.offsetMax = new Vector2(-12f, -5f);

        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return label;
    }

    private string GetPromptText()
    {
        MenuLanguage language = BuildingMenuConfig.SavedLanguage;

        string[] ru =
        {
            "Амбар",
            "Ангар",
            "Магазин"
        };

        string[] en =
        {
            "Barn",
            "Hangar",
            "Shop"
        };

        string[] de =
        {
            "Scheune",
            "Hangar",
            "Laden"
        };

        string[] names = language switch
        {
            MenuLanguage.English => en,
            MenuLanguage.German => de,
            _ => ru
        };

        string name = names[(int)_building];

        return $"[{_interactionKey}]  {name}";
    }

    private void OnDisable()
    {
        Close();
        ShowPrompt(false);
    }

    private void OnDestroy()
    {
        Close();

        if (prompt != null)
            Destroy(prompt);
    }
}
