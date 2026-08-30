using UnityEngine;
using UnityEngine.SceneManagement;

public class Gate : MonoBehaviour
{
    [SerializeField] private GameObject _targetObject;
    [SerializeField] private int _sceneIndex = 3;      
    [SerializeField] private int _distance = 5;

    private void Update()
    {

        float distance = Vector3.Distance(transform.position, _targetObject.transform.position);

        if (distance < _distance)
        {
            BootstrapLoader.TargetSceneIndex = _sceneIndex;
            SceneManager.LoadScene(0);
            return;
        }
    }
}