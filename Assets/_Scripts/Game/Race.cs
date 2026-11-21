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
    public bool IsPlayer { get; set; }

    /// <summary>Сторона от линии финиша: меньше 0 до линии, больше 0 после неё.</summary>
    public float FinishSide { get; set; }
    public bool HasFinishSide { get; set; }
    public bool HasTrackedProgress { get; set; }

    /// <summary>Сглаженная скорость, км/ч.</summary>
    public float SpeedKmh { get; set; }

    /// <summary>Пробег с начала гонки, м.</summary>
    public float DistanceMeters { get; set; }
    public float DistanceKm => DistanceMeters / 1000f;

    public Vector3 LastPosition { get; set; }
    public bool HasLastPosition { get; set; }

    /// <summary>Накопленный путь за текущий интервал телеметрии, м.</summary>
    public float IntervalDistanceMeters { get; set; }

    public RacerState(IEntity entity)
    {
        Entity = entity;
    }
}

public class Race : MonoBehaviour
{
    [Header("Трасса")]
    [SerializeField] private SplineContainer _track;

    [Header("Префабы")]
    [SerializeField] private GameObject _startPrefab;
    [SerializeField] private GameObject _finishPrefab;

    [Header("Настройки")]
    [SerializeField] private int _countdownFrom = 3;

    [Tooltip("Как часто обновлять показатели и таблицу позиций (сек)")]
    [SerializeField] private float _updateInterval = 0.5f;

    [Tooltip("Насколько далеко от сплайна может быть гонщик, чтобы участвовать")]
    [SerializeField] private float _maxDistanceFromTrack = 50f;

    [Tooltip("Половина ширины линии финиша: дальше от центра финиш не засчитывается")]
    [SerializeField] private float _finishHalfWidth = 13f;

    [Tooltip("Максимальный скачок прогресса за кадр. Больше этого считается прыжком на другой участок трассы и игнорируется")]
    [SerializeField] private float _maxProgressStep = 0.1f;

    [Header("События")]
    [SerializeField] private RaceEventChannelSO _raceEvents;

