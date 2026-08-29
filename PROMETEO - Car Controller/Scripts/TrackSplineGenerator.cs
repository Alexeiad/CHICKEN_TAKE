using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[RequireComponent(typeof(SplineContainer))]
public class TrackSplineGenerator : MonoBehaviour
{
    [Header("Track Guide Points")]
    [Tooltip("Точки трассы, по которым построится маршрут")]
    [SerializeField] private Transform[] trackGuidePoints;

    [Header("Spline Settings")]
    [SerializeField] private bool loopTrack = true;

    [Header("NavMesh Ground Snapping (Привязка к Y)")]
    [Tooltip("Автоматически брать высоту Y с запеченного NavMesh, сохраняя XZ координаты точек")]
    [SerializeField] private bool snapToNavMeshY = true;

    [Tooltip("Радиус поиска поверхности NavMesh вокруг вашей точки")]
    [SerializeField] private float sampleRadius = 15f;

    [Tooltip("Небольшой отступ по Y над дорогой (в метрах), чтобы линия не утопала в асфальте")]
    [SerializeField] private float heightOffset = 0.05f;

    [Header("Curvature Control (Контроль изгибов)")]
    [Tooltip("Натяжение сплайна (0 = прямые отрезки, 0.2-0.3 = идеальные сглаженные повороты)")]
    [Range(0f, 1f)]
    [SerializeField] private float curveTension = 0.3f;

    [Tooltip("Угол (в градусах), ниже которого участок считается прямой линией")]
    [SerializeField] private float straightAngleThreshold = 10f;

    [ContextMenu("Generate Spline From Points (With NavMesh Y)")]
    public void GenerateSpline()
    {
        if (trackGuidePoints == null || trackGuidePoints.Length < 2)
        {
            Debug.LogError("[SplineGen] Укажи минимум 2 точки в массиве Track Guide Points!");
            return;
        }

        SplineContainer container = GetComponent<SplineContainer>();

#if UNITY_EDITOR
        Undo.RecordObject(container, "Generate Spline");
#endif

        Spline spline = container.Spline;
        spline.Clear();

        int createdKnots = 0;

        for (int i = 0; i < trackGuidePoints.Length; i++)
        {
            Transform point = trackGuidePoints[i];
            if (point == null) continue;

            Vector3 finalWorldPos = point.position;

            // 1. АВТО-ПРИМАГНИЧИВАНИЕ ВЫСОТЫ Y К NAVMESH
            if (snapToNavMeshY)
            {
                if (NavMesh.SamplePosition(point.position, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                {
                    // Сохраняем твои X и Z, а Y берем строго с поверхности NavMesh + небольшой отступ
                    finalWorldPos = new Vector3(point.position.x, hit.position.y + heightOffset, point.position.z);
                }
                else
                {
                    Debug.LogWarning($"[SplineGen] Точка '{point.name}' находится слишком далеко от NavMesh! Использована ее исходная позиция по Y.");
                }
            }

            Vector3 localPos = container.transform.InverseTransformPoint(finalWorldPos);
            BezierKnot knot = new BezierKnot(new float3(localPos.x, localPos.y, localPos.z));
            spline.Add(knot, TangentMode.AutoSmooth);
            createdKnots++;
        }

        spline.Closed = loopTrack;

        // 2. НАСТРОЙКА НАТЯЖЕНИЯ И АВТО-ВЫПРЯМЛЕНИЯ ПРЯМЫХ
        int totalKnots = spline.Count;
        for (int i = 0; i < totalKnots; i++)
        {
            int prevIndex = (i - 1 + totalKnots) % totalKnots;
            int nextIndex = (i + 1) % totalKnots;

            Transform prevTransform = trackGuidePoints[prevIndex];
            Transform currentTransform = trackGuidePoints[i];
            Transform nextTransform = trackGuidePoints[nextIndex];

            if (prevTransform == null || currentTransform == null || nextTransform == null) continue;

            TrackWaypoint waypoint = currentTransform.GetComponent<TrackWaypoint>();
            bool forceLinear = waypoint != null && waypoint.isLinear;

            Vector3 dirIn = (currentTransform.position - prevTransform.position).normalized;
            Vector3 dirOut = (nextTransform.position - currentTransform.position).normalized;
            float angle = Vector3.Angle(dirIn, dirOut);

            if (forceLinear || angle < straightAngleThreshold || curveTension <= 0.01f)
            {
                spline.SetTangentMode(i, TangentMode.Linear);
            }
            else
            {
                spline.SetTangentMode(i, TangentMode.AutoSmooth);
                BezierKnot knot = spline[i];
                knot.TangentIn *= curveTension;
                knot.TangentOut *= curveTension;
                spline[i] = knot;
            }
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(container);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        SceneView.RepaintAll();

        Debug.Log($"[SplineGen] Готово! Построено {createdKnots} узлов. Высота Y идеально привязана к NavMesh.");
#endif
    }
}