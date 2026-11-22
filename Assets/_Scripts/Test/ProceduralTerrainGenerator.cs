using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Процедурный ландшафт: холмы/горы/долины, море, 8 материалов по биомам, трава, цветы, деревья, кактусы, камни.
/// Повесь на Terrain и жми Generate в инспекторе (работает в редакторе, без Play).
[RequireComponent(typeof(Terrain))]
public class SimpleTerrain : MonoBehaviour
{
    public int seed = 1;
    public int size = 1000;               // ширина/длина карты, м
    public int height = 250;              // максимальная высота, м
    public float hillScale = 3f;          // сколько крупных холмов на карту (больше = мельче рельеф)
    [Range(0f, 1f)] public float mountainAmount = 0.7f;   // сила острых горных хребтов
    [Range(0f, 1f)] public float seaLevel = 0.28f;        // уровень моря (доля высоты)
    public bool islandEdges = false;                      // true = только края карты уходят в море, остальное - суша на всю площадь
    [Range(0.5f, 0.98f)] public float islandStart = 0.8f; // где начинается спуск к морю (доля от центра до края)
    public float moistureScale = 2.2f;    // размер биомов (больше = мельче пятна лесов/пустынь)

    [Header("Количество и дальность прорисовки")]
    [Range(0f, 3f)] public float treeDensity = 0.5f;
    [Min(0)] public int maxTrees = 3000;                  // жёсткий лимит деревьев/кактусов/камней
    [Range(0f, 3f)] public float grassDensity = 1.4f;
    [Min(10f)] public float treeDrawDistance = 800f;      // дальность отрисовки деревьев
    [Min(10f)] public float billboardDistance = 220f;     // с какой дистанции деревья переходят в 2D-билборд
    [Min(10f)] public float grassDrawDistance = 260f;     // дальность отрисовки травы
    [Min(1)] public int maxFullLodTrees = 150;
    public bool cullByDistance = false;                  // прятать деревья дальше treeDrawDistance (LODGroup). Если деревья пропадают - выключи            // сколько ближайших деревьев рендерятся в полном 3D

    [Header("Что генерировать (галочки)")]
    public bool makeGrass = true;
    public bool makeFlowers = true;
    public bool makeOaks = true;
    public bool makePines = true;
    public bool makeCacti = true;
    public bool makeRocks = true;

    [Header("Материал террейна")]
    public Material terrainMaterial;          // твой материал; пусто = не трогать то, что стоит на Terrain
    public bool autoTerrainMaterial = false;  // true = скрипт сам поставит шейдер террейна под URP/HDRP

    [Header("Материалы земли: перетащи свои (пусто = нарисуется само)")]
    public Material seabedMat;      // дно под водой
    public Material sandMat;        // пляж
    public Material meadowMat;      // луг
    public Material forestMat;      // лесная земля
    public Material steppeMat;      // сухая степь
    public Material dirtMat;        // горная земля
    public Material rockMat;        // скалы
    public Material snowMat;        // снег
    [Header("Или просто текстура (приоритет выше материала). Кидай сюда картинки из папок с текстурами")]
    public Texture2D seabedTex;
    public Texture2D sandTex;
    public Texture2D meadowTex;
    public Texture2D forestTex;
    public Texture2D steppeTex;
    public Texture2D dirtTex;
    public Texture2D rockTex;
    public Texture2D snowTex;
    public float tileSize = 12f;     // размер одного повтора своих текстур, м

    [Header("Трава и цветы (материал/цвет/текстуры)")]
    public Material grassMaterial;              // свой материал травы (если задан — задаст оттенок и текстуру)
    public Color grassColor = new Color(0.36f, 0.72f, 0.22f);
    public Texture2D grassTex;
    public Texture2D dryGrassTex;
    public Texture2D flowersTex;

    [Header("Свои модели (префабы, пусто = простые из кода)")]
    public GameObject oakPrefab;
    public GameObject pinePrefab;
    public GameObject cactusPrefab;
    public GameObject rockPrefab;

    [Header("Размер объектов (мин / макс)")]
    [Min(0.01f)] public float treeMinScale = 0.8f;
    [Min(0.01f)] public float treeMaxScale = 1.6f;
    [Min(0.01f)] public float rockMinScale = 0.5f;
    [Min(0.01f)] public float rockMaxScale = 3.2f;

    const int G = 1024;                   // разрешение карты биомов / текстур / травы
    Vector2 moistOff, jitOff;

    // Номера слоёв-материалов
    const int SEABED = 0, SAND = 1, MEADOW = 2, FOREST = 3, STEPPE = 4, DIRT = 5, ROCK = 6, SNOW = 7;

    private Terrain _terrain;
    float _lastTMin = -1f, _lastTMax = -1f, _lastRMin = -1f, _lastRMax = -1f;

