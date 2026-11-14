using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Zenject;

public sealed class RacerState
{
    public IEntity Entity { get; }
    public string Name => Entity.Name;

    public float Progress { get; set; }
    public int Place { get; set; }
    public bool Finished { get; set; }

    public RacerState(IEntity entity)
    {
        Entity = entity;
    }
}

public class Race : MonoBehaviour
{
    [Header("Трасса")]
    [SerializeField] private SplineContainer _track;

    [Header("Настройки")]
    [SerializeField] private int _countdownFrom = 3;

    [Tooltip("Насколько далеко от сплайна может быть гонщик, чтобы участвовать")]
    [SerializeField] private float _maxDistanceFromTrack = 50f;

    [Header("UI")]
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField] private GameObject _finishWindow;
    [SerializeField] private TMP_Text _standingsText;

    private readonly List<RacerState> _racers = new List<RacerState>();
    private readonly HashSet<IEntity> _registrySet = new HashSet<IEntity>();
    private readonly StringBuilder _sb = new StringBuilder(256);

    private IEntityRegistry<IEntity> _registry;
    private Vechicle _playerVehicle;

    private float _finishT;
    private bool _started;
    private bool _finished;

    public IReadOnlyList<RacerState> Standings => _racers;

    [Inject]
    public void Construct(IEntityRegistry<IEntity> registry)
    {
        _registry = registry;
    }

    private IEnumerator Start()
    {
        _finishWindow.SetActive(false);
        _finishT = CalculateFinishT();

        yield return Countdown();

        _started = true;
    }

    private void Update()
    {
        if (!_started || _finished) return;

        SyncWithRegistry();

        if (_racers.Count == 0) return;

        UpdateProgress();
        UpdateStandings();
        CheckFinish();
    }

    private void SyncWithRegistry()
    {
        if (_registry == null) return;

        _registrySet.Clear();
        foreach (IEntity e in _registry.AllEntities)
        {
            if (e is Player) continue;
            if (e == null || e.Transform == null) continue;
            if (!e.Transform.gameObject.activeInHierarchy) continue;

            MonoBehaviour mb = e as MonoBehaviour;
            if (mb != null && !mb.isActiveAndEnabled) continue;

            Vector3 local = _track.transform.InverseTransformPoint(e.Transform.position);
            SplineUtility.GetNearestPoint(_track.Spline, local, out float3 nearest, out _);

            Vector3 nearestV3 = new Vector3(nearest.x, nearest.y, nearest.z);
            if (Vector3.Distance(nearestV3, local) > _maxDistanceFromTrack) continue;

            _registrySet.Add(e);
        }

        for (int i = _racers.Count - 1; i >= 0; i--)
            if (!_registrySet.Contains(_racers[i].Entity))
                _racers.RemoveAt(i);

        foreach (IEntity e in _registrySet)
            if (!ContainsRacer(e))
                _racers.Add(new RacerState(e));

        _playerVehicle = FindPlayerVehicle();
    }

    private Vechicle FindPlayerVehicle()
    {
        if (_registry == null) return null;

        Player player = null;
        foreach (IEntity e in _registry.AllEntities)
            if (e is Player p) { player = p; break; }

        if (player == null) return null;

        Vechicle closest = null;
        float bestSqr = float.MaxValue;
        Vector3 pp = player.transform.position;

        foreach (IEntity e in _registry.AllEntities)
        {
            if (e is not Vechicle v) continue;
            if (!v.gameObject.activeInHierarchy) continue;

            float sqr = (v.transform.position - pp).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                closest = v;
            }
        }

        return closest;
    }

    private bool ContainsRacer(IEntity entity)
    {
        for (int i = 0; i < _racers.Count; i++)
            if (ReferenceEquals(_racers[i].Entity, entity)) return true;
        return false;
    }

    private IEnumerator Countdown()
    {
        _countdownText.gameObject.SetActive(true);
        StopTime();

        for (int i = _countdownFrom; i > 0; i--)
        {
            _countdownText.text = i.ToString();
            yield return new WaitForSecondsRealtime(1f);
        }

        _countdownText.gameObject.SetActive(false);
        StartTime();
    }

    private float CalculateFinishT()
    {
        Spline spline = _track.Spline;

        if (spline.Count < 3)
        {
            Debug.LogError("В сплайне должно быть минимум 3 узла", this);
            return 1f;
        }

        int curveCount = spline.Count - 1;
        int finishKnot = spline.Count - 2;

        return (float)finishKnot / curveCount;
    }

    private void UpdateProgress()
    {
        foreach (RacerState racer in _racers)
            racer.Progress = GetProgress(racer.Entity.Transform.position);
    }

    private float GetProgress(Vector3 worldPosition)
    {
        Vector3 localPosition = _track.transform.InverseTransformPoint(worldPosition);
        SplineUtility.GetNearestPoint(_track.Spline, localPosition, out _, out float t);
        return t;
    }

    private void UpdateStandings()
    {
        _racers.Sort((a, b) => b.Progress.CompareTo(a.Progress));

        for (int i = 0; i < _racers.Count; i++)
            _racers[i].Place = i + 1;

        UpdateStandingsUI();
    }

    private void UpdateStandingsUI()
    {
        if (_standingsText == null) return;

        _sb.Clear();
        for (int i = 0; i < _racers.Count; i++)
        {
            if (i > 0) _sb.Append('\n');

            bool isLeader = i == 0;
            bool isPlayer = _playerVehicle != null && ReferenceEquals(_racers[i].Entity, _playerVehicle);

            string line = $"{i + 1}. {_racers[i].Name ?? "—"}";
            if (isPlayer) line += " (Вы)";

            // Игрок важнее: если ты первый — ты жёлтый, не зелёный
            if (isPlayer)
                _sb.Append("<color=#FFD700>").Append(line).Append("</color>");
            else if (isLeader)
                _sb.Append("<color=#00FF00>").Append(line).Append("</color>");
            else
                _sb.Append(line);
        }

        _standingsText.text = _sb.ToString();
    }

    private void CheckFinish()
    {
        if (_racers[0].Progress < _finishT) return;
        Finish();
    }

    private void Finish()
    {
        _finished = true;
        _racers[0].Finished = true;

        LogResults();

        _finishWindow.SetActive(true);
        UnlockCursor();
        StopTime();
    }

    private void LogResults()
    {
        foreach (RacerState racer in _racers)
            Debug.Log($"{racer.Place}. {racer.Name}");
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void StartTime() => Time.timeScale = 1f;
    private void StopTime() => Time.timeScale = 0f;

    private void OnDestroy()
    {
        StartTime();
    }
}