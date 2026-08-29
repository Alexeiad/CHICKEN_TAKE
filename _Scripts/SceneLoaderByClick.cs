using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderByClick : MonoBehaviour
{
    [SerializeField] private int _sceneIndex = 1;

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnClick();
        }
    }

    public void OnClick()
    {
        BootstrapLoader.TargetSceneIndex = _sceneIndex;
        SceneManager.LoadScene(0);
    }
}