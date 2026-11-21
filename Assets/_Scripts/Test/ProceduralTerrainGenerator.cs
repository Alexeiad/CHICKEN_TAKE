using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ProceduralTerrainGenerator : MonoBehaviour
{
    [Serializable]
    public struct HeightMaterialBand
    {
        public string name;
        public Material material;
        [Range(0f, 1f)] public float minHeight;
        [Range(0f, 1f)] public float maxHeight;
    }

    [Serializable]
    public struct BiomeMaterialPatch
    {
        public string name;
        public Material material;
        [Range(0f, 1f)] public float voronoiThreshold;
        [Range(0f, 1f)] public float minHeight;
        [Range(0f, 1f)] public float maxHeight;
        [Min(4)] public int gridResolution;
    }

    [Serializable]
    public struct TreeBiomeGroup
    {
        public string name;
        public GameObject[] treePrefabs;
        [Range(0f, 1f)] public float voronoiThreshold;
        [Range(0f, 1f)] public float minHeight;
        [Range(0f, 1f)] public float maxHeight;
        [Range(0f, 90f)] public float maxSlope;
        [Min(0.5f)] public float spacing;
        public Vector2 scaleRange;
    }

    [Header("Target Terrain")]
    [SerializeField] private Terrain targetTerrain;

    [Header("Seed & Size")]
    public int seed = 42069;
    public int heightmapResolution = 129; // 65, 129, 257
    public Vector3 terrainSize = new Vector3(200f, 50f, 200f);
    public bool generateOnStart = false;

    [Header("Perlin Height Noise")]
    [Min(0.0001f)] public float noiseScale = 0.015f;
    [Range(1, 8)] public int octaves = 4;
    [Range(0.1f, 0.9f)] public float persistence = 0.5f;
    [Range(1.5f, 4f)] public float lacunarity = 2.0f;
    public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("River Carving")]
    public bool carveRiver = true;
    [Min(0.0001f)] public float riverNoiseScale = 0.008f;
    [Range(0.005f, 0.25f)] public float riverWidth = 0.045f;
    [Range(0f, 0.5f)] public float riverDepth = 0.14f;

    [Header("Height Level Materials (Mesh Overlays)")]
    [Min(16)] public int meshGridResolution = 64;
    public HeightMaterialBand[] heightMaterials;

    [Header("Voronoi Mini-Biome Materials & Trees")]
    [Min(2f)] public float voronoiCellSize = 35f;
    [Range(0f, 1f)] public float voronoiJitter = 0.85f;
    public BiomeMaterialPatch[] grassMaterialBiomes;
    public TreeBiomeGroup[] treeBiomes;

    [Header("Water Level")]
    public bool spawnWater = true;
    [Range(0f, 1f)] public float waterLevel = 0.25f;
    public Material waterMaterial;

    private Terrain _terrain;
    private TerrainData _terrainData;
    private const string GeneratedRootName = "__Generated_World_Parts__";
    private const string WaterObjectName = "__Generated_Water__";

    private void Awake()
    {
        EnsureTerrain();
    }

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    public void Generate()
    {
        EnsureTerrain();
        ClearGenerated();

        _terrainData.heightmapResolution = heightmapResolution;
        _terrainData.size = terrainSize;

        System.Random rng = new System.Random(seed);
        Vector2 heightOffset = new Vector2(
            (float)(rng.NextDouble() * 10000.0 - 5000.0),
            (float)(rng.NextDouble() * 10000.0 - 5000.0)
        );
        Vector2 riverOffset = new Vector2(
            (float)(rng.NextDouble() * 10000.0 - 5000.0),
            (float)(rng.NextDouble() * 10000.0 - 5000.0)
        );

        float[,] heights = BuildHeights(heightOffset, riverOffset);
        _terrainData.SetHeights(0, 0, heights);

        Transform root = GetOrCreateRoot();
        BuildHeightMaterialMeshes(root, heights);
        BuildVoronoiMaterialBiomes(root, heights, rng);
        BuildTrees(heights, rng);
        BuildWater(root);

#if UNITY_EDITOR
        EditorUtility.SetDirty(_terrainData);
        EditorUtility.SetDirty(_terrain);
        EditorUtility.SetDirty(this);
        Selection.activeGameObject = _terrain.gameObject;
#endif
    }

    public void ClearGenerated()
    {
        EnsureTerrain();
        if (_terrainData != null)
        {
            _terrainData.treeInstances = new TreeInstance[0];
            _terrainData.treePrototypes = new TreePrototype[0];
        }

        Transform parent = _terrain != null ? _terrain.transform : transform;
        Transform existingRoot = parent.Find(GeneratedRootName);
        if (existingRoot != null)
        {
            if (Application.isPlaying)
                Destroy(existingRoot.gameObject);
            else
                DestroyImmediate(existingRoot.gameObject);
        }
    }

    private void EnsureTerrain()
    {
        if (targetTerrain == null)
            targetTerrain = GetComponent<Terrain>();

        if (targetTerrain == null)
        {
            GameObject go = new GameObject("Generated_Terrain");
            go.transform.SetParent(transform, false);
            targetTerrain = go.AddComponent<Terrain>();
            go.AddComponent<TerrainCollider>();
        }

        _terrain = targetTerrain;
        _terrainData = _terrain.terrainData;

        if (_terrainData == null)
        {
            _terrainData = new TerrainData();
            _terrain.terrainData = _terrainData;
            TerrainCollider col = _terrain.GetComponent<TerrainCollider>();
            if (col == null)
                col = _terrain.gameObject.AddComponent<TerrainCollider>();
            col.terrainData = _terrainData;
        }
    }

    private Transform GetOrCreateRoot()
    {
        Transform parent = _terrain.transform;
        Transform existing = parent.Find(GeneratedRootName);
        if (existing != null)
            return existing;

        GameObject go = new GameObject(GeneratedRootName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        return go.transform;
    }

    private float[,] BuildHeights(Vector2 heightOffset, Vector2 riverOffset)
    {
        int res = heightmapResolution;
        float[,] heights = new float[res, res];

        for (int y = 0; y < res; y++)
        {
            float v = (float)y / (res - 1);
            float worldZ = v * terrainSize.z;

            for (int x = 0; x < res; x++)
            {
                float u = (float)x / (res - 1);
                float worldX = u * terrainSize.x;

                float amp = 1f;
                float freq = noiseScale;
                float sum = 0f;
                float maxAmp = 0f;

                for (int o = 0; o < octaves; o++)
                {
                    float sx = (worldX + heightOffset.x) * freq;
                    float sz = (worldZ + heightOffset.y) * freq;
                    sum += Mathf.PerlinNoise(sx, sz) * amp;
                    maxAmp += amp;

                    amp *= persistence;
                    freq *= lacunarity;
                }

                float h = maxAmp > 0f ? sum / maxAmp : 0f;
                h = Mathf.Clamp01(heightCurve.Evaluate(h));

                if (carveRiver && riverWidth > 0.0001f && riverDepth > 0.0001f)
                {
                    float rn = Mathf.PerlinNoise(
                        (worldX + riverOffset.x) * riverNoiseScale,
                        (worldZ + riverOffset.y) * riverNoiseScale
                    );
                    float dist = Mathf.Abs(rn - 0.5f);
                    if (dist < riverWidth)
                    {
                        float t = 1f - (dist / riverWidth);
                        float smooth = t * t * (3f - 2f * t);
                        h = Mathf.Clamp01(h - smooth * riverDepth);
                    }
                }

                heights[y, x] = h;
            }
        }

        return heights;
    }

    private void BuildHeightMaterialMeshes(Transform root, float[,] heights)
    {
        if (heightMaterials == null || heightMaterials.Length == 0)
            return;

        int res = Mathf.Max(8, meshGridResolution);
        float yOffset = 0.03f;

        for (int i = 0; i < heightMaterials.Length; i++)
        {
            HeightMaterialBand band = heightMaterials[i];
            if (band.material == null)
                continue;

            Mesh mesh = BuildFilteredHeightMesh(heights, res, band.minHeight, band.maxHeight, yOffset + i * 0.01f);
            if (mesh == null)
                continue;

            CreateMeshObject(root, $"HeightMat_{i}_{band.name}", mesh, band.material);
        }
    }

    private void BuildVoronoiMaterialBiomes(Transform root, float[,] heights, System.Random rng)
    {
        if (grassMaterialBiomes == null || grassMaterialBiomes.Length == 0)
            return;

        int baseSeed = rng.Next(1, 100000);
        float baseLift = 0.08f;

        for (int b = 0; b < grassMaterialBiomes.Length; b++)
        {
            BiomeMaterialPatch patch = grassMaterialBiomes[b];
            if (patch.material == null)
                continue;

            int res = Mathf.Max(8, patch.gridResolution > 0 ? patch.gridResolution : meshGridResolution);
            int biomeSeed = baseSeed + b * 7919;

            Mesh mesh = BuildVoronoiFilteredMesh(heights, res, patch, biomeSeed, baseLift + b * 0.015f);
            if (mesh == null)
                continue;

            CreateMeshObject(root, $"BiomeMat_{b}_{patch.name}", mesh, patch.material);
        }
    }

    private Mesh BuildFilteredHeightMesh(float[,] heights, int res, float minH, float maxH, float lift)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (int z = 0; z < res; z++)
        {
            float v = (float)z / (res - 1);
            for (int x = 0; x < res; x++)
            {
                float u = (float)x / (res - 1);
                float h = SampleHeightBilinear(heights, u, v);
                verts.Add(new Vector3(u * terrainSize.x, h * terrainSize.y + lift, v * terrainSize.z));
                uvs.Add(new Vector2(u * 10f, v * 10f));
            }
        }

        for (int z = 0; z < res - 1; z++)
        {
            for (int x = 0; x < res - 1; x++)
            {
                int i00 = z * res + x;
                int i10 = z * res + (x + 1);
                int i01 = (z + 1) * res + x;
                int i11 = (z + 1) * res + (x + 1);

                float uCenter = (x + 0.5f) / (res - 1);
                float vCenter = (z + 0.5f) / (res - 1);
                float hCenter = SampleHeightBilinear(heights, uCenter, vCenter);

                if (hCenter < minH || hCenter > maxH)
                    continue;

                tris.Add(i00);
                tris.Add(i01);
                tris.Add(i10);

                tris.Add(i10);
                tris.Add(i01);
                tris.Add(i11);
            }
        }

        if (tris.Count == 0)
            return null;

        Mesh mesh = new Mesh();
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh BuildVoronoiFilteredMesh(float[,] heights, int res, BiomeMaterialPatch patch, int biomeSeed, float lift)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (int z = 0; z < res; z++)
        {
            float v = (float)z / (res - 1);
            for (int x = 0; x < res; x++)
            {
                float u = (float)x / (res - 1);
                float h = SampleHeightBilinear(heights, u, v);
                verts.Add(new Vector3(u * terrainSize.x, h * terrainSize.y + lift, v * terrainSize.z));
                uvs.Add(new Vector2(u * 12f, v * 12f));
            }
        }

        for (int z = 0; z < res - 1; z++)
        {
            for (int x = 0; x < res - 1; x++)
            {
                int i00 = z * res + x;
                int i10 = z * res + (x + 1);
                int i01 = (z + 1) * res + x;
                int i11 = (z + 1) * res + (x + 1);

                float uCenter = (x + 0.5f) / (res - 1);
                float vCenter = (z + 0.5f) / (res - 1);
                float worldX = uCenter * terrainSize.x;
                float worldZ = vCenter * terrainSize.z;

                float hCenter = SampleHeightBilinear(heights, uCenter, vCenter);
                if (hCenter < patch.minHeight || hCenter > patch.maxHeight)
                    continue;

                float voronoi = SampleVoronoi(worldX, worldZ, voronoiCellSize, voronoiJitter, biomeSeed);
                if (voronoi < patch.voronoiThreshold)
                    continue;

                tris.Add(i00);
                tris.Add(i01);
                tris.Add(i10);

                tris.Add(i10);
                tris.Add(i01);
                tris.Add(i11);
            }
        }

        if (tris.Count == 0)
            return null;

        Mesh mesh = new Mesh();
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void CreateMeshObject(Transform parent, string objName, Mesh mesh, Material mat)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
    }

    private void BuildTrees(float[,] heights, System.Random rng)
    {
        if (treeBiomes == null || treeBiomes.Length == 0)
        {
            _terrainData.treeInstances = new TreeInstance[0];
            _terrainData.treePrototypes = new TreePrototype[0];
            return;
        }

        List<TreePrototype> protos = new List<TreePrototype>();
        Dictionary<int, int> prefabIdToIndex = new Dictionary<int, int>();

        for (int b = 0; b < treeBiomes.Length; b++)
        {
            TreeBiomeGroup group = treeBiomes[b];
            if (group.treePrefabs == null)
                continue;

            for (int p = 0; p < group.treePrefabs.Length; p++)
            {
                GameObject prefab = group.treePrefabs[p];
                if (prefab == null)
                    continue;

                int id = prefab.GetInstanceID();
                if (!prefabIdToIndex.ContainsKey(id))
                {
                    protos.Add(new TreePrototype { prefab = prefab });
                    prefabIdToIndex[id] = protos.Count - 1;
                }
            }
        }

        _terrainData.treePrototypes = protos.ToArray();
        if (protos.Count == 0)
        {
            _terrainData.treeInstances = new TreeInstance[0];
            return;
        }

        List<TreeInstance> instances = new List<TreeInstance>();
        int seedBase = rng.Next(1, 100000);

        for (int b = 0; b < treeBiomes.Length; b++)
        {
            TreeBiomeGroup group = treeBiomes[b];
            if (group.treePrefabs == null || group.treePrefabs.Length == 0)
                continue;

            float step = Mathf.Max(0.5f, group.spacing);
            int biomeSeed = seedBase + (b + 1) * 104729;

            for (float z = 0f; z < terrainSize.z; z += step)
            {
                for (float x = 0f; x < terrainSize.x; x += step)
                {
                    float wx = Mathf.Clamp(x + (float)(rng.NextDouble() - 0.5) * step * 0.8f, 0f, terrainSize.x);
                    float wz = Mathf.Clamp(z + (float)(rng.NextDouble() - 0.5) * step * 0.8f, 0f, terrainSize.z);

                    float u = wx / terrainSize.x;
                    float v = wz / terrainSize.z;
                    float h = SampleHeightBilinear(heights, u, v);

                    if (h < group.minHeight || h > group.maxHeight)
                        continue;

                    if (spawnWater && h < waterLevel)
                        continue;

                    if (_terrainData.GetSteepness(u, v) > group.maxSlope)
                        continue;

                    float voronoi = SampleVoronoi(wx, wz, voronoiCellSize, voronoiJitter, biomeSeed);
                    if (voronoi < group.voronoiThreshold)
                        continue;

                    GameObject prefab = group.treePrefabs[rng.Next(0, group.treePrefabs.Length)];
                    if (prefab == null)
                        continue;

                    float minScale = Mathf.Min(group.scaleRange.x, group.scaleRange.y);
                    float maxScale = Mathf.Max(group.scaleRange.x, group.scaleRange.y);
                    if (Mathf.Approximately(minScale, 0f) && Mathf.Approximately(maxScale, 0f))
                    {
                        minScale = 0.85f;
                        maxScale = 1.3f;
                    }

                    float scale = minScale + (float)rng.NextDouble() * (maxScale - minScale);

                    instances.Add(new TreeInstance
                    {
                        position = new Vector3(u, h, v),
                        prototypeIndex = prefabIdToIndex[prefab.GetInstanceID()],
                        widthScale = scale,
                        heightScale = scale,
                        rotation = (float)(rng.NextDouble() * Mathf.PI * 2.0),
                        color = Color.white,
                        lightmapColor = Color.white
                    });
                }
            }
        }

        _terrainData.treeInstances = instances.ToArray();
    }

    private void BuildWater(Transform root)
    {
        if (!spawnWater)
            return;

        GameObject waterGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        waterGo.name = WaterObjectName;
        waterGo.transform.SetParent(root, false);

        Collider col = waterGo.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }

        float waterY = waterLevel * terrainSize.y;
        waterGo.transform.localPosition = new Vector3(terrainSize.x * 0.5f, waterY, terrainSize.z * 0.5f);
        waterGo.transform.localScale = new Vector3(terrainSize.x / 10f, 1f, terrainSize.z / 10f);

        MeshRenderer mr = waterGo.GetComponent<MeshRenderer>();
        if (mr != null && waterMaterial != null)
        {
            mr.sharedMaterial = waterMaterial;
        }
    }

    private static float SampleVoronoi(float worldX, float worldZ, float cellSize, float jitter, int seed)
    {
        float gx = worldX / cellSize;
        float gz = worldZ / cellSize;
        int ix = Mathf.FloorToInt(gx);
        int iz = Mathf.FloorToInt(gz);

        float minDistSq = float.MaxValue;
        float cellValue = 0f;

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = ix + dx;
                int cz = iz + dz;

                float rx = Hash01(cx, cz, seed);
                float rz = Hash01(cx, cz, seed + 31337);
                float score = Hash01(cx, cz, seed + 99991);

                float px = cx + 0.5f + (rx - 0.5f) * jitter;
                float pz = cz + 0.5f + (rz - 0.5f) * jitter;

                float distX = gx - px;
                float distZ = gz - pz;
                float distSq = distX * distX + distZ * distZ;

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    cellValue = score;
                }
            }
        }

        float proximity = Mathf.Clamp01(1f - Mathf.Sqrt(minDistSq) * 0.7f);
        return Mathf.Clamp01(cellValue * 0.75f + proximity * 0.25f);
    }

    private static float Hash01(int x, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + z * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h = h ^ (h >> 16);
            return (h & 0x00FFFFFFu) / (float)0x01000000u;
        }
    }

    private static float SampleHeightBilinear(float[,] heights, float u, float v)
    {
        int res = heights.GetLength(0);
        float fx = Mathf.Clamp01(u) * (res - 1);
        float fy = Mathf.Clamp01(v) * (res - 1);

        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        int x1 = Mathf.Min(x0 + 1, res - 1);
        int y1 = Mathf.Min(y0 + 1, res - 1);

        float tx = fx - x0;
        float ty = fy - y0;

        float h00 = heights[y0, x0];
        float h10 = heights[y0, x1];
        float h01 = heights[y1, x0];
        float h11 = heights[y1, x1];

        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
    }
}