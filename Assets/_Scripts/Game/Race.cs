using System.Collections;
using TMPro;
using UnityEngine;
using Zenject;

public class Race : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private Transform _finishPoint;
    [SerializeField] private float _finishRadius = 3f;
    [SerializeField] private int _countdownFrom = 3;

    [Header("UI")]
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField] private TMP_Text _distanceText;      // необязательно
    [SerializeField] private GameObject _finishWindow;

    private Vechicle _vehicle;

    private bool _started;
    private bool _finished;

    [Inject]
    public void Construct(Vechicle vehicle)
    {
        _vehicle = vehicle;
    }

    private IEnumerator Start()
    {
        _finishWindow.SetActive(false);
        _countdownText.gameObject.SetActive(true);

        Time.timeScale = 0f;

        for (int i = _countdownFrom; i > 0; i--)
        {
            _countdownText.text = i.ToString();
            yield return new WaitForSecondsRealtime(1f);
        }

        _countdownText.gameObject.SetActive(false);
        Time.timeScale = 1f;
        _started = true;
    }

    private void Update()
    {
        if (!_started || _finished) return;

        float distance = Vector3.Distance(_vehicle.transform.position, _finishPoint.position);

        if (_distanceText != null)
            _distanceText.text = distance.ToString("F0") + " m";

        if (distance <= _finishRadius)
            Finish();
    }

    private void Finish()
    {
        _finished = true;
        _finishWindow.SetActive(true);
        StopTime();
    }

    private void StopTime()
    {
        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void OnDrawGizmosSelected()
    {
        if (_finishPoint == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_finishPoint.position, _finishRadius);
    }
}