    [Header("UI")]
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField] private GameObject _finishWindow;
    [SerializeField] private TMP_Text _standingsText;
    [SerializeField] private TMP_Text _resultsText;

    private const int StartKnot = 1;
    private const int MinKnots = 4;

    private const float SpeedSmoothing = 8f;
    private const float MaxStepPerFrame = 20f;

    private readonly List<RacerState> _racers = new List<RacerState>();
    private readonly HashSet<IEntity> _registrySet = new HashSet<IEntity>();
    private readonly StringBuilder _sb = new StringBuilder(256);

    private IEntityRegistry<IEntity> _registry;
    private Vechicle _playerVehicle;

    private float _finishT;
    private Vector3 _finishPosition;
    private Vector3 _finishForward;
    private float _finishArriveT;
    private float _updateTimer;
    private float _elapsedSinceTick;
    private bool _countdownActive;
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
        if (_finishWindow != null)
            _finishWindow.SetActive(false);

        _finishT = CalculateFinishT();
        CacheFinishLine();

        SpawnGates();
        SyncWithRegistry();
        HoldRacersAtCurrentPositions();

        yield return Countdown();

        _updateTimer = _updateInterval;
        _elapsedSinceTick = 0f;
        _started = true;
        _raceEvents?.RaiseRaceStarted();
    }

    private void Update()
    {
        if (_countdownActive)
            HoldRacersAtCurrentPositions();

        if (!_started || _finished) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        AccumulateMovement(dt);

        _updateTimer += dt;
        if (_updateTimer >= _updateInterval)
        {
            float tickDt = _elapsedSinceTick;
            _updateTimer %= _updateInterval;
            _elapsedSinceTick = 0f;

            SyncWithRegistry();

            if (_racers.Count > 0)
            {
                UpdateProgress();
                ApplyTelemetryTick(tickDt);
                UpdateStandings();
            }
        }

        if (_racers.Count > 0)
            CheckFinish();
    }

    private void SpawnGates()
    {
        if (_track.Spline.Count < MinKnots) return;

        SpawnGate(_startPrefab, CalculateStartT());
        SpawnGate(_finishPrefab, _finishT);
    }

    private void SpawnGate(GameObject prefab, float t)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Префаб ворот не назначен в Race", this);
            return;
        }

        if (!_track.Spline.Evaluate(t, out float3 position, out float3 tangent, out float3 up))
        {
            Debug.LogWarning($"Не удалось посчитать точку сплайна при t={t}", this);
            return;
        }

        Vector3 worldPosition = _track.transform.TransformPoint(position);
        Vector3 worldForward = _track.transform.TransformDirection(tangent);
        Vector3 worldUp = _track.transform.TransformDirection(up);

        GameObject gate = Instantiate(prefab, worldPosition, Quaternion.LookRotation(worldForward, worldUp));
        Debug.Log($"Ворота {gate.name} созданы в {worldPosition}", gate);
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
        _countdownActive = true;
        GameState.Pause();
        HoldRacersAtCurrentPositions();

        if (_countdownText != null)
            _countdownText.gameObject.SetActive(true);

        for (int i = _countdownFrom; i > 0; i--)
        {
            if (_countdownText != null)
                _countdownText.text = i.ToString();

            HoldRacersAtCurrentPositions();
            yield return new WaitForSecondsRealtime(1f);
        }

        if (_countdownText != null)
            _countdownText.gameObject.SetActive(false);

        _countdownActive = false;
        GameState.Resume();
    }

    private void HoldRacersAtCurrentPositions()
    {
        foreach (RacerState racer in _racers)
        {
            if (racer.Entity == null || racer.Entity.Transform == null) continue;

            Vector3 pos = racer.Entity.Transform.position;
            if (!racer.HasLastPosition)
            {
                racer.LastPosition = pos;
                racer.HasLastPosition = true;
            }
            else
            {
                racer.Entity.Transform.position = racer.LastPosition;
            }

            Rigidbody rb = racer.Entity.Transform.GetComponentInChildren<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private float CalculateStartT()
    {
        int curveCount = _track.Spline.Count - 1;
        return (float)StartKnot / curveCount;
    }

    private float CalculateFinishT()
    {
        Spline spline = _track.Spline;

        if (spline.Count < MinKnots)
        {
            Debug.LogError($"В сплайне должно быть минимум {MinKnots} узла", this);
            return 1f;
        }

        int curveCount = spline.Count - 1;
        int finishKnot = spline.Count - 2;

        return (float)finishKnot / curveCount;
    }

    private void UpdateProgress()
    {
        foreach (RacerState racer in _racers)
        {
            float t = GetProgress(racer.Entity.Transform.position);

            if (!racer.HasTrackedProgress || Mathf.Abs(t - racer.Progress) <= _maxProgressStep)
            {
                racer.Progress = t;
                racer.HasTrackedProgress = true;
            }
        }
    }

    private void AccumulateMovement(float dt)
    {
        _elapsedSinceTick += dt;

        foreach (RacerState racer in _racers)
        {
            if (racer.Entity == null || racer.Entity.Transform == null) continue;

            Vector3 position = racer.Entity.Transform.position;

            if (racer.HasLastPosition)
            {
                float step = Vector3.Distance(position, racer.LastPosition);
                if (step <= MaxStepPerFrame)
                {
                    racer.DistanceMeters += step;
                    racer.IntervalDistanceMeters += step;
                }
            }

            racer.LastPosition = position;
            racer.HasLastPosition = true;
        }
    }

    private void ApplyTelemetryTick(float tickDt)
    {
        if (tickDt <= 0f) return;

        float smoothing = 1f - Mathf.Exp(-SpeedSmoothing * tickDt);

        foreach (RacerState racer in _racers)
        {
            float rawKmh = (racer.IntervalDistanceMeters / tickDt) * 3.6f;
            racer.SpeedKmh = Mathf.Lerp(racer.SpeedKmh, rawKmh, smoothing);
            racer.IntervalDistanceMeters = 0f;
        }
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
        {
            _racers[i].Place = i + 1;
            _racers[i].IsPlayer = _playerVehicle != null && ReferenceEquals(_racers[i].Entity, _playerVehicle);
        }

        UpdateStandingsUI();
        _raceEvents?.RaiseStandingsUpdated(_racers);
    }

    private void UpdateStandingsUI()
    {
        if (_standingsText == null) return;

        _sb.Clear();
        _sb.Append("<size=70%><color=#AAAAAA>")
           .Append("<pos=0%>#<pos=8%>Имя<pos=62%>км/ч<pos=82%>км")
           .Append("</color></size>\n");

        for (int i = 0; i < _racers.Count; i++)
        {
            if (i > 0) _sb.Append('\n');

            bool isLeader = i == 0;
            bool isPlayer = _racers[i].IsPlayer;

            RacerState racer = _racers[i];

            string line = $"<pos=0%>{i + 1}<pos=8%>{racer.Name ?? "—"}{(isPlayer ? " (Вы)" : string.Empty)}" +
                          $"<pos=62%>{racer.SpeedKmh:F0}<pos=82%>{racer.DistanceKm:F2}";

            if (isPlayer)
                _sb.Append("<color=#FFD700>").Append(line).Append("</color>");
            else if (isLeader)
                _sb.Append("<color=#00FF00>").Append(line).Append("</color>");
            else
                _sb.Append(line);
        }

        _standingsText.text = _sb.ToString();
    }

    private void CacheFinishLine()
    {
        if (_track.Spline.Count < MinKnots) return;

        if (!_track.Spline.Evaluate(_finishT, out float3 position, out float3 tangent, out _)) return;

        _finishPosition = _track.transform.TransformPoint(position);
        _finishForward = _track.transform.TransformDirection(tangent).normalized;
        _finishArriveT = _finishT - 0.5f / (_track.Spline.Count - 1);
    }

    private void CheckFinish()
    {
        foreach (RacerState racer in _racers)
        {
            Vector3 offset = racer.Entity.Transform.position - _finishPosition;
            float side = Vector3.Dot(offset, _finishForward);

            bool crossedForward = racer.HasFinishSide && racer.FinishSide < 0f && side >= 0f;
            racer.FinishSide = side;
            racer.HasFinishSide = true;

            if (!crossedForward || racer.Progress < _finishArriveT) continue;

            Vector3 lateral = offset - _finishForward * side;
            if (lateral.magnitude > _finishHalfWidth) continue;

            Finish(racer);
            return;
        }
    }

    private void Finish(RacerState winner)
    {
        _finished = true;
        winner.Finished = true;

        _racers.Remove(winner);
        _racers.Insert(0, winner);

        for (int i = 0; i < _racers.Count; i++)
        {
            _racers[i].Place = i + 1;
            _racers[i].IsPlayer = _playerVehicle != null && ReferenceEquals(_racers[i].Entity, _playerVehicle);
        }

        UpdateStandingsUI();
        _raceEvents?.RaiseRaceFinished(winner, _racers);

        if (_resultsText != null && _standingsText != null)
            _resultsText.text = _standingsText.text;

        LogResults();

        if (_finishWindow != null)
        {
            _finishWindow.SetActive(true);
            CanvasGroup group = _finishWindow.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
        }

        UnlockCursor();
        GameState.Pause();
    }

    private void LogResults()
    {
        foreach (RacerState racer in _racers)
            Debug.Log($"{racer.Place}. {racer.Name} | {racer.DistanceKm:F2} км | {racer.SpeedKmh:F0} км/ч");
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_track == null || _track.Spline == null || _track.Spline.Count < MinKnots) return;

        int curveCount = _track.Spline.Count - 1;
        int finishKnot = _track.Spline.Count - 2;

        DrawMarker(CalculateStartT(), Color.green, "START");
        DrawMarker((float)finishKnot / curveCount, Color.red, "FINISH");
    }

    private void DrawMarker(float t, Color color, string label)
    {
        if (!_track.Spline.Evaluate(t, out float3 position, out float3 tangent, out _)) return;

        Vector3 point = _track.transform.TransformPoint(position);
        Vector3 forward = _track.transform.TransformDirection(tangent).normalized;

        Gizmos.color = color;
        Gizmos.DrawSphere(point, 1.5f);
        Gizmos.DrawLine(point, point + Vector3.up * 18f);
        Gizmos.DrawRay(point, forward * 10f);

        UnityEditor.Handles.Label(point + Vector3.up * 19f, label);
    }
#endif

    private void OnDestroy()
    {
        GameState.Resume();
    }
}