    // Размер дерева/камня = Lerp(мин, макс, случайное число от позиции). Одна формула и при генерации, и при смене размеров,
    // поэтому размеры можно менять на лету, не перегенерируя весь ландшафт.
    static float Hash01(int x, int y)
    {
        float s = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    void OnValidate()
    {
        if (maxTrees < 0) maxTrees = 0;
        if (treeDrawDistance < 10f) treeDrawDistance = 10f;
        if (billboardDistance < 10f) billboardDistance = 10f;
        if (grassDrawDistance < 10f) grassDrawDistance = 10f;
        if (maxFullLodTrees < 1) maxFullLodTrees = 1;

        Terrain t = GetComponent<Terrain>();
        if (t != null)
            ApplyDrawDistances(t);

#if UNITY_EDITOR
        // Поменял размеры в инспекторе -> деревья, кактусы и камни пересчитываются сразу
        if (!Mathf.Approximately(_lastTMin, treeMinScale) || !Mathf.Approximately(_lastTMax, treeMaxScale) ||
            !Mathf.Approximately(_lastRMin, rockMinScale) || !Mathf.Approximately(_lastRMax, rockMaxScale))
        {
            _lastTMin = treeMinScale; _lastTMax = treeMaxScale; _lastRMin = rockMinScale; _lastRMax = rockMaxScale;
            EditorApplication.delayCall += ApplyObjectSizes;
        }
#endif
    }

    /// Пересчитывает размеры уже расставленных объектов по текущим min/max (без полной генерации)
    [ContextMenu("Apply Object Sizes")]
    public void ApplyObjectSizes()
    {
        if (this == null) return;
        Terrain terrain = GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null) return;
        TerrainData td = terrain.terrainData;

        float tMin = Mathf.Max(0.01f, Mathf.Min(treeMinScale, treeMaxScale));
        float tMax = Mathf.Max(0.01f, Mathf.Max(treeMinScale, treeMaxScale));
        float rMin = Mathf.Max(0.01f, Mathf.Min(rockMinScale, rockMaxScale));
        float rMax = Mathf.Max(0.01f, Mathf.Max(rockMinScale, rockMaxScale));

        TreeInstance[] trees = td.treeInstances;
        for (int i = 0; i < trees.Length; i++)
        {
            int gx = Mathf.RoundToInt(trees[i].position.x * (G - 1));
            int gy = Mathf.RoundToInt(trees[i].position.z * (G - 1));
            float sc = Mathf.Lerp(tMin, tMax, Hash01(gx, gy));
            trees[i].widthScale = sc;
            trees[i].heightScale = sc;
        }
        td.SetTreeInstances(trees, true);

        Transform root = transform.Find("GeneratedProps");
        if (root != null)
        {
            foreach (Transform c in root)
            {
                int gx = Mathf.RoundToInt(c.localPosition.x / size * (G - 1));
                int gy = Mathf.RoundToInt(c.localPosition.z / size * (G - 1));
                float f = Hash01(gx, gy);
                float sc = c.name.StartsWith("Rock") ? Mathf.Lerp(rMin, rMax, f) : Mathf.Lerp(tMin, tMax, f);
                c.localScale = Vector3.one * sc;
                SetupCulling(c.gameObject);
            }
        }
#if UNITY_EDITOR
        EditorUtility.SetDirty(td);
        SceneView.RepaintAll();
#endif
        terrain.Flush();
    }

    void ApplyDrawDistances(Terrain terrain)
    {
        terrain.detailObjectDensity = 1f;
        terrain.detailObjectDistance = grassDrawDistance;
        terrain.treeDistance = treeDrawDistance;
        terrain.treeBillboardDistance = Mathf.Min(billboardDistance, treeDrawDistance);
        terrain.treeMaximumFullLODCount = maxFullLodTrees;
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
#if UNITY_EDITOR
        try { Build(); }
        finally { EditorUtility.ClearProgressBar(); }
#else
        Build();
#endif
    }

    static void Progress(string s, float p)
    {
#if UNITY_EDITOR
        EditorUtility.DisplayProgressBar("Генерация ландшафта", s, p);
#endif
    }

