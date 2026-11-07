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
public class TrackGen: MonoBehaviour
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
        Star,               // звезда с 5-20 лучами (Star Points), Star Skip = как «густо» прошита
        MooreFractal,       // фрактальная кривая Мура: сотни поворотов, пересечений нет
        KochSnowflake,      // снежинка Коха: фрактальная кайма из 60-градусных шпилек
        Custom              // свои углы из списка Custom Points
    }

    [Header("Форма трассы")]
    public Layout layout = Layout.Cloverleaf;
    [Min(0.2f)] public float layoutScale = 1f;
    [Tooltip("Сколько раз повторить выбранную фигуру в ряд. Копии соединяются в ОДНУ линию дороги (один старт/финиш). 1 = без повтора")]
    [Range(1, 10)] public int repeatCount = 1;
    [Tooltip("Просвет между копиями, м (до Layout Scale не масштабируется)")]
    [Min(10f)] public float repeatGap = 40f;

    [Header("Звезда (Layout = Star)")]
    [Range(5, 20)] public int starPoints = 7;
    [Tooltip("Через сколько лучей соединять: 2 = обычная звезда, 3 = глубже. Если число не взаимно простое с количеством лучей, берётся ближайшее подходящее. " +
             "Большие значения дают много пересечений: увеличь Star Radius, иначе эстакады не уложатся в уклон (смотри предупреждение в консоли)")]
    [Range(1, 9)] public int starSkip = 2;
    [Min(150f)] public float starRadius = 520f;
    [Min(15f)] public float starTipRadius = 45f;

    [Header("Фракталы (Layout = MooreFractal / KochSnowflake)")]
    [Tooltip("Мур: порядок 2-4 (4 даёт ~23 км и ~1000 поворотов). Коха: 1-2")]
    [Range(1, 4)] public int fractalOrder = 3;
    [Tooltip("Мур: длина одной клетки, м (70). Коха: сторона всей снежинки, м (если меньше 200, берётся 900)")]
    [Min(30f)] public float fractalSize = 70f;

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
    [Tooltip("Прижатие к рельефу. 1 = дорога повторяет рельеф (с учётом сглаживания). 0 = дорога почти не лезёт вверх-вниз за холмами: " +
             "над впадинами получаются плавные мосты на опорах, холмы прорезаются выемкой")]
    [Range(0f, 1f)] public float terrainHug = 0.7f;
    [Tooltip("Максимальная крутизна дороги (0.06 = 6%). Меньше = положе, на крутых склонах дорога идёт как плавный мост/выемка")]
    [Range(0.02f, 0.15f)] public float terrainMaxGrade = 0.06f;
    [Tooltip("Насколько дорога в среднем выше поверхности рельефа, м")]
    public float roadLift = 0.4f;

    [Header("Ответвления: запасные дороги (срезки, обманки, возвраты)")]
    [Tooltip("Сколько боковых дорог добавить к трассе. Появляются как дочерние объекты Branch_N со своим RoadBuilder. 0 = нет")]
    [Range(0, 12)] public int branchCount = 3;
    [Tooltip("Случайное зерно. Поменяй число, получишь другой набор ответвлений (правая кнопка на компоненте → Reroll Branches)")]
    public int branchSeed = 1;
    [Min(60f)] public float branchMinLength = 150f;
    [Min(100f)] public float branchMaxLength = 450f;
    [Tooltip("Ширина боковой дороги относительно основной")]
    [Range(0.4f, 1f)] public float branchWidthScale = 0.75f;
    [Tooltip("Разрешить петли-обманки: съехал, дорога разворачивает и выводит на основную чуть раньше по трассе, навстречу движению")]
    public bool allowBacktrack = true;

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

        BuildBranches(P, straight, s, L, h, cr, i0, ds);

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


    // ------------------------------------------------------------------ ответвления

    const string BranchPrefix = "Branch_";

    [ContextMenu("Reroll Branches")]
    public void RerollBranches()
    {
        branchSeed = Random.Range(0, 1000000);
        Generate();
    }

    void ClearBranches()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (!c.name.StartsWith(BranchPrefix))
                continue;
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }

    // Случайные боковые дороги: срезка (чуть короче основной), «блуждалка» (дольше), петля-обманка
    // (разворачивает и выходит на основную дорогу раньше по трассе). Ответвление не пересекает ни основную дорогу,
    // ни другие ответвления, а на концах плавно вливается в основную дорогу под тем же углом и на той же высоте.
    void BuildBranches(Vector2[] P, bool[] straight, float[] s, float L, float[] h,
                       List<Crossing> cr, int i0, float ds)
    {
        ClearBranches();
        if (branchCount <= 0)
            return;

        RoadBuilder main = GetComponent<RoadBuilder>();
        if (main == null)
            Debug.LogWarning("TrackGenerator: на этом объекте нет RoadBuilder, ответвления получатся без меша.", this);

        Terrain terr = followTerrain ? (terrain != null ? terrain : Terrain.activeTerrain) : null;
        var rng = new System.Random(branchSeed);
        int n = P.Length;
        float minSep = roadWidth + 4f;
        const float step = 4f;
        var used = new List<float> { s[i0] };       // s-координаты занятых мест (старт, развилки, слияния)
        var others = new List<List<Vector2>>();
        int made = 0;

        System.Func<float, bool> free = sv =>
        {
            foreach (float e in used)
                if (Mathf.Abs(Wrap(sv - e, L)) < 110f) return false;
            foreach (Crossing c in cr)
                if (Mathf.Abs(Wrap(sv - c.sa, L)) < 45f || Mathf.Abs(Wrap(sv - c.sb, L)) < 45f) return false;
            return true;
        };
        System.Func<int, Vector2> dirAt = i =>
        {
            Vector2 d = P[(i + 1) % n] - P[(i - 1 + n) % n];
            return d.sqrMagnitude < 1e-6f ? Vector2.right : d.normalized;
        };

        float maxLen = Mathf.Min(branchMaxLength, L * 0.35f);
        float minLen = Mathf.Min(branchMinLength, maxLen * 0.6f);

        for (int attempt = 0; attempt < branchCount * 150 && made < branchCount; attempt++)
        {
            int iF = rng.Next(n);
            if (!straight[iF]) continue;
            float sF = s[iF];
            if (!free(sF)) continue;

            double roll = rng.NextDouble();
            int type = roll < 0.4 ? 0 : (roll < 0.75 || !allowBacktrack ? 1 : 2);   // 0 срезка, 1 блуждалка, 2 обманка
            float D = Mathf.Lerp(type == 2 ? 80f : minLen, type == 2 ? 220f : maxLen, (float)rng.NextDouble());
            float sM = type == 2 ? sF - D : sF + D;
            sM = ((sM % L) + L) % L;
            int iM = IndexAt(s, sM);
            if (!straight[iM] || !free(sM)) continue;
            if (Mathf.Abs(Wrap(sM - sF, L)) < 60f) continue;

            Vector2 a = P[iF], b = P[iM];
            Vector2 fF = dirAt(iF), fM = dirAt(iM);
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
            Vector2 nrm = new Vector2(-fF.y, fF.x) * side;
            Vector2 nrmM = new Vector2(-fM.y, fM.x) * side;
            Vector2 mid = 0.5f * (a + b);
            float R01() => (float)rng.NextDouble();

            var c = new List<Vector2> { a };
            Vector2 t0 = fF, t1 = type == 2 ? -fM : fM;
            if (type == 0)
            {
                c.Add(mid + nrm * Mathf.Lerp(-10f, 25f, R01()));
            }
            else if (type == 1)
            {
                c.Add(Vector2.Lerp(a, b, 0.3f) + nrm * Mathf.Lerp(45f, 130f, R01()));
                c.Add(Vector2.Lerp(a, b, 0.7f) + nrm * Mathf.Lerp(-30f, 130f, R01()));
            }
            else
            {
                c.Add(a + fF * Mathf.Lerp(50f, 90f, R01()) + nrm * Mathf.Lerp(25f, 45f, R01()));
                c.Add(mid + nrm * Mathf.Lerp(120f, 230f, R01()));
                c.Add(b + fM * Mathf.Lerp(60f, 100f, R01()) + nrmM * Mathf.Lerp(20f, 40f, R01()));
            }
            c.Add(b);

            List<Vector2> pts = Hermite(c, t0, t1, step);
            if (pts.Count < 8) continue;

            var arc = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++)
                arc[i] = arc[i - 1] + Vector2.Distance(pts[i], pts[i - 1]);
            float Lb = arc[pts.Count - 1];
            if (type == 0 && Lb > 0.93f * D) continue;
            if (type == 1 && Lb < 1.15f * D) continue;

            // не слишком острые повороты
            bool ok = true;
            for (int i = 2; i < pts.Count - 2 && ok; i++)
            {
                Vector2 p0 = pts[i - 2], p1 = pts[i], p2 = pts[i + 2];
                float cross = Mathf.Abs((p1.x - p0.x) * (p2.y - p0.y) - (p1.y - p0.y) * (p2.x - p0.x));
                if (cross < 1e-3f) continue;
                float rad = (p1 - p0).magnitude * (p2 - p1).magnitude * (p2 - p0).magnitude / (2f * cross);
                if (rad < 26f) ok = false;
            }
            if (!ok) continue;

            // не лезем на основную дорогу и на другие ответвления
            for (int j = 0; j < pts.Count && ok; j++)
            {
                float fromEnd = Mathf.Min(arc[j], Lb - arc[j]);
                float t = Mathf.Clamp01((fromEnd - 20f) / 70f);
                float need = minSep * t;
                if (need > 0f)
                {
                    float nsq = need * need;
                    for (int k = 0; k < n; k += 2)
                        if ((P[k] - pts[j]).sqrMagnitude < nsq) { ok = false; break; }
                }
                if (!ok) break;
                float osq = minSep * minSep;
                foreach (var o in others)
                {
                    for (int k = 0; k < o.Count; k++)
                        if ((o[k] - pts[j]).sqrMagnitude < osq) { ok = false; break; }
                    if (!ok) break;
                }
            }
            if (!ok) continue;

            // ---- высоты
            float hF = h[iF], hM = h[iM];
            int m = pts.Count;
            var bh = new float[m];
            if (terr != null)
            {
                float oy = terr.GetPosition().y;
                var raw = new float[m];
                for (int i = 0; i < m; i++)
                    raw[i] = terr.SampleHeight(transform.TransformPoint(new Vector3(pts[i].x, 0f, pts[i].y))) + oy;
                int hwL = Mathf.Max(1, Mathf.RoundToInt(terrainSmoothing / step * 0.5f));
                int hwH = Mathf.Max(hwL, Mathf.RoundToInt(terrainSmoothing * 3.5f / step * 0.5f));
                for (int i = 0; i < m; i++)
                {
                    float sl = 0f, sh = 0f;
                    int cl = 0, ch = 0;
                    for (int k = -hwH; k <= hwH; k++)
                    {
                        int q = Mathf.Clamp(i + k, 0, m - 1);
                        sh += raw[q]; ch++;
                        if (Mathf.Abs(k) <= hwL) { sl += raw[q]; cl++; }
                    }
                    bh[i] = Mathf.Lerp(sh / ch, sl / cl, terrainHug);
                }
                float lim = terrainMaxGrade * step;
                for (int round = 0; round < 3; round++)
                {
                    for (int i = 1; i < m; i++) bh[i] = Mathf.Clamp(bh[i], bh[i - 1] - lim, bh[i - 1] + lim);
                    for (int i = m - 2; i >= 0; i--) bh[i] = Mathf.Clamp(bh[i], bh[i + 1] - lim, bh[i + 1] + lim);
                }
                for (int i = 0; i < m; i++) bh[i] += roadLift;
                float d0 = hF - bh[0], d1 = hM - bh[m - 1];
                float r0 = Mathf.Clamp(1.5f * Mathf.Abs(d0) / 0.05f, 60f, Lb * 0.6f);
                float r1 = Mathf.Clamp(1.5f * Mathf.Abs(d1) / 0.05f, 60f, Lb * 0.6f);
                for (int i = 0; i < m; i++)
                    bh[i] += d0 * Smooth(1f - arc[i] / r0) + d1 * Smooth(1f - (Lb - arc[i]) / r1);
            }
            else
            {
                for (int i = 0; i < m; i++)
                    bh[i] = Mathf.Lerp(hF, hM, Smooth(arc[i] / Lb));
            }

            // ---- объект ответвления
            var go = new GameObject(BranchPrefix + (made + 1));
            go.transform.SetParent(transform, false);
            var sc = go.AddComponent<SplineContainer>();
            Spline sp = sc.Spline;
            sp.Clear();
            float nextArc = 0f;
            for (int i = 0; i < m; i++)
            {
                if (i != m - 1 && arc[i] < nextArc) continue;
                nextArc = arc[i] + knotSpacing;
                Vector3 w = transform.TransformPoint(new Vector3(pts[i].x, 0f, pts[i].y));
                w.y = bh[i] + 0.03f;
                sp.Add(new BezierKnot(go.transform.InverseTransformPoint(w)));
            }
            sp.Closed = false;
            sp.SetTangentMode(TangentMode.AutoSmooth);

            if (main != null)
            {
                var rb = go.AddComponent<RoadBuilder>();
                foreach (var f in typeof(RoadBuilder).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    if (f.Name != "container")
                        f.SetValue(rb, f.GetValue(main));
                rb.container = sc;
                rb.roadWidth = main.roadWidth * branchWidthScale;
                rb.Rebuild();
            }

            others.Add(pts);
            used.Add(sF);
            used.Add(sM);
            made++;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(go);
#endif
        }

        if (made < branchCount)
            Debug.Log($"Ответвления: удалось разместить {made} из {branchCount}. Не хватило места без пересечений: " +
                      "увеличь Layout Scale, уменьши Branch Count или смени Branch Seed.", this);
    }

    // Кубический Эрмит через точки; на концах заданы направления, внутри касательные по Катмулу-Рому.
    static List<Vector2> Hermite(List<Vector2> c, Vector2 t0, Vector2 t1, float step)
    {
        int k = c.Count;
        var m = new Vector2[k];
        for (int i = 1; i < k - 1; i++)
            m[i] = 0.5f * (c[i + 1] - c[i - 1]);
        m[0] = t0 * Vector2.Distance(c[0], c[1]);
        m[k - 1] = t1 * Vector2.Distance(c[k - 1], c[k - 2]);
        var o = new List<Vector2>();
        for (int i = 0; i < k - 1; i++)
        {
            float len = Vector2.Distance(c[i], c[i + 1]);
            int steps = Mathf.Max(2, Mathf.CeilToInt(len * 1.2f / step));
            for (int q = 0; q < steps; q++)
            {
                float u = (float)q / steps;
                float u2 = u * u, u3 = u2 * u;
                o.Add((2f * u3 - 3f * u2 + 1f) * c[i] + (u3 - 2f * u2 + u) * m[i] +
                      (-2f * u3 + 3f * u2) * c[i + 1] + (u3 - u2) * m[i + 1]);
            }
        }
        o.Add(c[k - 1]);
        return o;
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

            case Layout.Star:
                AddStar(w);
                break;

            case Layout.MooreFractal:
                AddMoore(w);
                break;

            case Layout.KochSnowflake:
                AddKoch(w);
                break;

            case Layout.Custom:
                for (int i = 0; i < customPoints.Count; i++)
                    w.Add(new Wp(customPoints[i], customRadius, i < customHeights.Count ? customHeights[i] : 0f));
                break;
        }

        for (int i = 0; i < w.Count; i++)
            w[i] = new Wp(w[i].p * layoutScale, w[i].r * layoutScale, w[i].h * layoutScale);
        return TileRow(w, repeatCount, repeatGap);
    }

    // Повтор: n копий в ряд вдоль X. У соседних копий вырезается по одному ребру (правое у левой копии,
    // левое у правой) и концы соединяются двумя перемычками. Получается одна замкнутая линия: низ копий слева направо,
    // потом верх копий справа налево. Все остальные углы фигуры остаются на месте.
    static List<Wp> TileRow(List<Wp> b, int n, float gap)
    {
        int m = b.Count;
        if (n <= 1 || m < 4) return b;

        int jr = 0;
        for (int j = 1; j < m; j++)
            if (b[j].p.x + b[(j + 1) % m].p.x > b[jr].p.x + b[(jr + 1) % m].p.x) jr = j;
        int jl = -1;
        for (int j = 0; j < m; j++)
        {
            if (j == jr || (j + 1) % m == jr || (jr + 1) % m == j) continue;
            if (jl < 0 || b[j].p.x + b[(j + 1) % m].p.x < b[jl].p.x + b[(jl + 1) % m].p.x) jl = j;
        }
        if (jl < 0) return b;

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < m; i++)
        {
            minX = Mathf.Min(minX, b[i].p.x); maxX = Mathf.Max(maxX, b[i].p.x);
            minY = Mathf.Min(minY, b[i].p.y); maxY = Mathf.Max(maxY, b[i].p.y);
        }
        Vector2 c = new Vector2(0.5f * (minX + maxX), 0.5f * (minY + maxY));
        float pitch = (maxX - minX) + gap;

        int lStart = (jl + 1) % m;   // начало «нижней» дуги (за вырезанным левым ребром)
        int rEnd = jr;               // конец «нижней» дуги (перед вырезанным правым ребром)
        int rStart = (jr + 1) % m;   // начало «верхней» дуги
        int lEnd = jl;               // конец «верхней» дуги

        var o = new List<Wp>(m * n);
        void Arc(int copy, int a, int z, bool skipEnds)
        {
            float dx = (copy - 0.5f * (n - 1)) * pitch;
            int i = a;
            bool first = true;
            while (true)
            {
                bool last = i == z;
                if (!(skipEnds && (first || last)))
                {
                    var p = b[i];
                    o.Add(new Wp(p.p - c + new Vector2(dx, 0f), p.r, p.h));
                }
                if (last) break;
                first = false;
                i = (i + 1) % m;
            }
        }

        for (int k = 0; k < n; k++) Arc(k, lStart, rEnd, false);
        for (int k = n - 1; k >= 0; k--) Arc(k, rStart, lEnd, false);
        return o;
    }

    static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }

    // Звезда {n/k}: вершины на окружности, дорога соединяет каждую k-ю. k подбирается взаимно простым с n,
    // иначе звезда распадается на несколько отдельных фигур (например 6/2 = два треугольника).
    void AddStar(List<Wp> w)
    {
        int n = starPoints;
        int hi = Mathf.Max(1, (n - 1) / 2);
        int k = Mathf.Clamp(starSkip, 1, hi);
        int pick = 1;
        bool found = false;
        for (int c = k; c <= hi && !found; c++) if (Gcd(n, c) == 1) { pick = c; found = true; }
        for (int c = k - 1; c >= 1 && !found; c--) if (Gcd(n, c) == 1) { pick = c; found = true; }
        if (pick != starSkip)
            Debug.Log($"Звезда {n} лучей: Star Skip {starSkip} не подходит, взят {pick}", this);
        for (int j = 0; j < n; j++)
        {
            float a = -Mathf.PI * 0.5f + 2f * Mathf.PI * ((j * pick) % n) / n;
            w.Add(new Wp(starRadius * Mathf.Cos(a), starRadius * Mathf.Sin(a), starTipRadius));
        }
    }

    // L-система: правила применяются order раз, потом «черепашка» рисует путь (F = шаг, +/- = поворот).
    static string LSystem(string axiom, Dictionary<char, string> rules, int order)
    {
        string s = axiom;
        for (int i = 0; i < order; i++)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s)
            {
                string r;
                if (rules.TryGetValue(ch, out r)) sb.Append(r); else sb.Append(ch);
            }
            s = sb.ToString();
        }
        return s;
    }

    static List<Vector2> Turtle(string s, float step, float turnDeg)
    {
        var pts = new List<Vector2> { Vector2.zero };
        Vector2 pos = Vector2.zero;
        float ang = 0f;
        foreach (char ch in s)
        {
            if (ch == 'F')
            {
                pos += step * new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                pts.Add(pos);
            }
            else if (ch == '+') ang += turnDeg * Mathf.Deg2Rad;
            else if (ch == '-') ang -= turnDeg * Mathf.Deg2Rad;
        }
        return pts;
    }

    static void AddCentered(List<Wp> w, List<Vector2> pts, float r)
    {
        Vector2 mn = pts[0], mx = pts[0];
        foreach (var p in pts) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
        Vector2 c = 0.5f * (mn + mx);
        foreach (var p in pts) w.Add(new Wp(p - c, r));
    }

    // Кривая Мура (замкнутая версия кривой Гильберта): заполняет квадрат, сама себя не пересекает.
    // Порядок 3 = 64 клетки, около 4 км при клетке 70 м.
    void AddMoore(List<Wp> w)
    {
        int order = Mathf.Clamp(fractalOrder, 2, 4);
        string s = LSystem("LFL+F+LFL",
            new Dictionary<char, string> { { 'L', "-RF+LFL+FR-" }, { 'R', "+LF-RFR-FL+" } }, order - 1);
        var pts = Turtle(s, fractalSize, 90f);
        if (pts.Count > 1 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude < 0.01f) pts.RemoveAt(pts.Count - 1);
        AddCentered(w, pts, fractalSize * 0.4f);
    }

    // Снежинка Коха: треугольник, у которого каждая сторона превращена в «зубец». Уровень 2 = 48 сторон.
    // Уровень 3 не влезает в радиусы скруглений.
    void AddKoch(List<Wp> w)
    {
        int level = Mathf.Clamp(fractalOrder, 1, 2);
        float side = fractalSize > 200f ? fractalSize : 900f;
        string s = LSystem("F--F--F", new Dictionary<char, string> { { 'F', "F+F--F+F" } }, level);
        var pts = Turtle(s, side / Mathf.Pow(3f, level), 60f);
        if (pts.Count > 1 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude < 0.01f) pts.RemoveAt(pts.Count - 1);
        AddCentered(w, pts, Mathf.Min(35f, side / 26f));
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

            // два сглаживания по кругу: лёгкое (повторяет рельеф) и тяжёлое (плавный «мост» через неровности)
            int hwL = Mathf.Max(1, Mathf.RoundToInt(terrainSmoothing / ds * 0.5f));
            int hwH = Mathf.Max(hwL, Mathf.RoundToInt(terrainSmoothing * 3.5f / ds * 0.5f));
            hwL = Mathf.Min(hwL, n / 2 - 1);
            hwH = Mathf.Min(hwH, n / 2 - 1);
            float[] light = CircAverage(raw, hwL);
            float[] heavy = CircAverage(raw, hwH);
            for (int i = 0; i < n; i++)
                baseH[i] = Mathf.Lerp(heavy[i], light[i], terrainHug);

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

    static float[] CircAverage(float[] raw, int hw)
    {
        int n = raw.Length;
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0f;
            for (int k = -hw; k <= hw; k++)
                sum += raw[((i + k) % n + n) % n];
            o[i] = sum / (2 * hw + 1);
        }
        return o;
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