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
    [Inject(Optional = true)] private IEntityRegistry<IEntity> _registry;
    private static InformationPointUI active;
    private static int lastClosedFrame = -1;
    public static bool BlocksGameplayInput => active != null || lastClosedFrame == Time.frameCount;
    public bool IsOpen => active == this;
    private Player player;
    private BuildingMenuView view;
    private GameObject prompt;
    private float nearbyTime;
    private float savedTimeScale;
    private CursorLockMode savedLock;
    private bool savedVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; lastClosedFrame = -1; }
    private void Awake() { if (_canvas != null) _canvas.SetActive(false); }

    private void Update()
    {
        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (view != null) view.Back(); else Close();
            }
            return;
        }
        if (BlocksGameplayInput || Time.timeScale <= 0)
        {
            ShowPrompt(false); nearbyTime = 0; return;
        }
        if (player == null || !player.gameObject.activeInHierarchy)
            player = _registry != null ? _registry.AllEntities.OfType<Player>()
                .FirstOrDefault(p => p != null && p.gameObject.activeInHierarchy) : FindObjectOfType<Player>();
        if (player == null) { ShowPrompt(false); return; }
        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0;
        bool nearby = delta.sqrMagnitude <= _interactionDistance * _interactionDistance;
        nearbyTime = nearby ? nearbyTime + Time.unscaledDeltaTime : 0;
        ShowPrompt(nearby);
        if (nearby && nearbyTime >= _openDelay && Input.GetKeyDown(_interactionKey)) Open();
    }

    public void Open()
    {
        if (BlocksGameplayInput || !isActiveAndEnabled || (_buildingMenus == null && _canvas == null)) return;
        savedTimeScale = Time.timeScale;
        savedLock = Cursor.lockState; savedVisible = Cursor.visible;
        active = this;
        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        ShowPrompt(false);
        try
        {
            if (_buildingMenus != null) view = BuildingMenuView.Create(_buildingMenus, _building, Close);
            else _canvas.SetActive(true);
        }
        catch { Close(); throw; }
    }

    public void Close()
    {
        if (!IsOpen) return;
        if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); view = null; }
        if (_canvas != null) _canvas.SetActive(false);
        Time.timeScale = savedTimeScale;
        Cursor.lockState = savedLock; Cursor.visible = savedVisible;
        active = null;
        lastClosedFrame = Time.frameCount;
        nearbyTime = 0;
    }

    private void ShowPrompt(bool show)
    {
        if (!show) { if (prompt != null) prompt.SetActive(false); return; }
        if (prompt == null)
        {
            prompt = new GameObject("Building interaction prompt", typeof(Canvas), typeof(CanvasScaler));
            var canvas = prompt.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = prompt.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            var panel = new GameObject("Prompt", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(prompt.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 40); rect.sizeDelta = new Vector2(600, 65);
            panel.GetComponent<Image>().color = new Color(.1f, .1f, .1f, .9f);
            panel.GetComponent<Image>().raycastTarget = false;
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12, 5); label.rectTransform.offsetMax = new Vector2(-12, -5);
            if (_buildingMenus != null) label.font = _buildingMenus.font;
            label.fontSize = 28; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            MenuLanguage language = BuildingMenuConfig.SavedLanguage;
            string[] ru = { "Амбар", "Ангар", "Магазин" };
            string[] en = { "Barn", "Hangar", "Shop" };
            string[] de = { "Scheune", "Hangar", "Laden" };
            string name = (language == MenuLanguage.English ? en : language == MenuLanguage.German ? de : ru)[(int)_building];
            label.text = "[" + _interactionKey + "]  " + name;
        }
        prompt.SetActive(true);
    }

    private void OnDisable() { Close(); ShowPrompt(false); }
    private void OnDestroy() { Close(); if (prompt != null) Destroy(prompt); }
}
