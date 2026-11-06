using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Строит замкнутую трассу (один старт/финиш) в SplineContainer на этом же объекте.
// Форма: углы-«вешки» со скруглением, как у настоящей дороги. Никаких петель вверх ногами.
// Если трасса сама себя пересекает (клевер, «восьмёрка») — один проезд поднимается на эстакаду.
// Если указан Terrain — трасса по нему идёт (с ограничением уклона).
// Сам меш дороги строит RoadBuilder.
[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
public class TrackGen : MonoBehaviour
{
    public enum Layout
    {
        Oval,               // овал
        GrandPrix,          // кольцевая трасса со шпилькой и эсками
        FigureEightFlyover, // восьмёрка с путепроводом
        Cloverleaf,         // клеверный лист: 4 петли, 4 путепровода, 2 уровня
        Serpentine,         // серпантин
        TrefoilKnot,        // узел-трилистник: 3 путепровода, дорога то вверх, то вниз
        SpiralClimb,        // спираль: 2 витка вверх на 36 м, длинный спуск, внизу дорога проезжает под спиралью
        Custom              // свои углы из списка Custom Points
    }

    [Header("Форма трассы")]
    public Layout layout = Layout.Cloverleaf;
    [Min(0.2f)] public float layoutScale = 1f;

    [Header("Клеверный лист")]
    [Min(30f)] public float cloverLoopRadius = 70f;
    [Min(10f)] public float cloverLaneGap = 28f;

    [Header("Своя форма (Layout = Custom): углы замкнутой трассы, x и z в метрах")]
    public List<Vector2> customPoints = new List<Vector2>
    {
        new Vector2(-250f, -120f), new Vector2(250f, -120f), new Vector2(300f, 60f),
        new Vector2(0f, 160f), new Vector2(-300f, 100f)
    };
    [Min(10f)] public float customRadius = 70f;
    [Tooltip("Высота каждого угла, м (по порядку, как Custom Points). Если список короче, остальные = 0. Дорога плавно идёт между ними")]
    public List<float> customHeights = new List<float>();

    [Header("Дорога и эстакады")]
    [Min(4f)] public float roadWidth = 14f;
    [Tooltip("Насколько путепровод выше дороги под ним (по верху полотна), м")]
    [Min(4f)] public float crossingClearance = 7f;
    [Tooltip("Уклон подъёма на эстакаду (0.05 = 5%)")]
    [Range(0.02f, 0.12f)] public float rampGrade = 0.05f;

    [Header("Холмы вдоль трассы")]
    [Tooltip("Высота волн, м. 0 = ровно. Дорога по кругу поднимается и опускается. Уклон примерно amplitude * 3 * count / длина трассы")]
    [Min(0f)] public float waveAmplitude = 0f;
    [Min(1)] public int waveCount = 4;

    [Header("Рельеф")]
    public bool followTerrain = true;
    [Tooltip("Если пусто, берётся активный Terrain на сцене")]
    public Terrain terrain;
    [Tooltip("Насколько сглаживать рельеф вдоль дороги, м (больше = ровнее дорога, больше насыпей и выемок)")]
    [Min(10f)] public float terrainSmoothing = 70f;
    [Range(0.02f, 0.15f)] public float terrainMaxGrade = 0.06f;
    [Tooltip("Насколько дорога выше поверхности рельефа в среднем, м")]
    public float roadLift = 0.2f;

    [Header("Генерация")]
    [Min(1.5f)] public float sampleSpacing = 3f;
    [Min(3f)] public float knotSpacing = 9f;
    [Tooltip("Выключи, если хочешь вручную править сплайн: иначе он перезаписывается при каждом включении")]
    public bool autoRebuild = true;
    public bool showDebug;

    [SerializeField, HideInInspector] Vector3 _startWorld;
    [SerializeField, HideInInspector] Vector3 _startDir = Vector3.forward;
    [SerializeField, HideInInspector] bool _hasStart;

    public float TrackLength { get; private set; }

    SplineContainer _container;

    struct Wp
    {
        public Vector2 p;   // угол
        public float r;     // радиус скругления
        public float h;     // высота дороги в этом углу, м
        public Wp(Vector2 p, float r, float h = 0f) { this.p = p; this.r = r; this.h = h; }
        public Wp(float x, float z, float r, float h = 0f) { p = new Vector2(x, z); this.r = r; this.h = h; }
    }

    struct Crossing
    {
        public float sa, sb, sin;
    }

    struct Ev
    {
        public float s, ph;
        public int ci, w;
    }

    // ------------------------------------------------------------------ жизненный цикл

    void OnEnable()
    {
        _container = GetComponent<SplineContainer>();
        if (autoRebuild)
            RequestGenerate();
    }

    void OnValidate()
    {
        if (autoRebuild)
            RequestGenerate();
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= GenerateDelayed;
#endif
    }

    void RequestGenerate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= GenerateDelayed;
            EditorApplication.delayCall += GenerateDelayed;
            return;
        }
#endif
        Generate();
    }

