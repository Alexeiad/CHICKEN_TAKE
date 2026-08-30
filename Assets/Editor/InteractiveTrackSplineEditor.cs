using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[CustomEditor(typeof(SplineContainer))]
public class InteractiveTrackSplineEditor : Editor
{
    private SplineContainer container;
    private int selectedSplineIndex = 0;

    // Настройки прижимания к мешу
    private bool snapToMesh = true;
    private float heightOffset = 0.1f; // Небольшой отступ вверх, чтобы линия не проваливалась в текстуру
    private LayerMask groundLayer = ~0; // Все слои по умолчанию

    private void OnEnable()
    {
        container = (SplineContainer)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Управление Мульти-Сплайнами", EditorStyles.boldLabel);

        if (container.Splines == null || container.Splines.Count == 0)
        {
            if (GUILayout.Button("+ Добавить первый сплайн", GUILayout.Height(30)))
            {
                Undo.RecordObject(container, "Add Spline");
                container.AddSpline(new Spline());
                EditorUtility.SetDirty(container);
            }
            return;
        }

        // Выбор сплайна
        string[] options = new string[container.Splines.Count];
        for (int i = 0; i < container.Splines.Count; i++)
        {
            options[i] = $"Сплайн [{i}] (Точек: {container.Splines[i].Count})";
        }

        selectedSplineIndex = EditorGUILayout.Popup("Редактируемый сплайн:", selectedSplineIndex, options);
        selectedSplineIndex = Mathf.Clamp(selectedSplineIndex, 0, container.Splines.Count - 1);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Новый сплайн", GUILayout.Height(25)))
        {
            Undo.RecordObject(container, "Add Spline");
            var newSpline = new Spline();
            newSpline.Add(new BezierKnot(new float3(0, 0, 0)));
            newSpline.Add(new BezierKnot(new float3(0, 0, 10)));
            container.AddSpline(newSpline);
            selectedSplineIndex = container.Splines.Count - 1;
            EditorUtility.SetDirty(container);
        }

        if (container.Splines.Count > 1)
        {
            if (GUILayout.Button("Удалить текущий", GUILayout.Height(25)))
            {
                Undo.RecordObject(container, "Remove Spline");
                container.RemoveSplineAt(selectedSplineIndex);
                selectedSplineIndex = Mathf.Clamp(selectedSplineIndex - 1, 0, container.Splines.Count - 1);
                EditorUtility.SetDirty(container);
            }
        }
        EditorGUILayout.EndHorizontal();

        var activeSpline = container.Splines[selectedSplineIndex];
        bool isClosed = activeSpline.Closed;
        bool newClosed = EditorGUILayout.Toggle("Замкнуть трассу (Loop)", isClosed);
        if (newClosed != isClosed)
        {
            Undo.RecordObject(container, "Toggle Loop");
            activeSpline.Closed = newClosed;
            EditorUtility.SetDirty(container);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Авто-привязка к Мешу (Terrain / Road)", EditorStyles.boldLabel);
        snapToMesh = EditorGUILayout.Toggle("Прижимать к мешу (Raycast Y)", snapToMesh);
        heightOffset = EditorGUILayout.FloatField("Смещение над мешем (Y)", heightOffset);

        if (GUILayout.Button("Прижать весь сплайн к поверхности", GUILayout.Height(25)))
        {
            SnapAllKnotsToSurface(activeSpline);
        }

        EditorGUILayout.HelpBox(
            "Инструкция в Scene View:\n" +
            "• Тяни за КРАСНЫЕ точки — перемещение узлов (авто-проекция Y на меш).\n" +
            "• КЛИК по ЖЕЛТОМУ квадрату — вставить новую точку посередине.\n" +
            "• Shift + Клик по красной точке — удалить точку.",
            MessageType.Info
        );
    }

    private void OnSceneGUI()
    {
        if (container == null || container.Splines == null || container.Splines.Count == 0) return;
        if (selectedSplineIndex >= container.Splines.Count) return;

        Spline spline = container.Splines[selectedSplineIndex];
        Matrix4x4 localToWorld = container.transform.localToWorldMatrix;

        // 1. Отрисовка фоновых неактивных сплайнов
        for (int i = 0; i < container.Splines.Count; i++)
        {
            if (i == selectedSplineIndex) continue;
            DrawSplinePreview(container.Splines[i], localToWorld, new Color(0.2f, 0.6f, 1f, 0.4f), 2f);
        }

        // 2. Отрисовка активного сплайна
        DrawSplinePreview(spline, localToWorld, Color.green, 4f);

        int knotCount = spline.Count;
        if (knotCount == 0) return;

        Event e = Event.current;

        // 3. ПЕРЕМЕЩЕНИЕ И УДАЛЕНИЕ СУЩЕСТВУЮЩИХ ТОЧЕК
        for (int i = 0; i < knotCount; i++)
        {
            BezierKnot knot = spline[i];
            Vector3 worldPos = localToWorld.MultiplyPoint3x4((Vector3)knot.Position);

            // Shift + Click — Удалить
            if (e.shift && e.type == EventType.MouseDown && e.button == 0)
            {
                if (Vector2.Distance(e.mousePosition, HandleUtility.WorldToGUIPoint(worldPos)) < 15f)
                {
                    if (knotCount > 2)
                    {
                        Undo.RecordObject(container, "Remove Knot");
                        spline.RemoveAt(i);
                        EditorUtility.SetDirty(container);
                        e.Use();
                        break;
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.FreeMoveHandle(
                worldPos,
                HandleUtility.GetHandleSize(worldPos) * 0.12f,
                Vector3.zero,
                Handles.CircleHandleCap
            );

            Handles.Label(worldPos + Vector3.up * 0.5f, $"P{i}", EditorStyles.boldLabel);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(container, "Move Knot");

                // Проекция Y на меш под точкой
                if (snapToMesh)
                {
                    newWorldPos = GetSurfacePosition(newWorldPos);
                }

                Vector3 localPos = container.transform.InverseTransformPoint(newWorldPos);
                knot.Position = localPos;
                spline[i] = knot;

                AutoSmoothTangents(spline, i);
                EditorUtility.SetDirty(container);
            }
        }

        // 4. ВСТАВКА НОВОЙ ТОЧКИ (По одиночному клику на желтый квадрат)
        int segmentCount = spline.Closed ? knotCount : knotCount - 1;
        for (int i = 0; i < segmentCount; i++)
        {
            int nextIndex = (i + 1) % knotCount;

            Vector3 pA = localToWorld.MultiplyPoint3x4((Vector3)spline[i].Position);
            Vector3 pB = localToWorld.MultiplyPoint3x4((Vector3)spline[nextIndex].Position);
            Vector3 midPoint = Vector3.Lerp(pA, pB, 0.5f);

            if (snapToMesh)
            {
                midPoint = GetSurfacePosition(midPoint);
            }

            Handles.color = Color.yellow;
            float midHandleSize = HandleUtility.GetHandleSize(midPoint) * 0.1f;

            // Используем Handles.Button вместо FreeMoveHandle, чтобы сработка была строго при КЛИКЕ
            if (Handles.Button(midPoint, Quaternion.identity, midHandleSize, midHandleSize, Handles.RectangleHandleCap))
            {
                Undo.RecordObject(container, "Insert Knot");

                Vector3 localMidPos = container.transform.InverseTransformPoint(midPoint);
                BezierKnot newKnot = new BezierKnot(localMidPos);

                spline.Insert(nextIndex, newKnot);

                AutoSmoothTangents(spline, nextIndex);
                AutoSmoothTangents(spline, i);
                AutoSmoothTangents(spline, (nextIndex + 1) % spline.Count);

                EditorUtility.SetDirty(container);
                break; // Выходим из цикла мгновенно после вставки
            }
        }
    }

    // Метод Raycast для поиска поверхности меша под точкой
    private Vector3 GetSurfacePosition(Vector3 originalWorldPos)
    {
        // Пускаем луч сверху вниз
        Ray ray = new Ray(originalWorldPos + Vector3.up * 50f, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, 150f, groundLayer))
        {
            return hit.point + Vector3.up * heightOffset;
        }

        return originalWorldPos;
    }

    private void SnapAllKnotsToSurface(Spline spline)
    {
        Undo.RecordObject(container, "Snap Spline To Surface");
        Matrix4x4 localToWorld = container.transform.localToWorldMatrix;

        for (int i = 0; i < spline.Count; i++)
        {
            BezierKnot knot = spline[i];
            Vector3 worldPos = localToWorld.MultiplyPoint3x4((Vector3)knot.Position);
            Vector3 snappedWorldPos = GetSurfacePosition(worldPos);

            knot.Position = container.transform.InverseTransformPoint(snappedWorldPos);
            spline[i] = knot;
            AutoSmoothTangents(spline, i);
        }

        EditorUtility.SetDirty(container);
    }

    private void AutoSmoothTangents(Spline spline, int index)
    {
        int count = spline.Count;
        if (count < 3) return;

        int prevIdx = (index - 1 + count) % count;
        int nextIdx = (index + 1) % count;

        if (!spline.Closed && (index == 0 || index == count - 1)) return;

        Vector3 pPrev = (Vector3)spline[prevIdx].Position;
        Vector3 pNext = (Vector3)spline[nextIdx].Position;

        Vector3 dir = (pNext - pPrev).normalized;
        float dist = Vector3.Distance(pPrev, pNext) * 0.2f;

        BezierKnot knot = spline[index];
        knot.TangentIn = (float3)(-dir * dist);
        knot.TangentOut = (float3)(dir * dist);
        spline[index] = knot;
    }

    private void DrawSplinePreview(Spline spline, Matrix4x4 localToWorld, Color color, float width)
    {
        Handles.color = color;
        int stepCount = 30 * Mathf.Max(1, spline.Count);

        float3 startPos = SplineUtility.EvaluatePosition(spline, 0f);
        Vector3 prevPoint = localToWorld.MultiplyPoint3x4((Vector3)startPos);

        for (int i = 1; i <= stepCount; i++)
        {
            float t = (float)i / stepCount;
            float3 evalPos = SplineUtility.EvaluatePosition(spline, t);
            Vector3 currentPoint = localToWorld.MultiplyPoint3x4((Vector3)evalPos);

            Handles.DrawLine(prevPoint, currentPoint, width);
            prevPoint = currentPoint;
        }
    }
}