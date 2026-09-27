using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderByClick : MonoBehaviour
{
    [SerializeField] private int _sceneIndex = 1;

    private void Update()
    {
        if (InformationPointUI.BlocksGameplayInput) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnClick();
        }
    }

    public void OnClick()
    {
        if (InformationPointUI.BlocksGameplayInput) return;
        BootstrapLoader.TargetSceneIndex = _sceneIndex;
        SceneManager.LoadScene(0);
    }
}