#if UNITY_EDITOR
    void GenerateDelayed()
    {
        if (this == null || !enabled || !autoRebuild)
            return;
        Generate();
    }
#endif

    void OnDrawGizmosSelected()
    {
        if (!_hasStart)
            return;
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(_startWorld + Vector3.up * 1.5f, 2f);
        Gizmos.DrawLine(_startWorld + Vector3.up * 1.5f, _startWorld + Vector3.up * 1.5f + _startDir * 20f);
    }

    // ------------------------------------------------------------------ основной проход

    [ContextMenu("Generate Track")]
    public void Generate()
    {
        if (_container == null)
            _container = GetComponent<SplineContainer>();
        if (_container == null)
            return;

        List<Wp> wps = GetWaypoints();
        if (wps.Count < 3)
        {
            Debug.LogWarning("TrackGenerator: нужно минимум 3 угла.", this);
            return;
        }

        // шаг выборки подбираем так, чтобы точек было не больше ~4000
        float perimeter = 0f;
        for (int i = 0; i < wps.Count; i++)
            perimeter += Vector2.Distance(wps[i].p, wps[(i + 1) % wps.Count].p);
        float ds = Mathf.Max(1.5f, sampleSpacing, perimeter / 4000f);

        var ptsL = new List<Vector2>();
        var stL = new List<bool>();
        BuildPath(wps, ds, ptsL, stL);
        if (ptsL.Count < 12)
        {
            Debug.LogWarning("TrackGenerator: трасса получилась слишком короткой.", this);
            return;
        }

        Vector2[] P = ptsL.ToArray();
        bool[] straight = stL.ToArray();
        int n = P.Length;

        var s = new float[n];
        var seg = new float[n];
        float L = 0f;
        for (int i = 0; i < n; i++)
        {
            s[i] = L;
            seg[i] = Vector2.Distance(P[i], P[(i + 1) % n]);
            L += seg[i];
        }
        TrackLength = L;

        List<Crossing> cr = FindCrossings(P, s, L, seg);
        int clusters;
        float worst;
        float[] profile = BuildProfile(wps, P, s, L);
        float[] h = BuildHeights(P, s, L, seg, ds, cr, profile, out clusters, out worst);
        int i0 = PickStart(straight, s, L, h, cr, ds);

        // ---- сплайн
        int step = Mathf.Max(1, Mathf.RoundToInt(knotSpacing / ds));
        int count = Mathf.Max(8, n / step);

        Spline spline = _container.Spline;
        spline.Clear();
        for (int m = 0; m < count; m++)
        {
            int idx = (i0 + (int)((long)m * n / count)) % n;
            Vector3 w = transform.TransformPoint(new Vector3(P[idx].x, 0f, P[idx].y));
            w.y = h[idx];
            spline.Add(new BezierKnot(transform.InverseTransformPoint(w)));
        }
        spline.Closed = true;
        spline.SetTangentMode(TangentMode.AutoSmooth);

        // ---- точка старта для гизмо
        Vector3 s0 = transform.TransformPoint(new Vector3(P[i0].x, 0f, P[i0].y));
        s0.y = h[i0];
        Vector2 d2 = P[(i0 + 1) % n] - P[i0];
        _startWorld = s0;
        _startDir = transform.TransformDirection(new Vector3(d2.x, 0f, d2.y)).normalized;
        _hasStart = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(_container);
            EditorUtility.SetDirty(this);
            SceneView.RepaintAll();
        }