    void Build()
    {
        Terrain terrain = GetComponent<Terrain>();
        _terrain = terrain;
        TerrainData td = terrain.terrainData;

        ApplyDrawDistances(terrain);

        var rnd0 = new System.Random(seed * 7 + 3);
        moistOff = new Vector2(rnd0.Next(0, 5000) / 5f, rnd0.Next(0, 5000) / 5f);
        jitOff = new Vector2(rnd0.Next(0, 5000) / 5f, rnd0.Next(0, 5000) / 5f);

        // ---------- 1. Рельеф ----------
        Progress("Рельеф", 0.05f);
        td.heightmapResolution = 513;
        td.size = new Vector3(size, height, size);
        td.SetHeights(0, 0, GenHeights(td.heightmapResolution));

        // ---------- 2. Карты высоты / уклона / влажности / биомов ----------
        Progress("Биомы", 0.2f);
        float[,] H = new float[G, G], S = new float[G, G], M = new float[G, G];
        byte[,] B = new byte[G, G];
        for (int y = 0; y < G; y++)
            for (int x = 0; x < G; x++)
            {
                float nx = x / (G - 1f), ny = y / (G - 1f);
                float h = td.GetInterpolatedHeight(nx, ny) / height;
                float s = td.GetSteepness(nx, ny);
                float m = Moist(nx, ny);
                float jit = (Mathf.PerlinNoise(nx * 30 + jitOff.x, ny * 30 + jitOff.y) - 0.5f) * 0.05f;
                H[y, x] = h;
                S[y, x] = s;
                M[y, x] = m;
                B[y, x] = (byte)Biome(h + jit, m + jit, s);
            }

        // ---------- 3. Материалы (текстуры создаются кодом) ----------
        Progress("Текстуры", 0.4f);
        td.terrainLayers = new TerrainLayer[]
        {
            Layer("0_Seabed", seabedTex, seabedMat, MakeTex("seabed", new Color(.26f,.23f,.18f), new Color(.46f,.40f,.30f), 5, 40, 1f, false, 2.2f), 12),
            Layer("1_Sand",   sandTex, sandMat,   MakeTex("sand",   new Color(.80f,.72f,.48f), new Color(.94f,.87f,.64f), 4, 30, 4f, false, 1.8f), 12),
            Layer("2_Meadow", meadowTex, meadowMat, MakeTex("meadow", new Color(.18f,.42f,.10f), new Color(.42f,.68f,.22f), 6, 60, .12f, false, 2.4f), 10),
            Layer("3_Forest", forestTex, forestMat, MakeTex("forest", new Color(.08f,.19f,.06f), new Color(.30f,.30f,.12f), 7, 50, .3f, false, 2.6f), 10),
            Layer("4_Steppe", steppeTex, steppeMat, MakeTex("steppe", new Color(.52f,.46f,.20f), new Color(.80f,.72f,.36f), 6, 60, .12f, false, 2.2f), 10),
            Layer("5_Dirt",   dirtTex, dirtMat,   MakeTex("dirt",   new Color(.27f,.19f,.12f), new Color(.52f,.40f,.26f), 6, 35, 1f, false, 2.4f), 12),
            Layer("6_Rock",   rockTex, rockMat,   MakeTex("rock",   new Color(.24f,.24f,.26f), new Color(.64f,.62f,.60f), 4, 20, 1f, true, 2.6f), 18),
            Layer("7_Snow",   snowTex, snowMat,   MakeTex("snow",   new Color(.82f,.86f,.94f), new Color(1f,1f,1f), 4, 25, 1f, false, 1.4f), 16),
        };

        // Альфа-карта: у каждого пикселя свой биом, границы размываются -> плавные рваные переходы
        Progress("Раскраска", 0.55f);
        td.alphamapResolution = G;
        float[,,] map = new float[G, G, 8];
        const int R = 2;
        float wgt = 1f / ((2 * R + 1) * (2 * R + 1));
        for (int y = 0; y < G; y++)
            for (int x = 0; x < G; x++)
                for (int dy = -R; dy <= R; dy++)
                {
                    int yy = Mathf.Clamp(y + dy, 0, G - 1);
                    for (int dx = -R; dx <= R; dx++)
                    {
                        int xx = Mathf.Clamp(x + dx, 0, G - 1);
                        map[y, x, B[yy, xx]] += wgt;
                    }
                }
        td.SetAlphamaps(0, 0, map);

        // ---------- 4. Трава и цветы ----------
        Progress("Трава", 0.7f);
        ApplyGrassMaterialOverrides();

        Texture2D resolvedGrassTex = grassTex != null ? grassTex : BladeTex("GrassGreen", false);
        Texture2D resolvedDryTex = dryGrassTex != null ? dryGrassTex : BladeTex("GrassDry", false);
        Texture2D resolvedFlowerTex = flowersTex != null ? flowersTex : BladeTex("Flowers", true);

        Color healthyGreen = grassColor;
        Color dryGreen = new Color(
            Mathf.Clamp01(grassColor.r * 1.25f),
            Mathf.Clamp01(grassColor.g * 0.95f),
            Mathf.Clamp01(grassColor.b * 0.85f)
        );

        td.SetDetailResolution(G, 16);
        td.detailPrototypes = new DetailPrototype[]
        {
            new DetailPrototype
            {
                prototypeTexture = resolvedGrassTex,
                renderMode = DetailRenderMode.GrassBillboard,
                minWidth = 1.3f, maxWidth = 2.4f,
                minHeight = 0.9f, maxHeight = 1.8f,
                noiseSpread = 0.45f,
                healthyColor = healthyGreen,
                dryColor = dryGreen
            },
            new DetailPrototype
            {
                prototypeTexture = resolvedDryTex,
                renderMode = DetailRenderMode.GrassBillboard,
                minWidth = 1.2f, maxWidth = 2.2f,
                minHeight = 0.75f, maxHeight = 1.45f,
                noiseSpread = 0.4f,
                healthyColor = new Color(.84f, .74f, .36f),
                dryColor = new Color(.62f, .50f, .25f)
            },
            new DetailPrototype
            {
                prototypeTexture = resolvedFlowerTex,
                renderMode = DetailRenderMode.GrassBillboard,
                minWidth = 1.0f, maxWidth = 1.7f,
                minHeight = 0.75f, maxHeight = 1.3f,
                noiseSpread = 0.6f,
                healthyColor = Color.white,
                dryColor = new Color(.92f, .92f, .84f)
            },
        };

        int[,] gGreen = new int[G, G], gDry = new int[G, G], gFlower = new int[G, G];
        for (int y = 0; y < G; y++)
            for (int x = 0; x < G; x++)
            {
                float nx = x / (G - 1f), ny = y / (G - 1f);
                float clump = Mathf.PerlinNoise(nx * 45 + 311f, ny * 45 + 311f);
                int b = B[y, x];
                if (!makeGrass) continue;

                if (b == MEADOW)
                {
                    gGreen[y, x] = Mathf.RoundToInt(11f * grassDensity * (0.45f + clump));
                    if (makeFlowers && Mathf.PerlinNoise(nx * 70 + 77f, ny * 70 + 77f) > 0.64f)
                        gFlower[y, x] = Mathf.RoundToInt(3f * grassDensity);
                }
                else if (b == FOREST)
                {
                    gGreen[y, x] = Mathf.RoundToInt(6f * grassDensity * (0.4f + clump));
                }
                else if (b == STEPPE)
                {
                    gDry[y, x] = Mathf.RoundToInt(8f * grassDensity * (0.4f + clump));
                    gGreen[y, x] = Mathf.RoundToInt(2f * grassDensity * clump);
                }
            }
        td.SetDetailLayer(0, 0, 0, gGreen);
        td.SetDetailLayer(0, 0, 1, gDry);
        td.SetDetailLayer(0, 0, 2, gFlower);

        // ---------- 5. Деревья на Terrain + кактусы и камни отдельными объектами ----------
        Progress("Деревья и объекты", 0.85f);
        BuildTreesAndProps(td, H, S, M, B);

        if (terrainMaterial != null) terrain.materialTemplate = terrainMaterial;
        else if (autoTerrainMaterial) FixTerrainMaterial(terrain);
        td.baseMapResolution = 1024;
        terrain.basemapDistance = 3000f;
        ApplyDrawDistances(terrain);
        terrain.Flush();

        // ---------- 6. Вода ----------
        Progress("Вода", 0.95f);
        Transform w = transform.Find("Water");
        if (w == null)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Water";
            DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false);
            w = g.transform;
            g.GetComponent<MeshRenderer>().sharedMaterial = MakeMat("Water", new Color(.08f, .33f, .52f, .7f), true, 0.9f);
        }
        else
        {
            var mr = w.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial == null)
                mr.sharedMaterial = MakeMat("Water", new Color(.08f, .33f, .52f, .7f), true, 0.9f);
        }
        w.localPosition = new Vector3(size / 2f, seaLevel * height, size / 2f);
        w.localScale = new Vector3(size / 10f * 3f, 1, size / 10f * 3f);

        GetComponent<TerrainCollider>().terrainData = td;
