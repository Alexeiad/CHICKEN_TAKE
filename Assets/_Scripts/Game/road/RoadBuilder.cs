using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Строит меш дороги вдоль сплайна из SplineContainer (на этом же объекте или в поле Container).
//  - полотно дороги (материал 0)
//  - если дорога выше земли: насыпь с откосами до террейна (материал 2, плиты-ромбы)
//  - если слишком высоко или над другой дорогой: мост с плитой, барьерами и одиночными опорами (материал 1, бетон)
// Фрейм дороги считается от мирового «вверх», поэтому дорогу не выворачивает наизнанку.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoadBuilder : MonoBehaviour
{
    public SplineContainer container;

    [Header("Дорога")]
    [Min(4f)] public float roadWidth = 14f;
    [Min(0.5f)] public float sectionStep = 2.5f;
    [Tooltip("Длина одного повтора текстуры асфальта вдоль дороги, м")]
    [Min(0.1f)] public float roadTileLength = 1f;

    [Header("Виражи (наклон в поворотах)")]
    public bool banking = true;
    [Tooltip("Наклон в повороте радиусом 100 м, градусы. Чем круче поворот, тем больше наклон")]
    [Range(0f, 15f)] public float bankAtRadius100 = 6f;
    [Range(0f, 20f)] public float maxBank = 10f;
    [Tooltip("Насколько плавно наклон входит в поворот и выходит из него, м")]
    [Min(5f)] public float bankSmoothing = 40f;

    [Header("Материалы")]
    public Material roadMaterial;
    public Material concreteMaterial;
    public Material slopeMaterial;
    [Min(0.5f)] public float concreteTile = 4f;
    [Min(0.5f)] public float slopeTile = 4f;

    [Header("Земля")]
    [Tooltip("Если пусто, берётся активный Terrain. Если террейна нет, землёй считается высота Ground Level")]
    public Terrain terrain;
    public float groundLevel = 0f;
    [Tooltip("Для команды Snap Spline To Terrain: насколько дорога выше земли, м")]
    public float snapLift = 0.2f;

    [Header("Прокопка холма (кнопка в инспекторе)")]
    [Tooltip("Запас ровного дна выемки по бокам дороги, м")]
    [Min(0f)] public float carveShoulder = 1f;
    [Tooltip("Удалять деревья террейна, которые попали на дорогу")]
    public bool removeTreesOnRoad = true;

    [Header("Насыпь и откосы")]
    [Tooltip("Дорога выше земли меньше чем на столько, считается лежащей на земле, м")]
    [Min(0f)] public float embankmentMinGap = 0.2f;
    [Tooltip("Выше этого начинается мост вместо насыпи, м")]
    [Min(1f)] public float embankmentMaxHeight = 9f;
    [Tooltip("Крутизна откоса: метров по горизонтали на 1 м высоты")]
    [Min(0.5f)] public float slopeRatio = 1.5f;
    [Min(5f)] public float maxSlopeWidth = 60f;

    [Header("Мост")]
    [Min(0.3f)] public float deckThickness = 1.2f;
    [Min(0f)] public float barrierHeight = 0.9f;
    [Min(0.1f)] public float barrierWidth = 0.45f;
    [Tooltip("Мосты короче этого превращаются в насыпь (если не над другой дорогой), м")]
    [Min(10f)] public float minBridgeLength = 30f;
    [Tooltip("Насколько мост продлевается за пересечение с другой дорогой, м")]
    [Min(0f)] public float bridgeMargin = 15f;

    [Header("Опоры")]
    public bool pillars = true;
    [Min(8f)] public float pillarSpacing = 28f;
    [Min(0.3f)] public float pillarRadius = 1.1f;
    [Tooltip("Опоры ставятся только если под мостом больше этой высоты, м")]
    [Min(1f)] public float pillarMinHeight = 3f;
    [Tooltip("Поперечная балка-ригель на опоре. 0 в ширине = без ригеля")]
    [Min(0f)] public float capWidth = 7f;
    [Min(0f)] public float capHeight = 1.2f;
    [Min(0.5f)] public float capDepth = 2.4f;

    [Header("Прочее")]
    public bool autoRebuild = true;
    public bool addMeshCollider = true;

    Mesh _mesh;
    bool _dirty;
    Terrain _terr;

    // рабочие данные
    int _m;
    Vector3[] _pos, _fwd, _right, _rightH, _up, _edgeL, _edgeR;
    float[] _s;
    float _total;

    readonly List<Vector3> _v = new List<Vector3>();
    readonly List<Vector2> _uv = new List<Vector2>();
    readonly List<int> _triRoad = new List<int>();
    readonly List<int> _triConc = new List<int>();
    readonly List<int> _triSlope = new List<int>();

    // ------------------------------------------------------------------ жизненный цикл

    void OnEnable()
    {
        if (container == null)
            container = GetComponent<SplineContainer>();
        Spline.Changed += OnSplineChanged;
        if (autoRebuild)
            RequestRebuild();
    }

    void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
#if UNITY_EDITOR
        EditorApplication.delayCall -= DelayedRebuild;
#endif
    }

    void OnValidate()
    {
        if (autoRebuild)
            RequestRebuild();
    }

    void LateUpdate()
    {
        if (_dirty)
        {
            _dirty = false;
            Rebuild();
        }
    }

    void OnSplineChanged(Spline s, int knotIndex, SplineModification mod)
    {
        if (autoRebuild && container != null && s == container.Spline)
            RequestRebuild();
    }

    // Сплайн при генерации меняется десятки раз подряд, поэтому пересборка откладывается и склеивается в одну.
    void RequestRebuild()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= DelayedRebuild;
            EditorApplication.delayCall += DelayedRebuild;
            return;
        }