#endif

        if (cr.Count > 0 && worst < crossingClearance - 0.1f)
            Debug.LogWarning($"TrackGenerator: зазор в пересечении {worst:F1} м меньше нужных {crossingClearance:F1} м. " +
                             "Увеличь размер формы (Layout Scale), уменьши Ramp Grade или уменьши Crossing Clearance.", this);

        if (showDebug)
            Debug.Log($"Трасса {layout}: длина {L:F0} м, узлов {count}, пересечений {cr.Count}, " +
                      $"эстакадных участков {clusters}, мин. зазор {(cr.Count > 0 ? worst : 0f):F1} м", this);
    }

    // ------------------------------------------------------------------ формы

    List<Wp> GetWaypoints()
    {
        var w = new List<Wp>();
        switch (layout)
        {
            case Layout.Oval:
                w.Add(new Wp(-300, -110, 100));
                w.Add(new Wp(300, -110, 100));
                w.Add(new Wp(300, 110, 100));
                w.Add(new Wp(-300, 110, 100));
                break;

            case Layout.GrandPrix:
                w.Add(new Wp(-430, -210, 90));
                w.Add(new Wp(330, -210, 110));
                w.Add(new Wp(450, -60, 70));
                w.Add(new Wp(270, 20, 55));
                w.Add(new Wp(360, 170, 60));
                w.Add(new Wp(140, 270, 80));
                w.Add(new Wp(-40, 170, 45));
                w.Add(new Wp(-130, 280, 60));
                w.Add(new Wp(-380, 250, 90));
                w.Add(new Wp(-470, 60, 70));
                w.Add(new Wp(-250, -10, 50));
                w.Add(new Wp(-420, -110, 60));
                break;

            case Layout.FigureEightFlyover:
                w.Add(new Wp(-270, -210, 70));
                w.Add(new Wp(270, 210, 70));
                w.Add(new Wp(270, -210, 70));
                w.Add(new Wp(-270, 210, 70));
                break;

            case Layout.Cloverleaf:
                AddClover(w);
                break;

            case Layout.Serpentine:
                w.Add(new Wp(-450, -80, 60));
                w.Add(new Wp(330, -80, 40));
                w.Add(new Wp(-330, 40, 40));
                w.Add(new Wp(330, 160, 40));
                w.Add(new Wp(-330, 280, 40));
                w.Add(new Wp(330, 400, 40));
                w.Add(new Wp(-450, 470, 60));
                break;

            case Layout.TrefoilKnot:
                AddTrefoil(w);
                break;

            case Layout.SpiralClimb:
                AddSpiral(w);
                break;

            case Layout.Custom:
                for (int i = 0; i < customPoints.Count; i++)
                    w.Add(new Wp(customPoints[i], customRadius, i < customHeights.Count ? customHeights[i] : 0f));
                break;
        }

        for (int i = 0; i < w.Count; i++)
            w[i] = new Wp(w[i].p * layoutScale, w[i].r * layoutScale, w[i].h * layoutScale);
        return w;
    }

    // Трилистник: x = sin t + 2 sin 2t, y = cos t - 2 cos 2t. Скругления с огромным радиусом
    // сами сжимаются до длины отрезков, получается гладкая кривая из дуг.
    void AddTrefoil(List<Wp> w)
    {
        const int count = 60;
        const float size = 95f;
        for (int k = 0; k < count; k++)
        {
            float t = 2f * Mathf.PI * k / count;
            float x = Mathf.Sin(t) + 2f * Mathf.Sin(2f * t);
            float z = Mathf.Cos(t) - 2f * Mathf.Cos(2f * t);
            w.Add(new Wp(size * x, size * z, 900f));
        }
    }

    // Спираль: два витка наружу с подъёмом, потом длинный спуск по большой петле,
    // внизу подъезд к началу спирали проходит под её витками.
    void AddSpiral(List<Wp> w)
    {
        const float r0 = 70f, gap = 45f, hmax = 36f;
        const int turns = 2, perTurn = 18;
        int n = turns * perTurn;
        for (int k = 0; k <= n; k++)
        {
            float th = 2f * Mathf.PI * turns * k / n;
            float r = r0 + gap * th / (2f * Mathf.PI);
            w.Add(new Wp(r * Mathf.Cos(th), r * Mathf.Sin(th), 40f, hmax * k / n));
        }
        float rend = r0 + gap * turns;
        w.Add(new Wp(rend + 8f, 260f, 90f, hmax * 0.92f));
        w.Add(new Wp(-300f, 340f, 130f, hmax * 0.6f));
        w.Add(new Wp(-360f, -300f, 130f, hmax * 0.25f));
        w.Add(new Wp(r0, -320f, 70f, 0f));
    }

    // Клевер: 4 одинаковых «рукава» с петлёй 270 градусов, повёрнутые на 90 градусов.
    // Пары параллельных рукавов стоят на расстоянии cloverLaneGap, получается обычный «плюс» с четырьмя петлями.
    void AddClover(List<Wp> w)
    {
        float R = cloverLoopRadius;
        float L = 2f * R + cloverLaneGap;
        float x1 = L + R;
        Vector2[] unit =
        {
            new Vector2(x1, 0f),
            new Vector2(x1, -2f * R),
            new Vector2(x1 - 2f * R, -2f * R)
        };
        var o = new Vector2(L * 0.5f, L * 0.5f - R);
        for (int k = 0; k < 4; k++)
        {
            foreach (Vector2 p in unit)
            {
                Vector2 q = p - o;
                for (int m = 0; m < k; m++)
                    q = new Vector2(-q.y, q.x);
                w.Add(new Wp(o + q, R));
            }
        }
    }

    // Углы со скруглением дугой -> плотная замкнутая ломаная.
    static void BuildPath(List<Wp> w, float ds, List<Vector2> pts, List<bool> straight)
    {
        int n = w.Count;
        var t = new float[n];
        var v1 = new Vector2[n];
        var v2 = new Vector2[n];
        var phi = new float[n];

        for (int i = 0; i < n; i++)
        {
            Vector2 p = w[i].p;
            v1[i] = (w[(i - 1 + n) % n].p - p).normalized;
            v2[i] = (w[(i + 1) % n].p - p).normalized;
            phi[i] = Mathf.Acos(Mathf.Clamp(Vector2.Dot(v1[i], v2[i]), -1f, 1f));
            if (w[i].r > 0.01f && phi[i] > 0.02f && phi[i] < Mathf.PI - 0.002f)
                t[i] = w[i].r / Mathf.Tan(phi[i] * 0.5f);
        }

        // если соседние скругления не помещаются на отрезке, уменьшаем оба
        var f = new float[n];
        for (int i = 0; i < n; i++)
            f[i] = 1f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            float len = Vector2.Distance(w[i].p, w[j].p);
            float need = t[i] + t[j];
            if (need > len && need > 0f)
            {
                float k = len / need;
                f[i] = Mathf.Min(f[i], k);
                f[j] = Mathf.Min(f[j], k);
            }
        }
        for (int i = 0; i < n; i++)
            t[i] *= f[i];

        for (int i = 0; i < n; i++)
        {
            Vector2 p = w[i].p;
            if (t[i] > 1e-4f)
            {
                Vector2 t1 = p + v1[i] * t[i];
                Vector2 t2 = p + v2[i] * t[i];
                float r = t[i] * Mathf.Tan(phi[i] * 0.5f);
                Vector2 bis = (v1[i] + v2[i]).normalized;
                Vector2 c = p + bis * (r / Mathf.Sin(phi[i] * 0.5f));
                float a0 = Mathf.Atan2(t1.y - c.y, t1.x - c.x);
                float turn = Mathf.PI - phi[i];
                Vector2 din = -v1[i];
                Vector2 dout = v2[i];
                float sign = (din.x * dout.y - din.y * dout.x) > 0f ? 1f : -1f;
                int steps = Mathf.Max(2, Mathf.CeilToInt(r * turn / ds));
                for (int k = 0; k <= steps; k++)
                {
                    float ang = a0 + sign * turn * k / steps;
                    pts.Add(c + r * new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)));
                    straight.Add(false);
                }
            }
            else
            {
                pts.Add(p);
                straight.Add(true);
            }

            // прямая до начала следующего скругления
            int j = (i + 1) % n;
            Vector2 end = w[j].p + v1[j] * t[j];
            Vector2 start = pts[pts.Count - 1];
            int m = Mathf.FloorToInt(Vector2.Distance(start, end) / ds);
            for (int k = 1; k <= m; k++)
            {
                pts.Add(Vector2.Lerp(start, end, k / (float)(m + 1)));
                straight.Add(true);
            }
        }

        // убираем совпавшие подряд точки (стык скруглений)
        for (int i = pts.Count - 1; i >= 0; i--)
        {
            if (pts.Count <= 4)
                break;
            int j = (i + 1) % pts.Count;
            if ((pts[i] - pts[j]).sqrMagnitude < 0.0004f)
            {
                pts.RemoveAt(i);
                straight.RemoveAt(i);
            }
        }
    }

    // ------------------------------------------------------------------ пересечения и высота

    List<Crossing> FindCrossings(Vector2[] P, float[] s, float L, float[] seg)
    {
        int n = P.Length;
        var res = new List<Crossing>();
        float minSep = 2.5f * roadWidth;

        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = P[i];
            Vector2 p1 = P[(i + 1) % n];
            Vector2 d1 = p1 - p0;
            float xa = Mathf.Min(p0.x, p1.x), xb = Mathf.Max(p0.x, p1.x);
            float ya = Mathf.Min(p0.y, p1.y), yb = Mathf.Max(p0.y, p1.y);

            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1)
                    continue;
                float dsep = Mathf.Abs(s[j] - s[i]);
                if (Mathf.Min(dsep, L - dsep) < minSep)
                    continue;

                Vector2 r0 = P[j];
                Vector2 r1 = P[(j + 1) % n];
                if (Mathf.Max(r0.x, r1.x) < xa || Mathf.Min(r0.x, r1.x) > xb ||
                    Mathf.Max(r0.y, r1.y) < ya || Mathf.Min(r0.y, r1.y) > yb)
                    continue;

                Vector2 d2 = r1 - r0;
                float den = d1.x * d2.y - d1.y * d2.x;
                if (Mathf.Abs(den) < 1e-9f)
                    continue;
                Vector2 wv = r0 - p0;
                float tt = (wv.x * d2.y - wv.y * d2.x) / den;
                float uu = (wv.x * d1.y - wv.y * d1.x) / den;
                if (tt >= 0f && tt < 1f && uu >= 0f && uu < 1f)
                {
                    res.Add(new Crossing
                    {
                        sa = s[i] + tt * seg[i],
                        sb = s[j] + uu * seg[j],
                        sin = Mathf.Abs(den) / (d1.magnitude * d2.magnitude)
                    });
                }
            }
        }
        return res;
    }

    // Высота по углам: плавная монотонная интерполяция по кругу (без провалов ниже заданных высот) + волны.
    float[] BuildProfile(List<Wp> wps, Vector2[] P, float[] s, float L)
    {
        int n = P.Length;
        var prof = new float[n];

        // каждый угол привязываем к ближайшей точке пути
        var idx = new List<int>();
        bool any = false;
        for (int k = 0; k < wps.Count; k++)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float dist = (P[i] - wps[k].p).sqrMagnitude;
                if (dist < bd)
                {
                    bd = dist;
                    best = i;
                }
            }
            idx.Add(best);
            if (Mathf.Abs(wps[k].h) > 1e-4f)
                any = true;
        }

        if (any)
        {
            var order = new List<int>();
            for (int k = 0; k < wps.Count; k++)
                order.Add(k);
            order.Sort((x, y) => s[idx[x]].CompareTo(s[idx[y]]));

            var aS = new List<float>();
            var aH = new List<float>();
            foreach (int k in order)
            {
                float sv = s[idx[k]];
                if (aS.Count > 0 && sv - aS[aS.Count - 1] < 1f)
                    continue;
                aS.Add(sv);
                aH.Add(wps[k].h);
            }

            int K = aS.Count;
            if (K >= 2)
            {
                var dLen = new float[K];
                var dSlope = new float[K];
                var mTan = new float[K];
                for (int i = 0; i < K; i++)
                {
                    float dd = aS[(i + 1) % K] - aS[i];
                    if (dd <= 1e-3f)
                        dd += L;
                    dLen[i] = dd;
                    dSlope[i] = (aH[(i + 1) % K] - aH[i]) / dd;
                }
                for (int i = 0; i < K; i++)
                {
                    int pv = (i - 1 + K) % K;
                    float d0 = dSlope[pv], d1 = dSlope[i];
                    if (d0 * d1 <= 0f)
                    {
                        mTan[i] = 0f;
                    }
                    else
                    {
                        float h0 = dLen[pv], h1 = dLen[i];
                        float w1 = 2f * h1 + h0, w2 = h1 + 2f * h0;
                        mTan[i] = (w1 + w2) / (w1 / d0 + w2 / d1);
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    int j = aS.BinarySearch(s[i]);
                    if (j < 0)
                        j = ~j - 1;
                    if (j < 0)
                        j = K - 1;
                    int nx = (j + 1) % K;
                    float sa = aS[j], sb = aS[nx];
                    if (sb <= sa)
                        sb += L;
                    float sv = s[i] >= sa ? s[i] : s[i] + L;
                    float len = sb - sa;
                    float t = (sv - sa) / len;
                    float t2 = t * t, t3 = t2 * t;
                    prof[i] = (2f * t3 - 3f * t2 + 1f) * aH[j]
                              + (t3 - 2f * t2 + t) * len * mTan[j]
                              + (-2f * t3 + 3f * t2) * aH[nx]
                              + (t3 - t2) * len * mTan[nx];
                }
            }
        }

        if (waveAmplitude > 0.001f)
        {
            for (int i = 0; i < n; i++)
                prof[i] += waveAmplitude * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * waveCount * s[i] / L));
        }
        return prof;
    }

    static float Wrap(float x, float L)
    {
        return x - L * Mathf.Round(x / L);
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static int IndexAt(float[] s, float v)
    {
        int i = System.Array.BinarySearch(s, v);
        if (i >= 0)
            return i;
        i = ~i;
        if (i <= 0)
            return 0;
        if (i >= s.Length)
            return s.Length - 1;
        return (v - s[i - 1] < s[i] - v) ? i - 1 : i;
    }

    // Итоговая высота (мировая Y) по каждой точке пути.
    float[] BuildHeights(Vector2[] P, float[] s, float L, float[] seg, float ds,
                         List<Crossing> cr, float[] profile, out int clusterCount, out float worst)
    {
        int n = P.Length;
        var baseH = new float[n];

        Terrain t = null;
        if (followTerrain)
            t = terrain != null ? terrain : Terrain.activeTerrain;

        if (t != null)
        {
            float oy = t.GetPosition().y;
            var raw = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 w = transform.TransformPoint(new Vector3(P[i].x, 0f, P[i].y));
                raw[i] = t.SampleHeight(w) + oy;
            }

            // скользящее среднее по кругу
            int hw = Mathf.Max(1, Mathf.RoundToInt(terrainSmoothing / ds * 0.5f));
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int k = -hw; k <= hw; k++)
                    sum += raw[((i + k) % n + n) % n];
                baseH[i] = sum / (2 * hw + 1);
            }

            LimitSlope(baseH, seg, terrainMaxGrade);
            Consensus(baseH, s, L, cr);
            for (int i = 0; i < n; i++)
                baseH[i] += roadLift;
        }
        else
        {
            float y = transform.position.y;
            for (int i = 0; i < n; i++)
                baseH[i] = y;
        }

        // высоты по углам и волны добавляются поверх рельефа
        for (int i = 0; i < n; i++)
            baseH[i] += profile[i];

        clusterCount = 0;
        worst = float.MaxValue;
        if (cr.Count == 0)
            return baseH;

        // --- зоны пересечений: вокруг каждого прохода берём участок, где дороги перекрываются по площади
        var evs = new List<Ev>();
        for (int ci = 0; ci < cr.Count; ci++)
        {
            float ph = roadWidth * 0.5f / Mathf.Max(cr[ci].sin, 0.3f) + 8f;
            evs.Add(new Ev { s = cr[ci].sa, ph = ph, ci = ci, w = 0 });
            evs.Add(new Ev { s = cr[ci].sb, ph = ph, ci = ci, w = 1 });
        }
        evs.Sort((a, b) => a.s.CompareTo(b.s));

        var ca = new List<float>();
        var cb = new List<float>();
        var cid = new int[cr.Count * 2];
        foreach (Ev e in evs)
        {
            if (ca.Count > 0 && e.s - e.ph <= cb[cb.Count - 1] + 20f)
                cb[cb.Count - 1] = Mathf.Max(cb[cb.Count - 1], e.s + e.ph);
            else
            {
                ca.Add(e.s - e.ph);
                cb.Add(e.s + e.ph);
            }
            cid[e.ci * 2 + e.w] = ca.Count - 1;
        }

        int K = ca.Count;
        clusterCount = K;

        var ia = new int[cr.Count];
        var ib = new int[cr.Count];
        var adj = new List<int>[K];
        for (int k = 0; k < K; k++)
            adj[k] = new List<int>();
        for (int ci = 0; ci < cr.Count; ci++)
        {
            ia[ci] = IndexAt(s, cr[ci].sa);
            ib[ci] = IndexAt(s, cr[ci].sb);
            int a = cid[ci * 2], b = cid[ci * 2 + 1];
            if (a != b)
            {
                adj[a].Add(b);
                adj[b].Add(a);
            }
        }

        // --- два уровня: раскрашиваем зоны в 0/1 так, чтобы в каждом пересечении были разные цвета
        var color = new int[K];
        var comp = new int[K];
        for (int k = 0; k < K; k++)
        {
            color[k] = -1;
            comp[k] = -1;
        }
        int nc = 0;
        for (int k = 0; k < K; k++)
        {
            if (color[k] >= 0)
                continue;
            color[k] = 0;
            comp[k] = nc;
            var stack = new Stack<int>();
            stack.Push(k);
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                foreach (int v in adj[u])
                {
                    if (color[v] < 0)
                    {
                        color[v] = 1 - color[u];
                        comp[v] = nc;
                        stack.Push(v);
                    }
                }
            }
            nc++;
        }

        // для каждой связной группы берём вариант, которому нужно меньше подъёма
        for (int g = 0; g < nc; g++)
        {
            var flipped = (int[])color.Clone();
            for (int k = 0; k < K; k++)
                if (comp[k] == g)
                    flipped[k] = 1 - color[k];
            if (RaiseCost(flipped, cr, cid, ia, ib, baseH) + 1e-4f < RaiseCost(color, cr, cid, ia, ib, baseH))
                color = flipped;
        }

        // подъём каждой зоны начинается с нуля и растёт только там, где зазора не хватает
        var H = new float[K];

        // --- подгоняем высоты, пока в каждом пересечении не будет нужного зазора
        float[] bump = null;
        for (int it = 0; it < 12; it++)
        {
            bump = Bumps(s, L, ca, cb, H);
            bool changed = false;
            var inc = new float[K];
            for (int ci = 0; ci < cr.Count; ci++)
            {
                int ka = cid[ci * 2], kb = cid[ci * 2 + 1];
                int hiK, hiI, loI;
                if (color[ka] != color[kb] && color[ka] > color[kb])
                {
                    hiK = ka; hiI = ia[ci]; loI = ib[ci];
                }
                else
                {
                    hiK = kb; hiI = ib[ci]; loI = ia[ci];
                }
                float diff = (baseH[hiI] + bump[hiI]) - (baseH[loI] + bump[loI]);
                if (diff < crossingClearance - 0.05f)
                {
                    inc[hiK] = Mathf.Max(inc[hiK], crossingClearance - diff);
                    changed = true;
                }
            }
            if (!changed)
                break;
            for (int k = 0; k < K; k++)
                H[k] = Mathf.Min(H[k] + inc[k], crossingClearance * 6f);
        }
        bump = Bumps(s, L, ca, cb, H);

        var result = new float[n];
        for (int i = 0; i < n; i++)
            result[i] = baseH[i] + bump[i];

        for (int ci = 0; ci < cr.Count; ci++)
            worst = Mathf.Min(worst, Mathf.Abs(result[ia[ci]] - result[ib[ci]]));
        return result;
    }

    float RaiseCost(int[] col, List<Crossing> cr, int[] cid, int[] ia, int[] ib, float[] baseH)
    {
        float tot = 0f;
        for (int ci = 0; ci < cr.Count; ci++)
        {
            int ka = cid[ci * 2], kb = cid[ci * 2 + 1];
            if (col[ka] == col[kb])
                continue;
            int hi = col[ka] > col[kb] ? ia[ci] : ib[ci];
            int lo = col[ka] > col[kb] ? ib[ci] : ia[ci];
            tot += Mathf.Max(0f, crossingClearance - (baseH[hi] - baseH[lo]));
        }
        return tot;
    }

    float[] Bumps(float[] s, float L, List<float> ca, List<float> cb, float[] H)
    {
        var h = new float[s.Length];
        for (int k = 0; k < H.Length; k++)
        {
            if (H[k] <= 0.001f)
                continue;
            float c = 0.5f * (ca[k] + cb[k]);
            float half = 0.5f * (cb[k] - ca[k]);
            float ramp = Mathf.Max(10f, 1.5f * H[k] / rampGrade);
            for (int i = 0; i < s.Length; i++)
            {
                float dd = Mathf.Abs(Wrap(s[i] - c, L)) - half;
                float v = dd <= 0f ? 1f : (dd >= ramp ? 0f : Smooth(1f - dd / ramp));
                h[i] = Mathf.Max(h[i], H[k] * v);
            }
        }
        return h;
    }

    // Ограничение уклона по кругу (вперёд-назад, несколько раз).
    static void LimitSlope(float[] h, float[] seg, float grade)
    {
        int n = h.Length;
        for (int round = 0; round < 3; round++)
        {
            for (int i = 0; i < 2 * n; i++)
            {
                int a = ((i - 1) % n + n) % n;
                int b = i % n;
                float lim = grade * seg[a];
                h[b] = Mathf.Clamp(h[b], h[a] - lim, h[a] + lim);
            }
            for (int i = 0; i < 2 * n; i++)
            {
                int a = ((-i) % n + n) % n;
                int b = ((-i - 1) % n + n) % n;
                float lim = grade * seg[b];
                h[b] = Mathf.Clamp(h[b], h[a] - lim, h[a] + lim);
            }
        }
    }

    // В пересечении оба прохода стоят над одной точкой рельефа: сводим их базовые высоты вместе плавно,
    // иначе на холмистой местности эстакада получилась бы огромной.
    static void Consensus(float[] h, float[] s, float L, List<Crossing> cr)
    {
        if (cr.Count == 0)
            return;
        var src = (float[])h.Clone();
        for (int ci = 0; ci < cr.Count; ci++)
        {
            int a = IndexAt(s, cr[ci].sa);
            int b = IndexAt(s, cr[ci].sb);
            float d = 0.5f * (src[b] - src[a]);
            float rad = Mathf.Max(90f, 1.5f * Mathf.Abs(d) / 0.05f);
            for (int pass = 0; pass < 2; pass++)
            {
                float center = pass == 0 ? cr[ci].sa : cr[ci].sb;
                float sign = pass == 0 ? 1f : -1f;
                for (int i = 0; i < h.Length; i++)
                {
                    float dd = Mathf.Abs(Wrap(s[i] - center, L));
                    if (dd < rad)
                        h[i] += sign * d * Smooth(1f - dd / rad);
                }
            }
        }
    }

    // Старт: ровная прямая, подальше от пересечений. Берём середину самой длинной такой зоны.
    static int PickStart(bool[] straight, float[] s, float L, float[] h, List<Crossing> cr, float ds)
    {
        int n = straight.Length;
        float flatLen = 25f;
        float minClear = 35f;
        int k = Mathf.Max(1, Mathf.RoundToInt(flatLen / ds));

        var ok = new bool[n];
        int okCount = 0;
        for (int i = 0; i < n; i++)
        {
            bool good = straight[i];
            float lo = h[i], hi = h[i];
            for (int j = -k; j <= k && good; j++)
            {
                int q = ((i + j) % n + n) % n;
                if (!straight[q])
                    good = false;
                lo = Mathf.Min(lo, h[q]);
                hi = Mathf.Max(hi, h[q]);
            }
            if (good && hi - lo > 0.03f * 2f * flatLen)
                good = false;
            if (good)
            {
                for (int ci = 0; ci < cr.Count && good; ci++)
                {
                    if (Mathf.Abs(Wrap(s[i] - cr[ci].sa, L)) < minClear ||
                        Mathf.Abs(Wrap(s[i] - cr[ci].sb, L)) < minClear)
                        good = false;
                }
            }
            ok[i] = good;
            if (good)
                okCount++;
        }
        if (okCount == 0 || okCount == n)
            return 0;

        int first = 0;
        for (int i = 0; i < n; i++)
        {
            if (!ok[i])
            {
                first = i;
                break;
            }
        }

        int best = 0, bestLen = 0, run = 0;
        for (int step = 0; step < n; step++)
        {
            int j = (first + step) % n;
            if (ok[j])
            {
                run++;
                if (run > bestLen)
                {
                    bestLen = run;
                    best = ((j - run / 2) % n + n) % n;
                }
            }
            else
                run = 0;
        }
        return best;
    }
}