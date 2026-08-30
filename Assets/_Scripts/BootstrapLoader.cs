using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootstrapLoader : MonoBehaviour
{
    public static int TargetSceneIndex = 1;

    [SerializeField] private float minWaitTime = 1f;

    private void Awake()
    {
        Time.timeScale = 1f;
    }

    private void Start()
    {
        StartCoroutine(LoadSceneRoutine());
    }

    private IEnumerator LoadSceneRoutine()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(TargetSceneIndex);

        if (operation == null)
        {
            Debug.LogError($"[Bootstrap] Scene with index {TargetSceneIndex} could not be loaded.");
            yield break;
        }

        operation.allowSceneActivation = false;

        float timer = 0f;

        while (timer < minWaitTime || operation.progress < 0.9f)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        operation.allowSceneActivation = true;
    }
}