#endif
        _dirty = true;
    }

#if UNITY_EDITOR
    void DelayedRebuild()
    {
        if (this == null || !enabled)
            return;
        Rebuild();
    }
#endif

    // ------------------------------------------------------------------ земля

    float Ground(Vector3 w)
    {
        if (_terr != null)
            return _terr.SampleHeight(w) + _terr.GetPosition().y;
        return groundLevel;
    }

    // Точка, где откос (наклонная плоскость от края дороги) встречается с землёй.
    Vector3 Contact(Vector3 edge, Vector3 outDir)
    {
        float gap0 = edge.y - Ground(edge);
        if (gap0 <= 0.01f)
            return edge;

        float step = Mathf.Max(0.5f, maxSlopeWidth / 120f);
        float prevD = 0f, prevDiff = gap0;
        for (float d = step; d <= maxSlopeWidth + 1e-3f; d += step)
        {
            Vector3 q = edge + outDir * d;
            float slopeY = edge.y - d / slopeRatio;
            float diff = slopeY - Ground(q);
            if (diff <= 0f)
            {
                float t = prevDiff / (prevDiff - diff);
                float dd = prevD + t * (d - prevD);
                Vector3 c = edge + outDir * dd;
                c.y = edge.y - dd / slopeRatio - 0.1f; // чуть утоплен в землю, чтобы не было щели
                return c;
            }
            prevD = d;
            prevDiff = diff;
        }
        Vector3 e = edge + outDir * maxSlopeWidth;
        e.y = edge.y - maxSlopeWidth / slopeRatio;
        return e;
    }

    // ------------------------------------------------------------------ сборка

    [ContextMenu("Rebuild Road")]
    public void Rebuild()
    {
        if (container == null)
            container = GetComponent<SplineContainer>();
        if (container == null || container.Splines.Count == 0)
            return;
        Spline spline = container.Spline;
        if (spline == null || spline.Count < 2)
            return;

        _terr = terrain != null ? terrain : Terrain.activeTerrain;

        float length = container.CalculateLength(0);
        if (length < 1f)
            return;
        bool closed = spline.Closed;

        int N = Mathf.Clamp(Mathf.CeilToInt(length / sectionStep), 8, 20000);
        int M = N + 1;
        _m = M;

        // ---- сечения вдоль сплайна
        _pos = new Vector3[M];
        _fwd = new Vector3[M];
        _right = new Vector3[M];
        _up = new Vector3[M];
        _rightH = new Vector3[M];
        _edgeL = new Vector3[M];
        _edgeR = new Vector3[M];
        _s = new float[M];

        for (int i = 0; i < M; i++)
        {
            container.Evaluate(0, (float)i / N, out var p, out var tan, out var upv);
            _pos[i] = (Vector3)p;
        }

        for (int i = 0; i < M; i++)
        {
            int a = i > 0 ? i - 1 : (closed ? M - 2 : 0);
            int b = i < M - 1 ? i + 1 : (closed ? 1 : M - 1);
            Vector3 f = _pos[b] - _pos[a];
            if (f.sqrMagnitude < 1e-8f)
                f = i > 0 ? _fwd[i - 1] : Vector3.forward;
            f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            if (r.sqrMagnitude < 1e-6f)
                r = Vector3.right;
            r.Normalize();
            _fwd[i] = f;
            _right[i] = r;
            _up[i] = Vector3.Cross(f, r).normalized;
            _rightH[i] = r;
            if (i > 0)
                _s[i] = _s[i - 1] + Vector3.Distance(_pos[i], _pos[i - 1]);
        }
        _total = _s[M - 1];
        float avgStep = _total / (M - 1);

        // ---- виражи: наклон по знаку и крутизне поворота (по горизонтали), потом сглаживание
        var bank = new float[M];
        if (banking && bankAtRadius100 > 0.01f)
        {
            int cycle = closed ? M - 1 : M;
            int kk = Mathf.Max(1, Mathf.RoundToInt(8f / avgStep));
            for (int i = 0; i < M; i++)
            {
                int a = i - kk, b = i + kk;
                if (closed)
                {
                    a = ((a % cycle) + cycle) % cycle;
                    b = b % cycle;
                }
                else
                {
                    a = Mathf.Max(0, a);
                    b = Mathf.Min(M - 1, b);
                }
                Vector3 f0 = _fwd[a]; f0.y = 0f;
                Vector3 f1 = _fwd[b]; f1.y = 0f;
                if (f0.sqrMagnitude < 1e-6f || f1.sqrMagnitude < 1e-6f)
                    continue;
                f0.Normalize();
                f1.Normalize();
                float turn = Mathf.Asin(Mathf.Clamp(Vector3.Cross(f0, f1).y, -1f, 1f)); // > 0: поворот направо
                float arc = Mathf.Abs(_s[b] - _s[a]);
                if (closed && arc > _total * 0.5f)
                    arc = _total - arc;
                if (arc < 0.1f)
                    continue;
                bank[i] = Mathf.Clamp(bankAtRadius100 * 100f * (turn / arc), -maxBank, maxBank);
            }

            int sm = Mathf.Max(1, Mathf.RoundToInt(bankSmoothing / avgStep * 0.5f));
            var smoothed = new float[M];
            for (int i = 0; i < M; i++)
            {
                float sum = 0f;
                for (int j = -sm; j <= sm; j++)
                {
                    int q = i + j;
                    q = closed ? ((q % cycle) + cycle) % cycle : Mathf.Clamp(q, 0, M - 1);
                    sum += bank[q];
                }
                smoothed[i] = sum / (2 * sm + 1);
            }
            if (closed)
                smoothed[M - 1] = smoothed[0];
            bank = smoothed;
        }

        for (int i = 0; i < M; i++)
        {
            // наклон к внутренней стороне поворота: нормаль отклоняется в сторону центра
            float phi = bank[i] * Mathf.Deg2Rad;
            Vector3 upB = _up[i] * Mathf.Cos(phi) + _rightH[i] * Mathf.Sin(phi);
            Vector3 rB = Vector3.Cross(upB, _fwd[i]).normalized;
            _right[i] = rB;
            _up[i] = Vector3.Cross(_fwd[i], rB).normalized;
            _edgeL[i] = _pos[i] - rB * (roadWidth * 0.5f);
            _edgeR[i] = _pos[i] + rB * (roadWidth * 0.5f);
        }

        float vTile = closed ? Mathf.Max(1f, Mathf.Round(_total / roadTileLength)) : _total / roadTileLength;
        float vScaleRoad = vTile / _total;

        // ---- высота дороги над землёй
        var gap = new float[M];      // максимум по краям и центру
        var gapC = new float[M];
        var groundC = new float[M];
        for (int i = 0; i < M; i++)
        {
            float gl = Ground(_edgeL[i]);
            float gr = Ground(_edgeR[i]);
            groundC[i] = Ground(_pos[i]);
            gapC[i] = _pos[i].y - groundC[i];
            gap[i] = Mathf.Max(Mathf.Max(_edgeL[i].y - gl, _edgeR[i].y - gr), gapC[i]);
        }

        // ---- где дорога идёт над другой дорогой
        var over = new bool[M];
        float minSep = 3f * roadWidth;
        float hw = roadWidth * 0.9f;
        for (int i = 0; i < M; i++)
        {
            for (int j = 0; j < M; j++)
            {
                if (i == j)
                    continue;
                float dsep = Mathf.Abs(_s[i] - _s[j]);
                if (closed)
                    dsep = Mathf.Min(dsep, _total - dsep);
                if (dsep < minSep)
                    continue;
                float dx = _pos[i].x - _pos[j].x;
                if (dx > hw || dx < -hw)
                    continue;
                float dz = _pos[i].z - _pos[j].z;
                if (dz > hw || dz < -hw)
                    continue;
                if (dx * dx + dz * dz < hw * hw && _pos[i].y - _pos[j].y > 1.5f)
                {
                    over[i] = true;
                    break;
                }
            }
        }
        int marginSteps = Mathf.CeilToInt(bridgeMargin / avgStep);
        bool[] nearOver = Dilate(over, marginSteps);
        bool[] noPillar = Dilate(over, marginSteps + Mathf.CeilToInt(10f / avgStep));

        // ---- режим по сечению: 0 земля, 1 насыпь, 2 мост
        var bridge = new bool[M];
        for (int i = 0; i < M; i++)
            bridge[i] = nearOver[i] || gap[i] > embankmentMaxHeight;

        int minLen = Mathf.CeilToInt(minBridgeLength / avgStep);
        var runS = new List<int>();
        var runE = new List<int>();

        var inv = new bool[M];
        for (int i = 0; i < M; i++)
            inv[i] = !bridge[i];
        Runs(inv, runS, runE);
        for (int k = 0; k < runS.Count; k++)
            if (runS[k] > 0 && runE[k] < M - 1 && runE[k] - runS[k] + 1 <= minLen / 2)
                for (int i = runS[k]; i <= runE[k]; i++)
                    bridge[i] = true;

        Runs(bridge, runS, runE);
        for (int k = 0; k < runS.Count; k++)
        {
            if (runE[k] - runS[k] + 1 >= minLen)
                continue;
            bool keep = false;
            for (int i = runS[k]; i <= runE[k]; i++)
                keep |= nearOver[i];
            if (!keep)
                for (int i = runS[k]; i <= runE[k]; i++)
                    bridge[i] = false;
        }

        var mode = new byte[M];
        for (int i = 0; i < M; i++)
            mode[i] = bridge[i] ? (byte)2 : (gap[i] > embankmentMinGap ? (byte)1 : (byte)0);

        // ---- линии, где откос касается земли
        var cl = new Vector3[M];
        var cr = new Vector3[M];
        for (int i = 0; i < M; i++)
        {
            if (mode[i] == 1)
            {
                cl[i] = Contact(_edgeL[i], -_rightH[i]);
                cr[i] = Contact(_edgeR[i], _rightH[i]);
            }
            else
            {
                cl[i] = _edgeL[i];
                cr[i] = _edgeR[i];
            }
        }

        // ---- геометрия
        _v.Clear();
        _uv.Clear();
        _triRoad.Clear();
        _triConc.Clear();
        _triSlope.Clear();

        // полотно по всей длине
        Sweep(0, M - 1, _triRoad, i => new[] { _edgeL[i], _edgeR[i] }, 1f / roadWidth, vScaleRoad);

        float slopeUv = 1f / slopeTile;
        float concUv = 1f / concreteTile;

        // насыпь
        var starts = new List<int>();
        var ends = new List<int>();
        RunsOfMode(mode, 1, starts, ends);
        for (int k = 0; k < starts.Count; k++)
        {
            int a = starts[k], b = ends[k];
            if (b <= a)
                continue;
            Sweep(a, b, _triSlope, i => new[] { cl[i], _edgeL[i] }, slopeUv, slopeUv);
            Sweep(a, b, _triSlope, i => new[] { _edgeR[i], cr[i] }, slopeUv, slopeUv);
            Cap(_triSlope, new[] { cl[a], _edgeL[a], _edgeR[a], cr[a] }, -_fwd[a], slopeUv);
            Cap(_triSlope, new[] { cl[b], _edgeL[b], _edgeR[b], cr[b] }, _fwd[b], slopeUv);
        }

        // мосты
        Vector3 down = Vector3.down * deckThickness;
        float bw = barrierWidth;
        float bh = barrierHeight;
        RunsOfMode(mode, 2, starts, ends);
        for (int k = 0; k < starts.Count; k++)
        {
            int a = starts[k], b = ends[k];
            if (b <= a)
                continue;

            // плита: правая стенка, низ, левая стенка
            Sweep(a, b, _triConc, i => new[] { _edgeR[i], _edgeR[i] + down, _edgeL[i] + down, _edgeL[i] }, concUv, concUv);
            Cap(_triConc, new[] { _edgeR[a], _edgeR[a] + down, _edgeL[a] + down, _edgeL[a] }, -_fwd[a], concUv);
            Cap(_triConc, new[] { _edgeR[b], _edgeR[b] + down, _edgeL[b] + down, _edgeL[b] }, _fwd[b], concUv);

            if (bh > 0.01f)
            {
                Sweep(a, b, _triConc, i => BarrierRight(i, bw, bh), concUv, concUv);
                Sweep(a, b, _triConc, i => BarrierLeft(i, bw, bh), concUv, concUv);
                Cap(_triConc, BarrierRight(a, bw, bh), -_fwd[a], concUv);
                Cap(_triConc, BarrierRight(b, bw, bh), _fwd[b], concUv);
                Cap(_triConc, BarrierLeft(a, bw, bh), -_fwd[a], concUv);
                Cap(_triConc, BarrierLeft(b, bw, bh), _fwd[b], concUv);
            }

            if (pillars)
            {
                for (int i = a + 1; i <= b; i++)
                {
                    if ((int)(_s[i] / pillarSpacing) == (int)(_s[i - 1] / pillarSpacing))
                        continue;
                    if (noPillar[i] || gapC[i] < pillarMinHeight)
                        continue;
                    AddPillar(i, groundC[i]);
                }
            }
        }

        // ---- в меш (локальные координаты этого объекта)
        for (int i = 0; i < _v.Count; i++)
            _v[i] = transform.InverseTransformPoint(_v[i]);

        if (_mesh == null)
            _mesh = new Mesh { name = "RoadMesh" };
        _mesh.Clear();
        _mesh.indexFormat = IndexFormat.UInt32;
        _mesh.SetVertices(_v);
        _mesh.SetUVs(0, _uv);
        _mesh.subMeshCount = 3;
        _mesh.SetTriangles(_triRoad, 0);
        _mesh.SetTriangles(_triConc, 1);
        _mesh.SetTriangles(_triSlope, 2);
        _mesh.RecalculateNormals();
        _mesh.RecalculateTangents();
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;

        var mr = GetComponent<MeshRenderer>();
        var mats = mr.sharedMaterials;
        if (mats.Length != 3)
            System.Array.Resize(ref mats, 3);
        if (roadMaterial != null) mats[0] = roadMaterial;
        if (concreteMaterial != null) mats[1] = concreteMaterial;
        if (slopeMaterial != null) mats[2] = slopeMaterial;
        mr.sharedMaterials = mats;

        if (addMeshCollider)
        {
            var mc = GetComponent<MeshCollider>();
            if (mc == null)
                mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = null;
            mc.sharedMesh = _mesh;
        }
    }

    Vector3[] BarrierRight(int i, float bw, float bh)
    {
        Vector3 inner = _edgeR[i] - _right[i] * bw;
        return new[] { inner, inner + Vector3.up * bh, _edgeR[i] + Vector3.up * bh, _edgeR[i] };
    }

    Vector3[] BarrierLeft(int i, float bw, float bh)
    {
        Vector3 inner = _edgeL[i] + _right[i] * bw;
        return new[] { _edgeL[i], _edgeL[i] + Vector3.up * bh, inner + Vector3.up * bh, inner };
    }

    // ------------------------------------------------------------------ примитивы

    // Заметание профиля вдоль сечений a..b. Профиль идёт по часовой стрелке, если смотреть вдоль дороги
    // (слева направо по верху, потом вниз, назад справа налево по низу и вверх): тогда нормали смотрят наружу.
    void Sweep(int a, int b, List<int> tris, System.Func<int, Vector3[]> prof, float uScale, float vScale)
    {
        int segCount = prof(a).Length - 1;
        int rows = b - a + 1;
        int baseV = _v.Count;

        for (int i = a; i <= b; i++)
        {
            Vector3[] p = prof(i);
            float u = 0f;
            float v = _s[i] * vScale;
            for (int k = 0; k < segCount; k++)
            {
                _v.Add(p[k]);
                _uv.Add(new Vector2(u, v));
                u += Vector3.Distance(p[k], p[k + 1]) * uScale;
                _v.Add(p[k + 1]);
                _uv.Add(new Vector2(u, v));
            }
        }

        int rowSize = segCount * 2;
        for (int r = 0; r < rows - 1; r++)
        {
            for (int k = 0; k < segCount; k++)
            {
                int A = baseV + r * rowSize + 2 * k;
                int B = A + 1;
                int D = A + rowSize;
                int C = D + 1;
                tris.Add(A); tris.Add(D); tris.Add(C);
                tris.Add(A); tris.Add(C); tris.Add(B);
            }
        }
    }

    // Плоский многоугольник (веером). Порядок обхода сам подбирается так, чтобы нормаль смотрела в outward.
    void Cap(List<int> tris, Vector3[] poly, Vector3 outward, float uvScale)
    {
        int n = poly.Length;
        if (n < 3)
            return;
        int baseV = _v.Count;
        Vector3 axisR = Vector3.Cross(Vector3.up, outward);
        if (axisR.sqrMagnitude < 1e-6f)
            axisR = Vector3.right;
        axisR.Normalize();
        for (int k = 0; k < n; k++)
        {
            _v.Add(poly[k]);
            _uv.Add(new Vector2(Vector3.Dot(poly[k], axisR) * uvScale, poly[k].y * uvScale));
        }
        Vector3 area = Vector3.zero;
        for (int k = 1; k < n - 1; k++)
            area += Vector3.Cross(poly[k] - poly[0], poly[k + 1] - poly[0]);
        bool flip = Vector3.Dot(area, outward) < 0f;
        for (int k = 1; k < n - 1; k++)
        {
            if (!flip)
            {
                tris.Add(baseV); tris.Add(baseV + k); tris.Add(baseV + k + 1);
            }
            else
            {
                tris.Add(baseV); tris.Add(baseV + k + 1); tris.Add(baseV + k);
            }
        }
    }

    void Box(Vector3 c, Vector3 ax, Vector3 ay, Vector3 az, Vector3 half)
    {
        Vector3[] axes = { ax, ay, az };
        float[] h = { half.x, half.y, half.z };
        for (int a = 0; a < 3; a++)
        {
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 n = axes[a] * sg;
                Vector3 u = axes[(a + 1) % 3];
                Vector3 v = axes[(a + 2) % 3];
                float hu = h[(a + 1) % 3];
                float hv = h[(a + 2) % 3];
                Vector3 fc = c + n * h[a];
                Cap(_triConc, new[]
                {
                    fc - u * hu - v * hv,
                    fc + u * hu - v * hv,
                    fc + u * hu + v * hv,
                    fc - u * hu + v * hv
                }, n, 1f / concreteTile);
            }
        }
    }

    void Cylinder(Vector3 bottom, float height, float radius, int sides)
    {
        int baseV = _v.Count;
        float circ = 2f * Mathf.PI * radius;
        for (int k = 0; k <= sides; k++)
        {
            float ang = 2f * Mathf.PI * k / sides;
            Vector3 off = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
            float u = (float)k / sides * circ / concreteTile;
            _v.Add(bottom + off);
            _uv.Add(new Vector2(u, 0f));
            _v.Add(bottom + off + Vector3.up * height);
            _uv.Add(new Vector2(u, height / concreteTile));
        }
        for (int k = 0; k < sides; k++)
        {
            int b0 = baseV + 2 * k;
            int t0 = b0 + 1;
            int b1 = b0 + 2;
            int t1 = b0 + 3;
            _triConc.Add(b0); _triConc.Add(t0); _triConc.Add(t1);
            _triConc.Add(b0); _triConc.Add(t1); _triConc.Add(b1);
        }
    }

    void AddPillar(int i, float ground)
    {
        Vector3 c = _pos[i];
        float top = c.y - deckThickness;
        float bottom = ground - 0.5f;
        float height = top - bottom;
        if (height < 0.5f)
            return;

        Cylinder(new Vector3(c.x, bottom, c.z), height, pillarRadius, 12);

        if (capWidth > 0.01f && capHeight > 0.01f)
        {
            Vector3 fh = new Vector3(_fwd[i].x, 0f, _fwd[i].z);
            if (fh.sqrMagnitude < 1e-6f)
                fh = Vector3.forward;
            fh.Normalize();
            Box(new Vector3(c.x, top - capHeight * 0.5f, c.z), _rightH[i], Vector3.up, fh,
                new Vector3(capWidth * 0.5f, capHeight * 0.5f, capDepth * 0.5f));
        }
    }

    // ------------------------------------------------------------------ служебное

    static void Runs(bool[] f, List<int> starts, List<int> ends)
    {
        starts.Clear();
        ends.Clear();
        int i = 0;
        while (i < f.Length)
        {
            if (!f[i])
            {
                i++;
                continue;
            }
            int j = i;
            while (j + 1 < f.Length && f[j + 1])
                j++;
            starts.Add(i);
            ends.Add(j);
            i = j + 1;
        }
    }

    static void RunsOfMode(byte[] mode, byte value, List<int> starts, List<int> ends)
    {
        var f = new bool[mode.Length];
        for (int i = 0; i < mode.Length; i++)
            f[i] = mode[i] == value;
        Runs(f, starts, ends);
    }

    static bool[] Dilate(bool[] src, int r)
    {
        var res = new bool[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            if (!src[i])
                continue;
            int a = Mathf.Max(0, i - r);
            int b = Mathf.Min(src.Length - 1, i + r);
            for (int j = a; j <= b; j++)
                res[j] = true;
        }
        return res;
    }

    // ------------------------------------------------------------------ команды из меню компонента (три точки справа)

    // Притянуть узлы сплайна к земле (для сплайна, нарисованного вручную).
    [ContextMenu("Snap Spline To Terrain")]
    public void SnapSplineToTerrain()
    {
        if (container == null)
            container = GetComponent<SplineContainer>();
        if (container == null)
            return;
        _terr = terrain != null ? terrain : Terrain.activeTerrain;

#if UNITY_EDITOR
        Undo.RecordObject(container, "Snap Spline To Terrain");
#endif
        Spline spline = container.Spline;
        for (int i = 0; i < spline.Count; i++)
        {
            BezierKnot k = spline[i];
            Vector3 w = container.transform.TransformPoint((Vector3)k.Position);
            w.y = Ground(w) + snapLift;
            k.Position = container.transform.InverseTransformPoint(w);
            spline[i] = k;
        }
#if UNITY_EDITOR
        EditorUtility.SetDirty(container);
#endif
    }

    // Вырезать в террейне выемку там, где дорога ниже земли (холм иначе закроет дорогу).
    // Меняет данные террейна. Отмена: Ctrl+Z.
    [ContextMenu("Carve Terrain Under Road")]
    public void CarveTerrain()
    {
        if (container == null)
            container = GetComponent<SplineContainer>();
        Terrain t = terrain != null ? terrain : Terrain.activeTerrain;
        if (container == null || t == null)
        {
            Debug.LogWarning("RoadBuilder: нужен SplineContainer и Terrain.", this);
            return;
        }

        TerrainData td = t.terrainData;
        int res = td.heightmapResolution;
        Vector3 tp = t.GetPosition();
        Vector3 size = td.size;
        float[,] hm = td.GetHeights(0, 0, res, res);
#if UNITY_EDITOR
        // резервная копия исходного рельефа: в первый раз сохраняем, потом каждая прокопка начинается с неё
        string backupPath = BackupPath(td);
        if (File.Exists(backupPath))
        {
            float[,] orig = ReadBackup(backupPath, res);
            if (orig != null)
                hm = orig;
        }
        else
            SaveBackup(backupPath, hm);
#endif
        float cell = Mathf.Min(size.x, size.z) / (res - 1);
        float step = Mathf.Max(0.3f, cell * 0.7f);
        float length = container.CalculateLength(0);
        int steps = Mathf.CeilToInt(length / step);
        float half = roadWidth * 0.5f + carveShoulder;
        float reach = maxSlopeWidth;
        var roadPts = new List<Vector3>();
        int pickEvery = Mathf.Max(1, Mathf.RoundToInt(2f / step));

#if UNITY_EDITOR
        Undo.RegisterCompleteObjectUndo(td, "Carve Terrain Under Road");
#endif
        for (int i = 0; i <= steps; i++)
        {
            float tt = Mathf.Min(1f, (float)i / steps);
            container.Evaluate(0, tt, out var p, out var tan, out var upv);
            Vector3 pos = (Vector3)p;
            if (i % pickEvery == 0)
                roadPts.Add(pos);
            Vector3 f = (Vector3)tan;
            Vector3 right = new Vector3(f.z, 0f, -f.x);
            if (right.sqrMagnitude < 1e-8f)
                continue;
            right.Normalize();
            float roadY = pos.y - 0.3f;

            for (float lat = -(half + reach); lat <= half + reach; lat += step)
            {
                float ad = Mathf.Abs(lat);
                float allowed = ad <= half ? roadY : roadY + (ad - half) / slopeRatio;
                Vector3 w = pos + right * lat;
                int ix = Mathf.RoundToInt((w.x - tp.x) / size.x * (res - 1));
                int iz = Mathf.RoundToInt((w.z - tp.z) / size.z * (res - 1));
                if (ix < 0 || iz < 0 || ix >= res || iz >= res)
                    continue;
                float hn = Mathf.Max(0f, (allowed - tp.y) / size.y);
                if (hn < hm[iz, ix])
                    hm[iz, ix] = hn;
            }
        }
        td.SetHeights(0, 0, hm);

        // деревья на полосе дороги убираем, иначе они останутся торчать посреди проезжей части
        if (removeTreesOnRoad && td.treeInstances.Length > 0)
        {
            float clear = half + 2f;
            float clear2 = clear * clear;
            TreeInstance[] trees = td.treeInstances;
            var keepTrees = new List<TreeInstance>(trees.Length);
            foreach (TreeInstance ti in trees)
            {
                float wx = tp.x + ti.position.x * size.x;
                float wz = tp.z + ti.position.z * size.z;
                bool onRoad = false;
                for (int k = 0; k < roadPts.Count && !onRoad; k++)
                {
                    float dx = roadPts[k].x - wx;
                    if (dx > clear || dx < -clear)
                        continue;
                    float dz = roadPts[k].z - wz;
                    if (dx * dx + dz * dz < clear2)
                        onRoad = true;
                }
                if (!onRoad)
                    keepTrees.Add(ti);
            }
            if (keepTrees.Count != trees.Length)
                td.treeInstances = keepTrees.ToArray();
        }

        RequestRebuild();
    }

    // Вернуть рельеф таким, каким он был до первой прокопки.
    [ContextMenu("Restore Terrain From Backup")]
    public void RestoreTerrain()
    {
#if UNITY_EDITOR
        Terrain t = terrain != null ? terrain : Terrain.activeTerrain;
        if (t == null)
            return;
        TerrainData td = t.terrainData;
        string bp = BackupPath(td);
        if (!File.Exists(bp))
        {
            Debug.LogWarning("RoadBuilder: резервной копии рельефа нет, сначала прокопай холм.", this);
            return;
        }
        float[,] orig = ReadBackup(bp, td.heightmapResolution);
        if (orig == null)
            return;
        Undo.RegisterCompleteObjectUndo(td, "Restore Terrain");
        td.SetHeights(0, 0, orig);
        RequestRebuild();
#endif
    }

    // Удалить резервную копию. Нужно, если после прокопки ты вручную лепил террейн и хочешь, чтобы это стало «исходным».
    public void ForgetTerrainBackup()
    {
#if UNITY_EDITOR
        Terrain t = terrain != null ? terrain : Terrain.activeTerrain;
        if (t == null)
            return;
        string bp = BackupPath(t.terrainData);
        if (File.Exists(bp))
            AssetDatabase.DeleteAsset(bp);
#endif
    }

#if UNITY_EDITOR
    static string BackupPath(TerrainData td)
    {
        return "Assets/" + td.name + "_heights_backup.bytes";
    }

    static void SaveBackup(string path, float[,] hm)
    {
        var bytes = new byte[hm.Length * sizeof(float)];
        System.Buffer.BlockCopy(hm, 0, bytes, 0, bytes.Length);
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();
    }

    static float[,] ReadBackup(string path, int res)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length != res * res * sizeof(float))
        {
            Debug.LogWarning("RoadBuilder: резервная копия не подходит к этому террейну (другое разрешение). " +
                             "Нажми «Забыть резервную копию».");
            return null;
        }
        var hm = new float[res, res];
        System.Buffer.BlockCopy(bytes, 0, hm, 0, bytes.Length);
        return hm;
    }
#endif
}