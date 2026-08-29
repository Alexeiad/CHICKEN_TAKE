using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TestMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject savePanel;
    [SerializeField] private GameObject authorsPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject soundPanel;
    [SerializeField] private GameObject graphicsPanel;

    [SerializeField] private int _lvlBase=2;

    private GameObject[] panels;

    private void Awake()
    {
        panels = new[] { mainPanel, savePanel, authorsPanel, settingsPanel, soundPanel, graphicsPanel };
        ShowMain();
    }

    public void ShowMain() => Show(mainPanel);
    public void ShowSaveSlots() => Show(savePanel);
    public void ShowAuthors() => Show(authorsPanel);
    public void ShowSettings() => Show(settingsPanel);
    public void ShowSound() => Show(soundPanel);
    public void ShowGraphics() => Show(graphicsPanel);

    public void StartGame()
    {
        Debug.Log("Старт игры: здесь можно подключить загрузку игровой сцены.");
        // 1. Передаем цифру в BootstrapLoader
        BootstrapLoader.TargetSceneIndex = _lvlBase;

        // 2. Открываем сцену Bootstrap (индекс 0)
        SceneManager.LoadScene(0);
    }

    public void SelectSaveSlot(int slot)
    {
        Debug.Log($"Выбрана ячейка сохранения {slot}.");
        ShowMain();
    }

    public void ButtonPlaceholder(string settingName)
    {
        Debug.Log($"Настройка «{settingName}»: подключите игровую логику к этой кнопке.");
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Show(GameObject target)
    {
        if (panels == null)
            panels = new[] { mainPanel, savePanel, authorsPanel, settingsPanel, soundPanel, graphicsPanel };

        foreach (GameObject panel in panels)
            if (panel != null)
                panel.SetActive(panel == target);
    }
}
