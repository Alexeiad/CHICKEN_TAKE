using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One translation database and explicit references to every menu label.
public sealed class MenuLocalization : MonoBehaviour
{
    [Serializable]
    public sealed class TextBinding
    {
        public string key;
        public TMP_Text tmpText;
        public Text legacyText;
    }

    private const string LanguagePreference = "Menu.Language";
    [SerializeField] private MenuTranslationsSO translations;
    [SerializeField] private MenuLanguage language = MenuLanguage.Russian;
    [SerializeField] private TextBinding[] texts = Array.Empty<TextBinding>();
    [SerializeField] private Canvas menuCanvas;
    [SerializeField] private TMP_FontAsset languageFont;
    [SerializeField] private TMP_Dropdown cameraModeDropdown;

    private Button languageButton;
    private TMP_Text languageLabel;
    public MenuLanguage CurrentLanguage => language;

    private void Awake()
    {
        string saved = PlayerPrefs.GetString(LanguagePreference, language.ToString());
        if (Enum.TryParse(saved, out MenuLanguage parsed) && Enum.IsDefined(typeof(MenuLanguage), parsed))
            language = parsed;
        CreateLanguageButton();
        foreach (TextBinding binding in texts)
        {
            if (binding == null) continue;
            if (binding.tmpText != null && !binding.tmpText.enableAutoSizing)
            {
                binding.tmpText.fontSizeMax = binding.tmpText.fontSize;
                binding.tmpText.fontSizeMin = binding.tmpText.fontSize * 0.55f;
                binding.tmpText.enableAutoSizing = true;
            }
            if (binding.legacyText != null && !binding.legacyText.resizeTextForBestFit)
            {
                binding.legacyText.resizeTextMaxSize = binding.legacyText.fontSize;
                binding.legacyText.resizeTextMinSize = Mathf.Max(10, binding.legacyText.fontSize / 2);
                binding.legacyText.resizeTextForBestFit = true;
            }
        }
    }

    private void OnEnable() => RefreshTexts();

    public void SetLanguage(int index)
    {
        if (!Enum.IsDefined(typeof(MenuLanguage), index)) return;
        language = (MenuLanguage)index;
        PlayerPrefs.SetString(LanguagePreference, language.ToString());
        PlayerPrefs.Save();
        RefreshTexts();
    }

    public void NextLanguage() => SetLanguage(((int)language + 1) % Enum.GetValues(typeof(MenuLanguage)).Length);

    [ContextMenu("Refresh translations")]
    public void RefreshTexts()
    {
        if (translations == null) return;
        foreach (TextBinding binding in texts)
        {
            if (binding == null) continue;
            string value = translations.GetText(binding.key, language);
            if (binding.tmpText != null) binding.tmpText.text = value;
            if (binding.legacyText != null) binding.legacyText.text = value;
        }
        if (languageLabel != null)
            languageLabel.text = translations.GetText("languageText", language);
        if (cameraModeDropdown != null && cameraModeDropdown.options.Count == 2)
        {
            cameraModeDropdown.Hide();
            cameraModeDropdown.options[0].text = translations.GetText("firstPersonText", language);
            cameraModeDropdown.options[1].text = translations.GetText("thirdPersonText", language);
            cameraModeDropdown.RefreshShownValue();
        }
    }

    private void CreateLanguageButton()
    {
        if (menuCanvas == null) return;
        var buttonObject = new GameObject("Language Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = menuCanvas.gameObject.layer;
        var rect = (RectTransform)buttonObject.transform;
        rect.SetParent(menuCanvas.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-24, -24);
        rect.sizeDelta = new Vector2(260, 52);
        buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
        languageButton = buttonObject.GetComponent<Button>();
        languageButton.targetGraphic = buttonObject.GetComponent<Image>();
        languageButton.onClick.AddListener(NextLanguage);

        var labelObject = new GameObject("Language Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = buttonObject.layer;
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 4);
        labelRect.offsetMax = new Vector2(-10, -4);
        languageLabel = labelObject.GetComponent<TextMeshProUGUI>();
        languageLabel.font = languageFont != null ? languageFont : TMP_Settings.defaultFontAsset;
        languageLabel.fontSize = 22;
        languageLabel.enableAutoSizing = true;
        languageLabel.fontSizeMin = 16;
        languageLabel.fontSizeMax = 22;
        languageLabel.alignment = TextAlignmentOptions.Center;
        languageLabel.color = Color.white;
        languageLabel.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (languageButton != null)
        {
            languageButton.onClick.RemoveListener(NextLanguage);
            Destroy(languageButton.gameObject);
        }
    }
}
