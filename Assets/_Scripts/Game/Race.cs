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

    [Tooltip("Насколько далеко от сплайна может быть гонщик, чтобы участвовать")]
    [SerializeField] private float _maxDistanceFromTrack = 50f;

    [Tooltip("Половина ширины линии финиша: дальше от центра финиш не засчитывается")]
    [SerializeField] private float _finishHalfWidth = 13f;

    [Tooltip("Максимальный скачок прогресса за кадр. Больше этого считается прыжком на другой участок трассы и игнорируется")]
    [SerializeField] private float _maxProgressStep = 0.1f;

    [Header("UI")]
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField] private GameObject _finishWindow;
    [SerializeField] private TMP_Text _standingsText;
    [SerializeField] private TMP_Text _resultsText;      // необязательно: итоговая таблица в окне финиша

    // Старт = узел с индексом 1 (до него разгонный отрезок), финиш = предпоследний узел.
    // Чтобы финиш стоял после старта, нужно минимум 4 узла.
    private const int StartKnot = 1;
    private const int MinKnots = 4;

    // Телеметрия: 1 юнит = 1 метр.
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
        CacheFinishLine();

        SpawnGates();

        yield return Countdown();

        _started = true;
    }

    private void Update()
    {
        if (!_started || _finished) return;

        SyncWithRegistry();

        if (_racers.Count == 0) return;

        UpdateProgress();
        UpdateTelemetry();
        UpdateStandings();
        CheckFinish();
    }

    // Старт = первый узел сплайна, финиш = предпоследний (тот же, что в CalculateFinishT).
    private void SpawnGates()
    {
        if (_track.Spline.Count < MinKnots) return;

        SpawnGate(_startPrefab, CalculateStartT());
        SpawnGate(_finishPrefab, _finishT);
    }

    // Ворота смотрят вдоль направления трассы: ось Z модели = направление движения.
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

    // Прогресс меняется только плавно. Скачок (задом через арку, участок трассы рядом
    // по прямой) игнорируется, поэтому такой манёвр прогресс не даёт.
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

    // Скорость и пробег считаются по реальному перемещению участника.
    private void UpdateTelemetry()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float smoothing = 1f - Mathf.Exp(-SpeedSmoothing * dt);

        foreach (RacerState racer in _racers)
        {
            Vector3 position = racer.Entity.Transform.position;

            if (racer.HasLastPosition)
            {
                float step = Vector3.Distance(position, racer.LastPosition);

                // Телепорт или респавн в пробег и скорость не идут.
                if (step <= MaxStepPerFrame)
                {
                    racer.DistanceMeters += step;

                    float rawKmh = step / dt * 3.6f;
                    racer.SpeedKmh = Mathf.Lerp(racer.SpeedKmh, rawKmh, smoothing);
                }
            }

            racer.LastPosition = position;
            racer.HasLastPosition = true;
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
            _racers[i].Place = i + 1;

        UpdateStandingsUI();
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
            bool isPlayer = _playerVehicle != null && ReferenceEquals(_racers[i].Entity, _playerVehicle);

            RacerState racer = _racers[i];

            string line = $"<pos=0%>{i + 1}<pos=8%>{racer.Name ?? "—"}{(isPlayer ? " (Вы)" : string.Empty)}" +
                          $"<pos=62%>{racer.SpeedKmh:F0}<pos=82%>{racer.DistanceKm:F2}";

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

    // Линия финиша: точка предпоследнего узла и направление трассы в ней.
    private void CacheFinishLine()
    {
        if (_track.Spline.Count < MinKnots) return;

        if (!_track.Spline.Evaluate(_finishT, out float3 position, out float3 tangent, out _)) return;

        _finishPosition = _track.transform.TransformPoint(position);
        _finishForward = _track.transform.TransformDirection(tangent).normalized;

        // Финиш засчитывается, только если участник реально доехал до последнего отрезка перед линией.
        _finishArriveT = _finishT - 0.5f / (_track.Spline.Count - 1);
    }

    // Финиш засчитывается только при пересечении линии по ходу трассы:
    // из «до линии» (side < 0) в «после линии» (side >= 0). Задним ходом сторона
    // меняется в обратную сторону и ничего не даёт, ближайшая точка сплайна не важна.
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
            _racers[i].Place = i + 1;

        UpdateStandingsUI();

        if (_resultsText != null && _standingsText != null)
            _resultsText.text = _standingsText.text;

        LogResults();

        _finishWindow.SetActive(true);
        UnlockCursor();
        StopTime();
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
    // Видно в Scene без запуска игры: зелёный = старт, красный = финиш.
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

    private void StartTime() => Time.timeScale = 1f;
    private void StopTime() => Time.timeScale = 0f;

    private void OnDestroy()
    {
        StartTime();
    }
}