#if UNITY_EDITOR
        EditorUtility.SetDirty(td);
        EditorUtility.SetDirty(this);
        SceneView.RepaintAll();
#endif
    }

    void ApplyGrassMaterialOverrides()
    {
        if (grassMaterial == null) return;
        Vector2 dummy;
        Texture2D matTex = MatTex(grassMaterial, new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_Diffuse" }, true, out dummy);
        if (matTex != null && grassTex == null) grassTex = matTex;

        foreach (string prop in new[] { "_BaseColor", "_Color" })
        {
            if (grassMaterial.HasProperty(prop))
            {
                Color c = grassMaterial.GetColor(prop);
                if (c.maxColorComponent > 0.01f) grassColor = c;
                break;
            }
        }
    }

    static float SS(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    float[,] GenHeights(int res)
    {
        const int oct = 6;
        var r = new System.Random(seed);
        Vector2[] off = new Vector2[oct];
        for (int i = 0; i < oct; i++) off[i] = new Vector2(r.Next(0, 5000) / 5f, r.Next(0, 5000) / 5f);

        float[,] fb = new float[res, res], rd = new float[res, res];
        float fmin = 9, fmax = -9, rmin = 9, rmax = -9;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float nx = x / (res - 1f), ny = y / (res - 1f);
                float amp = 1, f = 1, sum = 0, fbm = 0, ridge = 0;
                for (int o = 0; o < oct; o++)
                {
                    float p = Mathf.PerlinNoise(nx * hillScale * f + off[o].x, ny * hillScale * f + off[o].y);
                    float rr = 1f - Mathf.Abs(2f * p - 1f);
                    fbm += p * amp;
                    ridge += rr * rr * amp;
                    sum += amp;
                    amp *= 0.5f;
                    f *= 2f;
                }
                fbm /= sum;
                ridge /= sum;
                fb[y, x] = fbm;
                rd[y, x] = ridge;
                fmin = Mathf.Min(fmin, fbm);
                fmax = Mathf.Max(fmax, fbm);
                rmin = Mathf.Min(rmin, ridge);
                rmax = Mathf.Max(rmax, ridge);
            }

        float[,] h = new float[res, res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float b = Mathf.InverseLerp(fmin, fmax, fb[y, x]);
                float m = Mathf.InverseLerp(rmin, rmax, rd[y, x]);
                float mask = SS(0.55f, 0.85f, b) * mountainAmount;
                float v = Mathf.Clamp01(Mathf.Lerp(b, m, mask));

                if (islandEdges)
                {
                    float dx = x / (res - 1f) * 2f - 1f, dy = y / (res - 1f) * 2f - 1f;
                    float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    v *= 1f - SS(islandStart, 1f, d);
                }
                h[y, x] = v;
            }
        return h;
    }

    float Moist(float nx, float ny)
    {
        float v = 0, a = 1, f = 1, s = 0;
        for (int o = 0; o < 3; o++)
        {
            v += Mathf.PerlinNoise(nx * moistureScale * f + moistOff.x, ny * moistureScale * f + moistOff.y) * a;
            s += a;
            a *= 0.5f;
            f *= 2f;
        }
        return Mathf.Clamp01(v / s);
    }

    int Biome(float h, float m, float s)
    {
        if (h < seaLevel) return SEABED;
        if (h < seaLevel + 0.015f) return SAND;
        if (h > 0.82f && s < 48f) return SNOW;
        if (s > 38f) return ROCK;
        if (h > 0.68f) return s > 26f ? ROCK : DIRT;

        if (m < 0.22f) return STEPPE;
        if (m < 0.58f) return MEADOW;
        return FOREST;
    }

    void BuildTreesAndProps(TerrainData td, float[,] H, float[,] S, float[,] M, byte[,] B)
    {
        // Деревья - обычные объекты (а не Terrain Trees): так работает любой шейдер/пайплайн,
        // нет предупреждения про Nature/Soft Occlusion, а размер меняется на лету.
        td.SetTreeInstances(System.Array.Empty<TreeInstance>(), false);
        td.treePrototypes = System.Array.Empty<TreePrototype>();
        td.RefreshPrototypes();

        Transform propsRoot = transform.Find("GeneratedProps");
        if (propsRoot != null)
            DestroyImmediate(propsRoot.gameObject);

        if (maxTrees <= 0 || treeDensity <= 0f)
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(td);
#endif
            if (_terrain != null) _terrain.Flush();
            return;
        }

        Material barkM = MakeMat("Tree_Bark", new Color(.30f, .20f, .12f), false, 0.05f);
        Material oakM = MakeMat("Tree_OakLeaves", new Color(.17f, .42f, .09f), false, 0.05f);
        Material pineM = MakeMat("Tree_PineLeaves", new Color(.06f, .28f, .12f), false, 0.05f);
        Material cactM = MakeMat("Tree_Cactus", new Color(.20f, .50f, .25f));
        Material rockM = MakeMat("Tree_Rock", new Color(.42f, .41f, .40f), false, 0.05f);
        Mesh cone = MakeCone();

        // Дуб: ствол и крона - два подмеша с разными материалами
        GameObject oakTrunk = new GameObject("OakTrunk");
        AddPart(oakTrunk, PrimitiveType.Cylinder, new Vector3(0, 2, 0), new Vector3(.7f, 2, .7f));
        GameObject oakCrown = new GameObject("OakCrown");
        AddPart(oakCrown, PrimitiveType.Sphere, new Vector3(0, 5.5f, 0), new Vector3(5, 4, 5));
        AddPart(oakCrown, PrimitiveType.Sphere, new Vector3(1.8f, 4.6f, .8f), new Vector3(3.5f, 3, 3.5f));
        AddPart(oakCrown, PrimitiveType.Sphere, new Vector3(-1.6f, 4.9f, -1f), new Vector3(3.8f, 3.2f, 3.8f));
        Mesh[] oakMeshes = TwoMeshes("Oak", oakTrunk, oakCrown);
        DestroyImmediate(oakTrunk);
        DestroyImmediate(oakCrown);

        // Сосна
        GameObject pineTrunk = new GameObject("PineTrunk");
        AddPart(pineTrunk, PrimitiveType.Cylinder, new Vector3(0, 3, 0), new Vector3(.5f, 3, .5f));
        GameObject pineCrown = new GameObject("PineCrown");
        AddCustomPart(pineCrown, cone, new Vector3(0, 1.5f, 0), new Vector3(4f, 3.2f, 4f));
        AddCustomPart(pineCrown, cone, new Vector3(0, 3.5f, 0), new Vector3(3.2f, 3f, 3.2f));
        AddCustomPart(pineCrown, cone, new Vector3(0, 5.5f, 0), new Vector3(2.4f, 2.8f, 2.4f));
        AddCustomPart(pineCrown, cone, new Vector3(0, 7.5f, 0), new Vector3(1.5f, 2.6f, 1.5f));
        Mesh[] pineMeshes = TwoMeshes("Pine", pineTrunk, pineCrown);
        DestroyImmediate(pineTrunk);
        DestroyImmediate(pineCrown);

        GameObject cactusTemplate = new GameObject("CactusTemplate");
        AddPart(cactusTemplate, PrimitiveType.Capsule, new Vector3(0, 2, 0), new Vector3(.7f, 2, .7f));
        AddPart(cactusTemplate, PrimitiveType.Capsule, new Vector3(.55f, 1.5f, 0), new Vector3(.3f, .5f, .3f), new Vector3(0, 0, 90));
        AddPart(cactusTemplate, PrimitiveType.Capsule, new Vector3(1f, 2.3f, 0), new Vector3(.3f, .8f, .3f));
        AddPart(cactusTemplate, PrimitiveType.Capsule, new Vector3(-.55f, 2.1f, 0), new Vector3(.3f, .5f, .3f), new Vector3(0, 0, 90));
        AddPart(cactusTemplate, PrimitiveType.Capsule, new Vector3(-1f, 2.8f, 0), new Vector3(.3f, .7f, .3f));
        BakeRootMesh(cactusTemplate, cactM);
        Mesh cactusMesh = cactusTemplate.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(cactusTemplate);

        GameObject rockTemplate = new GameObject("RockTemplate");
        AddPart(rockTemplate, PrimitiveType.Sphere, new Vector3(0, .5f, 0), new Vector3(2.4f, 1.5f, 2f), new Vector3(0, 20, 5));
        AddPart(rockTemplate, PrimitiveType.Sphere, new Vector3(1.2f, .3f, .6f), new Vector3(1.3f, .9f, 1.2f), new Vector3(10, 60, 0));
        BakeRootMesh(rockTemplate, rockM);
        Mesh rockMesh = rockTemplate.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(rockTemplate);

        Debug.Log("Деревья: дуб (ствол " + oakMeshes[0].vertexCount + " верш., крона " + oakMeshes[1].vertexCount +
                  " верш.), сосна (ствол " + pineMeshes[0].vertexCount + ", крона " + pineMeshes[1].vertexCount +
                  "). Шейдер кроны: " + (oakM.shader != null ? oakM.shader.name : "НЕТ") +
                  ", шейдер коры: " + (barkM.shader != null ? barkM.shader.name : "НЕТ"));

        Debug.Log("Свои префабы в полях: дуб = " + (oakPrefab != null ? oakPrefab.name : "нет (беру встроенный)") +
                  ", сосна = " + (pinePrefab != null ? pinePrefab.name : "нет (беру встроенную)") +
                  ", кактус = " + (cactusPrefab != null ? cactusPrefab.name : "нет") +
                  ", камень = " + (rockPrefab != null ? rockPrefab.name : "нет"));

        Material[] oakMats = { barkM, oakM };
        Material[] pineMats = { barkM, pineM };
        Material[] cactMats = { cactM };
        Material[] rockMats = { rockM };

        GameObject containerGo = new GameObject("GeneratedProps");
        containerGo.transform.SetParent(transform, false);
        propsRoot = containerGo.transform;

        var rnd = new System.Random(seed + 7);
        int attempts = Mathf.Clamp((int)(size * size / 16f * treeDensity), 100, maxTrees * 12);

        float tMin = Mathf.Max(0.01f, Mathf.Min(treeMinScale, treeMaxScale));
        float tMax = Mathf.Max(0.01f, Mathf.Max(treeMinScale, treeMaxScale));
        float rMin = Mathf.Max(0.01f, Mathf.Min(rockMinScale, rockMaxScale));
        float rMax = Mathf.Max(0.01f, Mathf.Max(rockMinScale, rockMaxScale));

        int totalSpawned = 0;
        int[] counts = new int[4];

        for (int a = 0; a < attempts && totalSpawned < maxTrees; a++)
        {
            int gx = rnd.Next(G), gy = rnd.Next(G);
            int b = B[gy, gx];
            float h = H[gy, gx], m = M[gy, gx];
            float p = (float)rnd.NextDouble();
            int type = -1;

            switch (b)
            {
                case FOREST: if (p < 0.30f) type = (h > 0.5f || rnd.NextDouble() < 0.3) ? 1 : 0; break;
                case MEADOW: if (p < 0.015f) type = 0; break;
                case STEPPE: if (p < 0.008f) type = 0; break;
                case DIRT: if (p < 0.03f && h < 0.76f) type = 1; break;
                case SAND: if (m < 0.25f && h > seaLevel + 0.01f && p < 0.01f) type = 2; break;
                case ROCK: if (p < 0.05f) type = 3; break;
            }

            if (type < 0) continue;
            if ((type == 0 && !makeOaks) || (type == 1 && !makePines) || (type == 2 && !makeCacti) || (type == 3 && !makeRocks))
                continue;

            float nx = gx / (G - 1f);
            float ny = gy / (G - 1f);
            float rotY = (float)rnd.NextDouble() * 360f;
            float sc = type == 3 ? Mathf.Lerp(rMin, rMax, Hash01(gx, gy)) : Mathf.Lerp(tMin, tMax, Hash01(gx, gy));

            switch (type)
            {
                case 0: SpawnProp(propsRoot, oakPrefab, oakMeshes, oakMats, "Oak", nx, ny, td, sc, rotY); break;
                case 1: SpawnProp(propsRoot, pinePrefab, pineMeshes, pineMats, "Pine", nx, ny, td, sc, rotY); break;
                case 2: SpawnProp(propsRoot, cactusPrefab, new[] { cactusMesh }, cactMats, "Cactus", nx, ny, td, sc, rotY); break;
                default: SpawnProp(propsRoot, rockPrefab, new[] { rockMesh }, rockMats, "Rock", nx, ny, td, sc, rotY); break;
            }
            counts[type]++;
            totalSpawned++;
        }

        Debug.Log("Заспавнено: дубов " + counts[0] + ", сосен " + counts[1] + ", кактусов " + counts[2] + ", камней " + counts[3] +
                  ". Смотри в Hierarchy: " + transform.name + " > GeneratedProps > Oak > Part0 (ствол) и Part1 (крона)");

#if UNITY_EDITOR
        EditorUtility.SetDirty(td);
        EditorUtility.SetDirty(propsRoot.gameObject);
#endif
        if (_terrain != null)
            _terrain.Flush();
    }

    void SpawnProp(Transform parent, GameObject customPrefab, Mesh[] fallbackMeshes, Material[] fallbackMats, string label, float nx, float ny, TerrainData td, float scale, float rotY)
    {
        float wx = nx * size;
        float wz = ny * size;
        float wy = td.GetInterpolatedHeight(nx, ny);

        GameObject go;
#if UNITY_EDITOR
        if (customPrefab != null && EditorUtility.IsPersistent(customPrefab))
            go = (GameObject)PrefabUtility.InstantiatePrefab(customPrefab, parent);
        else
#endif
        {
            go = new GameObject(label);
            go.transform.SetParent(parent, false);
            for (int i = 0; i < fallbackMeshes.Length; i++)
            {
                // у дерева ствол и крона - отдельные дочерние объекты со своими материалами
                GameObject part = fallbackMeshes.Length == 1 ? go : new GameObject("Part" + i);
                if (part != go) part.transform.SetParent(go.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = fallbackMeshes[i];
                part.AddComponent<MeshRenderer>().sharedMaterial = fallbackMats[i];
            }
        }

        go.name = label;   // по имени потом отличаем камни от деревьев при смене размеров
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(wx, wy, wz);
        go.transform.localEulerAngles = new Vector3(0f, rotY, 0f);
        go.transform.localScale = Vector3.one * scale;
        SetupCulling(go);
    }

    // Отсечение по дальности (treeDrawDistance): за этой дистанцией объект не рисуется
    void SetupCulling(GameObject go)
    {
        if (!cullByDistance)
        {
            LODGroup old = go.GetComponent<LODGroup>();
            if (old != null) DestroyImmediate(old);
            return;
        }
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        LODGroup lg = go.GetComponent<LODGroup>();
        if (lg == null) lg = go.AddComponent<LODGroup>();
        lg.fadeMode = LODFadeMode.None;
        lg.SetLODs(new[] { new LOD(0.001f, renderers) });
        lg.RecalculateBounds();
        float worldSize = lg.size * Mathf.Max(go.transform.lossyScale.x, Mathf.Max(go.transform.lossyScale.y, go.transform.lossyScale.z));
        float thr = Mathf.Clamp(worldSize / (1.1547f * Mathf.Max(10f, treeDrawDistance)), 0.0005f, 0.5f);
        lg.SetLODs(new[] { new LOD(thr, renderers) });
    }

    static Mesh CombineChildren(GameObject root)
    {
        MeshFilter[] mfs = root.GetComponentsInChildren<MeshFilter>();
        var ci = new List<CombineInstance>(mfs.Length);
        Matrix4x4 inv = root.transform.worldToLocalMatrix;
        foreach (MeshFilter mf in mfs)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            ci.Add(new CombineInstance { mesh = mf.sharedMesh, transform = inv * mf.transform.localToWorldMatrix });
        }
        Mesh m = new Mesh();
        m.CombineMeshes(ci.ToArray(), true, true);
        m.RecalculateBounds();
        return m;
    }

    // Два меша: [0] ствол, [1] крона (каждый под свой материал)
    static Mesh[] TwoMeshes(string name, GameObject trunkRoot, GameObject crownRoot)
    {
        return new[]
        {
            SaveMeshAsset(CombineChildren(trunkRoot), name + "_Trunk"),
            SaveMeshAsset(CombineChildren(crownRoot), name + "_Crown"),
        };
    }

    static void AddPart(GameObject root, PrimitiveType t, Vector3 pos, Vector3 scale, Vector3 rot = default(Vector3))
    {
        GameObject temp = GameObject.CreatePrimitive(t);
        Mesh m = temp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(temp);
        AddCustomPart(root, m, pos, scale, rot);
    }

    static void AddCustomPart(GameObject root, Mesh mesh, Vector3 pos, Vector3 scale, Vector3 rot = default(Vector3))
    {
        GameObject g = new GameObject("Part");
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.transform.SetParent(root.transform, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localEulerAngles = rot;
    }

    static void BakeRootMesh(GameObject root, Material mat)
    {
        var mfs = root.GetComponentsInChildren<MeshFilter>();
        var combines = new List<CombineInstance>(mfs.Length);
        Matrix4x4 invRoot = root.transform.worldToLocalMatrix;

        for (int i = 0; i < mfs.Length; i++)
        {
            if (mfs[i] == null || mfs[i].sharedMesh == null) continue;
            combines.Add(new CombineInstance
            {
                mesh = mfs[i].sharedMesh,
                transform = invRoot * mfs[i].transform.localToWorldMatrix
            });
        }

        for (int i = 0; i < mfs.Length; i++)
        {
            if (mfs[i] != null && mfs[i].gameObject != root)
                DestroyImmediate(mfs[i].gameObject);
        }

        Mesh combined = new Mesh { name = root.name + "_Mesh" };
        if (combines.Count > 0)
        {
            combined.CombineMeshes(combines.ToArray(), true, true);
            if (combined.vertexCount > 65000)
            {
                combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combined.CombineMeshes(combines.ToArray(), true, true);
            }
        }
        combined.RecalculateNormals();
        combined.RecalculateBounds();

        MeshFilter mf = root.GetComponent<MeshFilter>();
        if (mf == null) mf = root.AddComponent<MeshFilter>();
        mf.sharedMesh = SaveMeshAsset(combined, root.name + "_Mesh");

        MeshRenderer mr = root.GetComponent<MeshRenderer>();
        if (mr == null) mr = root.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat != null ? mat : MakeMat(root.name + "_Mat", new Color(.35f, .45f, .25f));
    }

    static Mesh SaveMeshAsset(Mesh mesh, string name)
    {
#if UNITY_EDITOR
        if (mesh == null) return null;
        EnsureFolder();
        mesh.name = name;
        string path = "Assets/GeneratedTerrain/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);   // старый меш удаляем целиком, чтобы не осталось устаревшей геометрии
        AssetDatabase.CreateAsset(mesh, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
#else
        return mesh;
#endif
    }

    static Mesh MakeCone()
    {
        const int n = 14;
        var v = new List<Vector3>();
        var tri = new List<int>();
        v.Add(new Vector3(0, 1, 0));
        v.Add(new Vector3(0, 0, 0));
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a) * .5f, 0, Mathf.Sin(a) * .5f));
        }
        for (int i = 0; i < n; i++)
        {
            int a = 2 + i, b = 2 + (i + 1) % n;
            tri.Add(0); tri.Add(b); tri.Add(a);
            tri.Add(1); tri.Add(a); tri.Add(b);
        }
        Mesh m = new Mesh { name = "ConeMesh" };
        m.SetVertices(v);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return SaveMeshAsset(m, "ConeMesh");
    }

    static GameObject ValidPrefab(GameObject custom, GameObject fallback, string label)
    {
        if (custom == null) return fallback;
#if UNITY_EDITOR
        if (!EditorUtility.IsPersistent(custom))
        {
            Debug.LogWarning(label + ": в поле лежит объект со сцены, а не префаб из Project.");
            return fallback;
        }
#endif
        return custom;
    }

    static GameObject SavePrefab(GameObject root, string name)
    {
#if UNITY_EDITOR
        EnsureFolder();
        string path = "Assets/GeneratedTerrain/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        DestroyImmediate(root);
        return null;
#endif
    }

    static void FixTerrainMaterial(Terrain t)
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (rp == null) return;
        string shaderName = rp.GetType().Name.Contains("HD") ? "HDRP/TerrainLit" : "Universal Render Pipeline/Terrain/Lit";
        Shader s = Shader.Find(shaderName);
        if (s == null) { Debug.LogWarning("Не нашёл шейдер террейна: " + shaderName); return; }
        if (t.materialTemplate == null || t.materialTemplate.shader != s)
            t.materialTemplate = Save(new Material(s), "TerrainMaterial");
    }

    static Shader LitShader()
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (rp != null)
        {
            Shader s = Shader.Find(rp.GetType().Name.Contains("HD") ? "HDRP/Lit" : "Universal Render Pipeline/Lit");
            if (s != null) return s;
        }
        return Shader.Find("Standard");
    }

    static Material MakeNatureMat(string name, Color c, bool leaves)
    {
        Shader sh = Shader.Find(leaves ? "Nature/Soft Occlusion Leaves" : "Nature/Soft Occlusion Bark");
        if (sh == null) return MakeMat(name, c);
        Material m = new Material(sh);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return Save(m, name);
    }

    static Material MakeMat(string name, Color c, bool transparent = false, float smooth = 0.1f)
    {
        Shader sh = LitShader();
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.SetColor("_Color", c);
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Smoothness", smooth);
        if (transparent)
        {
            m.SetFloat("_Mode", 3);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
        }
        m.enableInstancing = true;
        return Save(m, name);
    }

    TerrainLayer Layer(string name, Texture2D direct, Material custom, Texture2D generated, float tile)
    {
        Texture2D tex = generated, normal = null;
        float ts = tile;
        if (direct != null) { custom = null; tex = direct; ts = tileSize; }   // текстура напрямую - приоритет
        if (custom != null)
        {
            Vector2 scale;
            Texture2D t = MatTex(custom, new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_Diffuse" }, true, out scale);
            if (t != null)
            {
                tex = t;
                ts = tileSize / Mathf.Max(0.01f, scale.x);
                Vector2 ns;
                normal = MatTex(custom, new[] { "_BumpMap", "_NormalMap", "_NormalMapOS" }, false, out ns);
            }
            else Debug.LogWarning("В материале " + custom.name + " (шейдер " + (custom.shader != null ? custom.shader.name : "?") + ") нет ни одной подходящей текстуры, беру сгенерированную. Текстурные слоты материала: " + string.Join(", ", custom.GetTexturePropertyNames()) + ". Перетащи картинку в Base Map / Albedo материала.");
        }
        var layer = new TerrainLayer { diffuseTexture = tex, normalMapTexture = normal, tileSize = new Vector2(ts, ts), smoothness = 0f, metallic = 0f };
        return Save(layer, name);
    }

    static readonly string[] NotDiffuse =
    {
        "bump", "normal", "mask", "metal", "occlusion", "emission", "height", "parallax",
        "detail", "spec", "rough", "smooth", "ao", "cube", "lightmap"
    };

    // Ищет текстуру в материале: сначала по известным именам, потом (anyFallback) любую подходящую, кроме нормалей/масок
    static Texture2D MatTex(Material m, string[] props, bool anyFallback, out Vector2 scale)
    {
        scale = Vector2.one;
        foreach (string p in props)
        {
            if (!m.HasProperty(p)) continue;
            Texture2D t = m.GetTexture(p) as Texture2D;
            if (t == null) continue;
            scale = m.GetTextureScale(p);
            return t;
        }
        if (!anyFallback) return null;

        if (m.mainTexture is Texture2D mt)
        {
            scale = m.mainTextureScale;
            return mt;
        }
        foreach (string p in m.GetTexturePropertyNames())
        {
            string l = p.ToLowerInvariant();
            bool skip = false;
            foreach (string bad in NotDiffuse)
                if (l.Contains(bad)) { skip = true; break; }
            if (skip) continue;
            Texture2D t = m.GetTexture(p) as Texture2D;
            if (t == null) continue;
            scale = m.GetTextureScale(p);
            return t;
        }
        return null;
    }

    static float TN(float u, float v, float fu, float fv)
    {
        float a = Mathf.PerlinNoise(u * fu + 100, v * fv + 100);
        float b = Mathf.PerlinNoise((u - 1) * fu + 100, v * fv + 100);
        float c = Mathf.PerlinNoise(u * fu + 100, (v - 1) * fv + 100);
        float d = Mathf.PerlinNoise((u - 1) * fu + 100, (v - 1) * fv + 100);
        return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
    }

    Texture2D MakeTex(string name, Color a, Color b, float low, float high, float stretch, bool cracks, float contrast)
    {
        const int T = 256;
        Texture2D t = new Texture2D(T, T, TextureFormat.RGBA32, true);
        Color[] px = new Color[T * T];
        for (int y = 0; y < T; y++)
            for (int x = 0; x < T; x++)
            {
                float u = x / (float)T, v = y / (float)T;
                float n = 0.45f * TN(u, v, low, low) + 0.25f * TN(u, v, low * 3, low * 3) + 0.30f * TN(u, v, high, high * stretch);
                if (cracks)
                {
                    float r = 1f - Mathf.Abs(2f * TN(u, v, low * 2, low * 2) - 1f);
                    n -= SS(0.88f, 1f, r) * 0.35f;
                }
                n = Mathf.Clamp01((n - 0.5f) * contrast + 0.5f);
                px[y * T + x] = Color.Lerp(a, b, n);
            }
        t.SetPixels(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.Apply(true);
        return Save(t, name + "_tex");
    }

    Texture2D BladeTex(string name, bool flowers)
    {
        const int W = 128;
        Texture2D t = new Texture2D(W, W, TextureFormat.RGBA32, true);
        Color[] px = new Color[W * W];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 0f);

        var r = new System.Random(seed + (flowers ? 5 : 3));
        Color[] petals = { new Color(1f, 1f, 1f, 1f), new Color(1f, .9f, .2f, 1f), new Color(1f, .45f, .65f, 1f), new Color(.7f, .45f, 1f, 1f) };
        int count = flowers ? 9 : 36;
        for (int i = 0; i < count; i++)
        {
            float x0 = r.Next(12, W - 12);
            int len = flowers ? r.Next(65, 102) : r.Next(55, 122);
            float lean = (float)(r.NextDouble() - 0.5) * (flowers ? 22f : 52f);
            float bw = flowers ? 1.8f : r.Next(3, 6);
            float cx = x0;
            for (int y = 0; y < len; y++)
            {
                float k = y / (float)len;
                cx = x0 + lean * k * k;
                float w = bw * (1f - k) + 0.7f;
                for (int xx = Mathf.FloorToInt(cx - w); xx <= Mathf.CeilToInt(cx + w); xx++)
                {
                    if (xx < 0 || xx >= W) continue;
                    float g = 0.55f + 0.45f * k;
                    px[y * W + xx] = flowers ? new Color(.25f, .65f, .2f, 1f) : new Color(g, g, g, 1f);
                }
            }
            if (flowers)
            {
                Color pc = petals[r.Next(petals.Length)];
                int cy = len;
                for (int dy = -7; dy <= 7; dy++)
                    for (int dx = -7; dx <= 7; dx++)
                    {
                        int yy = cy + dy, xx = Mathf.RoundToInt(cx) + dx;
                        if (yy < 0 || yy >= W || xx < 0 || xx >= W) continue;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d <= 3f) px[yy * W + xx] = new Color(1f, .82f, .1f, 1f);
                        else if (d <= 7f) px[yy * W + xx] = pc;
                    }
            }
        }
        t.SetPixels(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply(true);
        return Save(t, name);
    }

#if UNITY_EDITOR
    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/GeneratedTerrain"))
            AssetDatabase.CreateFolder("Assets", "GeneratedTerrain");
    }
#endif

    static T Save<T>(T obj, string name) where T : Object
    {
#if UNITY_EDITOR
        if (obj == null) return null;
        EnsureFolder();
        obj.name = name;

        string path = "Assets/GeneratedTerrain/" + name + ".asset";
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            existing.name = name;
            EditorUtility.CopySerialized(obj, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            if (obj != existing)
                DestroyImmediate(obj);
            return existing;
        }

        AssetDatabase.CreateAsset(obj, path);
        return AssetDatabase.LoadAssetAtPath<T>(path);
#else
        return obj;
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SimpleTerrain))]
public class SimpleTerrainEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var t = (SimpleTerrain)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Generate", GUILayout.Height(32))) t.Generate();
        if (GUILayout.Button("Apply Object Sizes (без перегенерации)")) t.ApplyObjectSizes();
        if (GUILayout.Button("Random Seed + Generate"))
        {
            t.seed = Random.Range(0, 100000);
            t.Generate();
        }
    }
}
#endif