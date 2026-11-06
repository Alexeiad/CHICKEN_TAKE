using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
public class TrackGenerator : MonoBehaviour
{
    public enum TrackType
    {
        Straight, SCurve, Oval, Zigzag, Hairpin, Chicane, Stadium, Slalom,
        Hill, FigureEightBridge, LoopTheLoop
    }

    public enum TrackAxis { X, Z }

    [Header("Track")]
    [SerializeField] private TrackType trackType = TrackType.Straight;
    [SerializeField] private TrackAxis axis = TrackAxis.X;
    [SerializeField] private float trackWidth = 20f;
    [SerializeField] private float trackLength = 400f;
    [SerializeField] private bool centerTrack = false;

    [Header("Geometry")]
    [SerializeField] private float loopRadius = 120f;
    [SerializeField, Range(1, 128)] private int loopDetail = 24;

    [Header("Height")]
    [SerializeField] private float maxHeight = 8f;
    [SerializeField] private float heightFrequency = 2f;

    [Header("Road Mesh")]
    [SerializeField] private Mesh sourceMesh;
    [SerializeField, Min(0.01f)] private float segmentLength = 1f;
    [SerializeField] private bool flipTriangles = false;

    [Header("Slab")]
    [SerializeField] private bool generateSlab = true;
    [SerializeField, Min(0f)] private float slabThickness = 0.5f;
    [SerializeField] private Material slabMaterial;
    [SerializeField, Min(0.01f)] private float slabTileSize = 1f;

    [Header("Terrain (Mesh Snap)")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private bool snapToTerrain = true;
    [SerializeField, Range(0f, 1f)] private float snapStrength = 1f;
    [SerializeField] private float minHeightAboveTerrain = 0.2f;

    [Header("Embankments")]
    [SerializeField] private bool generateEmbankments = true;
    [SerializeField] private float embankmentMaxHeight = 4f;
    [SerializeField] private float embankmentSlopeAngle = 35f;
    [SerializeField] private Material embankmentMaterial;
    [SerializeField, Min(0.01f)] private float embankmentTileSize = 2f;

    [Header("Bridge / Pillars")]
    [SerializeField] private bool generatePillars = true;
    [SerializeField] private float pillarStartHeight = 3.5f;
    [SerializeField, Min(0.5f)] private float pillarSpacing = 12f;
    [SerializeField] private GameObject pillarPrefab;
    [SerializeField, Min(0.05f)] private float pillarDiameter = 0.8f;
    [SerializeField] private float pillarPrefabBaseHeight = 0f;
    [SerializeField, Min(0f)] private float pillarTopOffset = 0.6f;
    [SerializeField, Min(0f)] private float pillarColliderTopShrink = 0.3f;
    [SerializeField] private Material pillarMaterial;

    [Header("Debug")]
    [SerializeField] private bool showDebug;

    // Cached refs
    private SplineContainer _container;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _generatedMesh;

    private readonly List<Vector3> _points = new List<Vector3>(256);
    private readonly List<GameObject> _spawnedPillars = new List<GameObject>(64);
    private Transform _pillarsRoot;

    // Source mesh cache
    private Mesh _cachedSourceMesh;
    private Vector3[] _srcV;
    private Vector3[] _srcN;
    private Vector2[] _srcUV;
    private int[] _srcT;
    private int[] _leftEdge;
    private int[] _rightEdge;
    private float _srcXMin;
    private float _srcXMax;

    // Reusable scratch lists
    private readonly List<Vector3> _verts = new List<Vector3>(8192);
    private readonly List<Vector3> _norms = new List<Vector3>(8192);
    private readonly List<Vector2> _uvs = new List<Vector2>(8192);
    private readonly List<int> _tris = new List<int>(16384);
    private readonly List<int> _slabTris = new List<int>(8192);
    private readonly List<int> _embankTris = new List<int>(8192);
    private Material[] _materials = new Material[3];

    // Reusable scratch arrays
    private Vector3[] _wpArr;
    private Vector3[] _upArr;
    private Vector3[] _rtArr;
    private float[] _dArr;
    private float[] _heightAbove;
    private bool[] _tileHasEmb;
    private float[] _tileDBoundary;

    private bool _suppressSplineChanged;

#if UNITY_EDITOR
    private bool _scheduled;
    private bool _generating;
    private int _lastSplineHash;
    private int _lastMeshHash;
#endif

    // ═══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    private void OnEnable()
    {
        _container = GetComponent<SplineContainer>();
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        Spline.Changed += OnSplineChanged;
        EnsurePillarsRoot();

#if UNITY_EDITOR
        if (!Application.isPlaying) Schedule();
#endif
        Rebuild();
    }

    private void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
#if UNITY_EDITOR
        EditorApplication.delayCall -= GenerateDelayed;
        _scheduled = false;
#endif
    }

    private void OnValidate()
    {
        trackWidth = Mathf.Max(1f, trackWidth);
        trackLength = Mathf.Max(1f, trackLength);
        loopRadius = Mathf.Max(1f, loopRadius);
        loopDetail = Mathf.Clamp(loopDetail, 1, 128);
        maxHeight = Mathf.Max(0f, maxHeight);
        heightFrequency = Mathf.Max(0.1f, heightFrequency);

        segmentLength = Mathf.Max(0.01f, segmentLength);
        slabThickness = Mathf.Max(0f, slabThickness);
        slabTileSize = Mathf.Max(0.01f, slabTileSize);
        snapStrength = Mathf.Clamp01(snapStrength);
        embankmentTileSize = Mathf.Max(0.01f, embankmentTileSize);
        pillarSpacing = Mathf.Max(0.5f, pillarSpacing);
        pillarDiameter = Mathf.Max(0.05f, pillarDiameter);
        pillarTopOffset = Mathf.Max(0f, pillarTopOffset);
        pillarColliderTopShrink = Mathf.Max(0f, pillarColliderTopShrink);

#if UNITY_EDITOR
        if (!Application.isPlaying && isActiveAndEnabled) Schedule();
#endif
    }

    private void OnSplineChanged(Spline s, int knotIndex, SplineModification mod)
    {
        if (_suppressSplineChanged) return;
        if (_container != null && s == _container.Spline)
            Rebuild();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebug) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(loopRadius, trackWidth * 6f));
    }

#if UNITY_EDITOR
    private void Schedule()
    {
        if (_scheduled || _generating) return;
        _scheduled = true;
        EditorApplication.delayCall += GenerateDelayed;
    }

    private void GenerateDelayed()
    {
        EditorApplication.delayCall -= GenerateDelayed;
        _scheduled = false;
        if (this == null || !isActiveAndEnabled || Application.isPlaying) return;
        Generate();
    }
#endif

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════

    [ContextMenu("Generate Track")]
    public void Generate()
    {
#if UNITY_EDITOR
        if (_generating) return;
        _generating = true;
#endif
        try
        {
            if (_container == null) _container = GetComponent<SplineContainer>();
            if (_container == null) return;

            int splineHash = GetSplineHash();
            int meshHash = GetMeshHash();

            bool needSpline = false;
            bool needMesh = false;

#if UNITY_EDITOR
            if (splineHash != _lastSplineHash || _container.Spline.Count < 2) needSpline = true;
            if (meshHash != _lastMeshHash) needMesh = true;
#else
            needSpline = true;
            needMesh = true;
#endif

            if (!needSpline && !needMesh) return;

            if (needSpline)
            {
                BuildSpline();
#if UNITY_EDITOR
                _lastSplineHash = splineHash;
#endif
            }

            if (needSpline || needMesh)
            {
                Rebuild();
#if UNITY_EDITOR
                _lastMeshHash = meshHash;
#endif
            }
        }
        finally
        {
#if UNITY_EDITOR
            _generating = false;
#endif
        }
    }

    [ContextMenu("Rebuild Mesh")]
    public void Rebuild()
    {
        if (_container == null || _container.Spline.Count < 2) return;
        if (sourceMesh == null || segmentLength <= 0.01f) return;

        EnsureSourceMeshCache();
        EnsureMeshFilterRefs();

        ClearPillars();

        float length = _container.CalculateLength(0);
        if (length < 0.01f) return;

        BuildMesh(_container, length);
    }

    // ═══════════════════════════════════════════════════════════
    //  SPLINE GENERATION
    // ═══════════════════════════════════════════════════════════

    private void BuildSpline()
    {
        float length = Mathf.Max(trackLength, trackWidth * 6f);
        int count = loopDetail;

        _points.Clear();
        bool closed = false;

        switch (trackType)
        {
            case TrackType.Straight: BuildStraight(count, length); break;
            case TrackType.SCurve: BuildSCurve(count, length); break;
            case TrackType.Oval: BuildOval(count, length); closed = true; break;
            case TrackType.Zigzag: BuildZigzag(count, length); break;
            case TrackType.Hairpin: BuildHairpin(count, length); break;
            case TrackType.Chicane: BuildChicane(count, length); break;
            case TrackType.Stadium: BuildStadium(count, length); closed = true; break;
            case TrackType.Slalom: BuildSlalom(count, length); break;
            case TrackType.Hill: BuildHill(count, length); break;
            case TrackType.FigureEightBridge: BuildFigureEightBridge(count, length); closed = true; break;
            case TrackType.LoopTheLoop: BuildLoopTheLoop(count, length); break;
        }

        if (axis == TrackAxis.Z)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                Vector3 p = _points[i];
                _points[i] = new Vector3(p.z, p.y, p.x);
            }
        }

        if (!centerTrack && _points.Count > 0)
        {
            Vector3 offset = -_points[0];
            for (int i = 0; i < _points.Count; i++)
                _points[i] += offset;
        }

        if (_points.Count < 2) return;

#if UNITY_EDITOR
        Undo.RecordObject(_container, "Generate Track");
#endif
        _suppressSplineChanged = true;
        try
        {
            Spline spline = _container.Spline;
            spline.Clear();
            for (int i = 0; i < _points.Count; i++)
                spline.Add(new BezierKnot(_points[i]));
            spline.Closed = closed;
            spline.SetTangentMode(TangentMode.AutoSmooth);
        }
        finally
        {
            _suppressSplineChanged = false;
        }
#if UNITY_EDITOR
        EditorUtility.SetDirty(_container);
#endif
    }

    // ═══════════════════════════════════════════════════════════
    //  MESH GENERATION
    // ═══════════════════════════════════════════════════════════

    private void BuildMesh(SplineContainer container, float length)
    {
        bool hasN = _srcN != null && _srcN.Length == _srcV.Length;
        bool hasUV = _srcUV != null && _srcUV.Length == _srcV.Length;
        bool hasSlab = generateSlab && slabThickness > 0.001f && slabMaterial != null;

        float T = slabThickness;
        float tile = slabTileSize;
        int vc = _srcV.Length;
        float xMin = _srcXMin;
        float xMax = _srcXMax;

        int tiles = Mathf.Max(1, Mathf.RoundToInt(length / segmentLength));
        float step = length / tiles;

        int topCount = vc * tiles;
        EnsureVectorArray(ref _wpArr, topCount);
        EnsureVectorArray(ref _upArr, topCount);
        EnsureVectorArray(ref _rtArr, topCount);
        EnsureFloatArray(ref _dArr, topCount);
        EnsureFloatArray(ref _heightAbove, topCount);
        EnsureBoolArray(ref _tileHasEmb, tiles);
        EnsureFloatArray(ref _tileDBoundary, tiles + 1);

        for (int k = 0; k < tiles; k++)
            _tileDBoundary[k] = (k + 0.5f) * step;
        _tileDBoundary[tiles] = (tiles + 0.5f) * step;

        _verts.Clear();
        _norms.Clear();
        _uvs.Clear();
        _tris.Clear();
        _slabTris.Clear();
        _embankTris.Clear();

        Matrix4x4 w2l = transform.worldToLocalMatrix;
        bool doSnap = snapToTerrain && terrain != null;

        // ---- 1) Верх дороги ----
        for (int k = 0; k < tiles; k++)
        {
            int vertBase = k * vc;

            for (int i = 0; i < vc; i++)
            {
                float localT = _srcV[i].z / segmentLength;
                float d = (k + localT + 0.5f) * step;
                float t = Mathf.Clamp01(d / length);

                container.Evaluate(0, t, out var p, out var tan, out var upv);
                Vector3 fwd = ((Vector3)tan).normalized;
                Vector3 u = ((Vector3)upv).normalized;
                Vector3 rt = Vector3.Cross(u, fwd).normalized;

                Vector3 wp = (Vector3)p + rt * _srcV[i].x + u * _srcV[i].y;

                if (doSnap)
                {
                    float th = GetTerrainHeight(wp);
                    float desiredY = Mathf.Max(th + minHeightAboveTerrain, wp.y);
                    wp.y = Mathf.Lerp(wp.y, desiredY, snapStrength);
                }

                float hAbove = wp.y - GetTerrainHeight(wp);

                int idx = vertBase + i;
                _heightAbove[idx] = hAbove;
                _wpArr[idx] = wp;
                _upArr[idx] = u;
                _rtArr[idx] = rt;
                _dArr[idx] = d;

                _verts.Add(w2l.MultiplyPoint3x4(wp));
                if (hasN)
                {
                    Vector3 nrm = rt * _srcN[i].x + u * _srcN[i].y + fwd * _srcN[i].z;
                    _norms.Add(w2l.MultiplyVector(nrm).normalized);
                }
                else
                {
                    _norms.Add(w2l.MultiplyVector(u).normalized);
                }
                _uvs.Add(hasUV ? _srcUV[i] : Vector2.zero);
            }

            int srcTCount = _srcT.Length;
            for (int j = 0; j < srcTCount; j += 3)
            {
                int a = _srcT[j] + vertBase;
                int b = _srcT[j + 1] + vertBase;
                int c = _srcT[j + 2] + vertBase;
                if (flipTriangles) { _tris.Add(a); _tris.Add(c); _tris.Add(b); }
                else { _tris.Add(a); _tris.Add(b); _tris.Add(c); }
            }
        }

        // ---- 2) Плита ----
        if (hasSlab)
        {
            int bottomStart = _verts.Count;
            for (int k = 0; k < tiles; k++)
            {
                int vertBase = k * vc;
                for (int i = 0; i < vc; i++)
                {
                    int idx = vertBase + i;
                    _verts.Add(w2l.MultiplyPoint3x4(_wpArr[idx] - _upArr[idx] * T));
                    _norms.Add(w2l.MultiplyVector(-_upArr[idx]).normalized);
                    _uvs.Add(new Vector2((_srcV[i].x - xMin) / tile, _dArr[idx] / tile));
                }
            }
            int triCount = _tris.Count;
            for (int j = 0; j < triCount; j += 3)
            {
                _slabTris.Add(_tris[j] + bottomStart);
                _slabTris.Add(_tris[j + 2] + bottomStart);
                _slabTris.Add(_tris[j + 1] + bottomStart);
            }

            int leftCount = _leftEdge.Length;
            int rightCount = _rightEdge.Length;
            for (int side = 0; side < 2; side++)
            {
                int[] edge = side == 0 ? _leftEdge : _rightEdge;
                int edgeCount = side == 0 ? leftCount : rightCount;
                float sgn = side == 0 ? -1f : 1f;
                if (edgeCount < 2) continue;
                int edgeLast = edgeCount - 1;

                for (int k = 0; k < tiles; k++)
                {
                    int vertBase = k * vc;
                    int baseIdx = _verts.Count;

                    for (int e = 0; e < edgeCount; e++)
                    {
                        int idx = vertBase + edge[e];
                        Vector3 n = w2l.MultiplyVector(_rtArr[idx] * sgn).normalized;
                        _verts.Add(w2l.MultiplyPoint3x4(_wpArr[idx]));
                        _verts.Add(w2l.MultiplyPoint3x4(_wpArr[idx] - _upArr[idx] * T));
                        _norms.Add(n); _norms.Add(n);
                        float uu = _dArr[idx] / tile;
                        _uvs.Add(new Vector2(uu, 0f));
                        _uvs.Add(new Vector2(uu, T / tile));
                    }

                    for (int e = 0; e < edgeLast; e++)
                    {
                        int a = baseIdx + e * 2;
                        int b = a + 1;
                        int c = a + 2;
                        int d = a + 3;
                        if (side == 0)
                        {
                            _slabTris.Add(a); _slabTris.Add(b); _slabTris.Add(c);
                            _slabTris.Add(c); _slabTris.Add(b); _slabTris.Add(d);
                        }
                        else
                        {
                            _slabTris.Add(a); _slabTris.Add(c); _slabTris.Add(b);
                            _slabTris.Add(c); _slabTris.Add(d); _slabTris.Add(b);
                        }
                    }
                }
            }
        }

        // ---- 3) Флаг скосов ----
        bool wantEmbank = generateEmbankments && embankmentMaterial != null
                          && _leftEdge.Length >= 2 && _rightEdge.Length >= 2;
        if (wantEmbank)
        {
            for (int k = 0; k < tiles; k++)
            {
                int vertBase = k * vc;
                float avg = 0f;
                int cnt = 0;
                for (int e = 0; e < _leftEdge.Length; e++) { avg += _heightAbove[vertBase + _leftEdge[e]]; cnt++; }
                for (int e = 0; e < _rightEdge.Length; e++) { avg += _heightAbove[vertBase + _rightEdge[e]]; cnt++; }
                avg /= Mathf.Max(1, cnt);
                _tileHasEmb[k] = avg >= 0.15f && avg <= embankmentMaxHeight;
            }
        }
        else
        {
            System.Array.Clear(_tileHasEmb, 0, tiles);
        }

        // ---- 4) Скосы ----
        if (wantEmbank)
        {
            float slopeTan = Mathf.Tan(Mathf.Max(1f, embankmentSlopeAngle) * Mathf.Deg2Rad);
            float embTile = embankmentTileSize;

            for (int side = 0; side < 2; side++)
            {
                int[] edge = side == 0 ? _leftEdge : _rightEdge;
                int edgeCount = edge.Length;
                float sgn = side == 0 ? -1f : 1f;
                if (edgeCount < 2) continue;
                int edgeLast = edgeCount - 1;

                for (int k = 0; k < tiles; k++)
                {
                    if (!_tileHasEmb[k]) continue;
                    int vertBase = k * vc;
                    int baseIdx = _verts.Count;

                    for (int e = 0; e < edgeCount; e++)
                    {
                        int idx = vertBase + edge[e];
                        Vector3 top = _wpArr[idx];
                        float h = _heightAbove[idx];
                        float run = h / slopeTan;

                        Vector3 bottom = top - _upArr[idx] * h + _rtArr[idx] * (sgn * run);
                        bottom.y = GetTerrainHeight(bottom);

                        _verts.Add(w2l.MultiplyPoint3x4(top));
                        _verts.Add(w2l.MultiplyPoint3x4(bottom));

                        Vector3 n = (_rtArr[idx] * sgn).normalized;
                        Vector3 wn = w2l.MultiplyVector(n);
                        _norms.Add(wn); _norms.Add(wn);

                        float uu = _dArr[idx] / embTile;
                        _uvs.Add(new Vector2(uu, 0f));
                        _uvs.Add(new Vector2(uu, h / embTile));
                    }

                    for (int e = 0; e < edgeLast; e++)
                    {
                        int a = baseIdx + e * 2;
                        int b = a + 1;
                        int c = a + 2;
                        int d = a + 3;
                        if (side == 0)
                        {
                            _embankTris.Add(a); _embankTris.Add(b); _embankTris.Add(c);
                            _embankTris.Add(c); _embankTris.Add(b); _embankTris.Add(d);
                        }
                        else
                        {
                            _embankTris.Add(a); _embankTris.Add(c); _embankTris.Add(b);
                            _embankTris.Add(c); _embankTris.Add(d); _embankTris.Add(b);
                        }
                    }
                }
            }

            AddEmbankmentEndCaps(tiles, length, w2l);
        }

        // ---- 5) Столбы ----
        if (generatePillars && pillarStartHeight > 0f && pillarSpacing > 0.01f)
        {
            SpawnPillars(container, length);
        }

        // ---- 6) Сборка меша ----
        FinalizeMesh(hasSlab);
    }

    private void SpawnPillars(SplineContainer container, float length)
    {
        float spacing = pillarSpacing;
        float topOffset = Mathf.Max(pillarTopOffset, slabThickness + 0.05f);

        for (float dist = spacing * 0.5f; dist < length; dist += spacing)
        {
            float t = dist / length;
            container.Evaluate(0, t, out var p, out _, out _);
            Vector3 pos = p;
            float h = pos.y - GetTerrainHeight(pos);

            if (h < pillarStartHeight) continue;

            float pillarH = h - topOffset;
            if (pillarH <= 0.05f) continue;

            Vector3 top = new Vector3(pos.x, pos.y - topOffset, pos.z);
            SpawnPillar(top, pillarH);
        }
    }

    private void FinalizeMesh(bool hasSlab)
    {
        if (_generatedMesh == null)
            _generatedMesh = new Mesh { name = "TrackMesh" };

        _generatedMesh.Clear();
        int vcount = _verts.Count;
        _generatedMesh.indexFormat = vcount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        _generatedMesh.SetVertices(_verts);
        _generatedMesh.SetNormals(_norms);
        _generatedMesh.SetUVs(0, _uvs);

        int subCount = 1;
        if (hasSlab) subCount++;
        if (_embankTris.Count > 0) subCount++;
        _generatedMesh.subMeshCount = subCount;

        _generatedMesh.SetTriangles(_tris, 0);
        int sub = 1;
        if (hasSlab)
        {
            _generatedMesh.SetTriangles(_slabTris, sub);
            sub++;
        }
        if (_embankTris.Count > 0)
            _generatedMesh.SetTriangles(_embankTris, sub);

        _generatedMesh.RecalculateBounds();

        if (_meshFilter != null)
            _meshFilter.sharedMesh = _generatedMesh;

        if (_materials == null || _materials.Length != subCount)
            _materials = new Material[subCount];

        Material top = _meshRenderer != null && _meshRenderer.sharedMaterials.Length > 0
            ? _meshRenderer.sharedMaterials[0]
            : null;
        _materials[0] = top;
        int mi = 1;
        if (hasSlab) _materials[mi++] = slabMaterial;
        if (_embankTris.Count > 0) _materials[mi] = embankmentMaterial;
        if (_meshRenderer != null) _meshRenderer.sharedMaterials = _materials;

        var mc = GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = _generatedMesh;
    }

    private void AddEmbankmentEndCaps(int tiles, float length, Matrix4x4 w2l)
    {
        float slopeTan = Mathf.Tan(Mathf.Max(1f, embankmentSlopeAngle) * Mathf.Deg2Rad);
        float T = slabThickness;
        float tileSize = embankmentTileSize;
        float xMin = _srcXMin;
        float xMax = _srcXMax;

        for (int k = 0; k <= tiles; k++)
        {
            bool prevHasEmb = k > 0 && _tileHasEmb[k - 1];
            bool currHasEmb = k < tiles && _tileHasEmb[k];
            if (prevHasEmb == currHasEmb) continue;

            float d = _tileDBoundary[k];
            float t = Mathf.Clamp01(d / length);

            _container.Evaluate(0, t, out var p, out var tan, out var upv);
            Vector3 fwd = ((Vector3)tan).normalized;
            Vector3 u = ((Vector3)upv).normalized;
            Vector3 rt = Vector3.Cross(u, fwd).normalized;

            Vector3 lTop = (Vector3)p + rt * xMin;
            Vector3 rTop = (Vector3)p + rt * xMax;

            float lH = Mathf.Max(0f, lTop.y - GetTerrainHeight(lTop));
            float rH = Mathf.Max(0f, rTop.y - GetTerrainHeight(rTop));
            if (lH < 0.15f && rH < 0.15f) continue;

            float lRun = lH / slopeTan;
            float rRun = rH / slopeTan;

            Vector3 lBot = lTop - Vector3.up * lH - rt * lRun;
            lBot.y = GetTerrainHeight(lBot);

            Vector3 rBot = rTop - Vector3.up * rH + rt * rRun;
            rBot.y = GetTerrainHeight(rBot);

            Vector3 lSlabBot = lTop - u * T;
            Vector3 rSlabBot = rTop - u * T;

            int baseIdx = _verts.Count;

            Vector3[] capV = { lTop, lSlabBot, lBot, rBot, rSlabBot, rTop };
            Vector3[] capN = {
                (-rt + u).normalized,
                (-rt - u).normalized,
                (-rt + Vector3.down).normalized,
                ( rt + Vector3.down).normalized,
                ( rt - u).normalized,
                ( rt + u).normalized
            };

            for (int i = 0; i < 6; i++)
            {
                _verts.Add(w2l.MultiplyPoint3x4(capV[i]));
                _norms.Add(w2l.MultiplyVector(capN[i]).normalized);
            }

            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(0f, T / tileSize));
            _uvs.Add(new Vector2(0f, lH / tileSize));
            _uvs.Add(new Vector2(1f, rH / tileSize));
            _uvs.Add(new Vector2(1f, T / tileSize));
            _uvs.Add(new Vector2(1f, 0f));

            int L = baseIdx, LS = baseIdx + 1, LB = baseIdx + 2;
            int RB = baseIdx + 3, RS = baseIdx + 4, R = baseIdx + 5;

            _embankTris.Add(L); _embankTris.Add(LS); _embankTris.Add(LB);
            _embankTris.Add(LS); _embankTris.Add(RB); _embankTris.Add(LB);
            _embankTris.Add(LS); _embankTris.Add(RS); _embankTris.Add(RB);
            _embankTris.Add(RS); _embankTris.Add(R); _embankTris.Add(RB);

            _embankTris.Add(LB); _embankTris.Add(LS); _embankTris.Add(L);
            _embankTris.Add(LB); _embankTris.Add(RB); _embankTris.Add(LS);
            _embankTris.Add(RB); _embankTris.Add(RS); _embankTris.Add(LS);
            _embankTris.Add(RB); _embankTris.Add(R); _embankTris.Add(RS);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PILLARS
    // ═══════════════════════════════════════════════════════════

    private void SpawnPillar(Vector3 topWorld, float height)
    {
        EnsurePillarsRoot();
        Vector3 center = new Vector3(topWorld.x, topWorld.y - height * 0.5f, topWorld.z);

        GameObject go;
        if (pillarPrefab != null)
        {
            go = Instantiate(pillarPrefab, _pillarsRoot);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            if (pillarPrefabBaseHeight > 0.01f)
            {
                var s = go.transform.localScale;
                go.transform.localScale = new Vector3(s.x, height / pillarPrefabBaseHeight, s.z);
            }
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Pillar";
            go.transform.SetParent(_pillarsRoot, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(pillarDiameter, height * 0.5f, pillarDiameter);

            var r = go.GetComponent<Renderer>();
            if (pillarMaterial != null) r.sharedMaterial = pillarMaterial;
        }

        LowerPillarColliders(go, pillarColliderTopShrink);
        _spawnedPillars.Add(go);
    }

    private void LowerPillarColliders(GameObject pillar, float shrinkWorld)
    {
        if (pillar == null || shrinkWorld <= 0.0001f) return;
        var colliders = pillar.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null) continue;
            float sy = c.transform.lossyScale.y;
            if (sy < 0.0001f) sy = 0.0001f;
            float localShrink = shrinkWorld / sy;

            if (c is CapsuleCollider cap)
            {
                cap.height = Mathf.Max(0.01f, cap.height - localShrink);
                var ctr = cap.center; ctr.y -= localShrink * 0.5f; cap.center = ctr;
            }
            else if (c is BoxCollider box)
            {
                var sz = box.size; sz.y = Mathf.Max(0.01f, sz.y - localShrink); box.size = sz;
                var ctr = box.center; ctr.y -= localShrink * 0.5f; box.center = ctr;
            }
            else
            {
                c.transform.position += Vector3.down * (shrinkWorld * 0.5f);
            }
        }
    }

    private void ClearPillars()
    {
        for (int i = 0; i < _spawnedPillars.Count; i++)
        {
            var go = _spawnedPillars[i];
            if (go == null) continue;
            go.transform.SetParent(null, false);
            SafeDestroy(go);
        }
        _spawnedPillars.Clear();

        if (_pillarsRoot == null)
        {
            var existing = transform.Find("Pillars");
            if (existing != null) _pillarsRoot = existing;
        }
        if (_pillarsRoot != null)
        {
            for (int i = _pillarsRoot.childCount - 1; i >= 0; i--)
            {
                var child = _pillarsRoot.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                SafeDestroy(child);
            }
        }
    }

    private void EnsurePillarsRoot()
    {
        if (_pillarsRoot != null) return;
        var t = transform.Find("Pillars");
        if (t == null)
        {
            var go = new GameObject("Pillars");
            go.transform.SetParent(transform, false);
            _pillarsRoot = go.transform;
        }
        else
        {
            _pillarsRoot = t;
        }
    }

    private static void SafeDestroy(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) Object.DestroyImmediate(go);
        else Object.Destroy(go);
#else
        Object.Destroy(go);
#endif
    }

    // ═══════════════════════════════════════════════════════════
    //  CACHE / HELPERS
    // ═══════════════════════════════════════════════════════════

    private void EnsureMeshFilterRefs()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
        if (_container == null) _container = GetComponent<SplineContainer>();
    }

    private void EnsureSourceMeshCache()
    {
        if (_cachedSourceMesh == sourceMesh && _srcV != null) return;

        _cachedSourceMesh = sourceMesh;

        if (sourceMesh == null)
        {
            _srcV = _srcN = null; _srcUV = null; _srcT = null;
            _leftEdge = _rightEdge = null;
            return;
        }

        _srcV = sourceMesh.vertices;
        _srcN = sourceMesh.normals;
        _srcUV = sourceMesh.uv;
        _srcT = sourceMesh.GetTriangles(0);

        Bounds b = sourceMesh.bounds;
        _srcXMin = b.min.x;
        _srcXMax = b.max.x;

        var left = new List<int>(_srcV.Length / 4);
        var right = new List<int>(_srcV.Length / 4);
        for (int i = 0; i < _srcV.Length; i++)
        {
            if (Mathf.Abs(_srcV[i].x - _srcXMin) < 0.001f) left.Add(i);
            if (Mathf.Abs(_srcV[i].x - _srcXMax) < 0.001f) right.Add(i);
        }
        left.Sort((a, b2) => _srcV[a].z.CompareTo(_srcV[b2].z));
        right.Sort((a, b2) => _srcV[a].z.CompareTo(_srcV[b2].z));
        _leftEdge = left.ToArray();
        _rightEdge = right.ToArray();
    }

    private float GetTerrainHeight(Vector3 worldPos)
    {
        if (terrain == null) return worldPos.y;
        return terrain.SampleHeight(worldPos) + terrain.transform.position.y;
    }

    private static void EnsureVectorArray(ref Vector3[] arr, int size)
    {
        if (arr == null || arr.Length != size) arr = new Vector3[size];
    }
    private static void EnsureFloatArray(ref float[] arr, int size)
    {
        if (arr == null || arr.Length != size) arr = new float[size];
    }
    private static void EnsureBoolArray(ref bool[] arr, int size)
    {
        if (arr == null || arr.Length != size) arr = new bool[size];
    }

    // ═══════════════════════════════════════════════════════════
    //  HASHES
    // ═══════════════════════════════════════════════════════════

    private int GetSplineHash()
    {
        unchecked
        {
            int h = (int)trackType;
            h = h * 31 + (int)axis;
            h = h * 31 + (centerTrack ? 1 : 0);
            h = h * 31 + trackWidth.GetHashCode();
            h = h * 31 + trackLength.GetHashCode();
            h = h * 31 + loopRadius.GetHashCode();
            h = h * 31 + loopDetail;
            h = h * 31 + maxHeight.GetHashCode();
            h = h * 31 + heightFrequency.GetHashCode();
            return h;
        }
    }

    private int GetMeshHash()
    {
        unchecked
        {
            int h = sourceMesh != null ? sourceMesh.GetInstanceID() : 0;
            h = h * 31 + segmentLength.GetHashCode();
            h = h * 31 + (flipTriangles ? 1 : 0);
            h = h * 31 + (generateSlab ? 1 : 0);
            h = h * 31 + slabThickness.GetHashCode();
            h = h * 31 + (slabMaterial != null ? slabMaterial.GetInstanceID() : 0);
            h = h * 31 + slabTileSize.GetHashCode();
            h = h * 31 + (terrain != null ? terrain.GetInstanceID() : 0);
            h = h * 31 + (snapToTerrain ? 1 : 0);
            h = h * 31 + snapStrength.GetHashCode();
            h = h * 31 + minHeightAboveTerrain.GetHashCode();
            h = h * 31 + (generateEmbankments ? 1 : 0);
            h = h * 31 + embankmentMaxHeight.GetHashCode();
            h = h * 31 + embankmentSlopeAngle.GetHashCode();
            h = h * 31 + (embankmentMaterial != null ? embankmentMaterial.GetInstanceID() : 0);
            h = h * 31 + embankmentTileSize.GetHashCode();
            h = h * 31 + (generatePillars ? 1 : 0);
            h = h * 31 + pillarStartHeight.GetHashCode();
            h = h * 31 + pillarSpacing.GetHashCode();
            h = h * 31 + (pillarPrefab != null ? pillarPrefab.GetInstanceID() : 0);
            h = h * 31 + pillarDiameter.GetHashCode();
            h = h * 31 + pillarPrefabBaseHeight.GetHashCode();
            h = h * 31 + pillarTopOffset.GetHashCode();
            h = h * 31 + pillarColliderTopShrink.GetHashCode();
            h = h * 31 + (pillarMaterial != null ? pillarMaterial.GetInstanceID() : 0);
            return h;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  SPLINE BUILDERS
    // ═══════════════════════════════════════════════════════════

    private float BaseHeight(float t)
    {
        if (maxHeight <= 0f) return 0f;
        return maxHeight * Mathf.Sin(t * Mathf.PI * 2f * heightFrequency);
    }

    private void BuildStraight(int count, float length)
    {
        if (count < 2) return;
        float denom = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = i / denom;
            _points.Add(new Vector3((t - 0.5f) * length, BaseHeight(t), 0f));
        }
    }

    private void BuildSCurve(int count, float length)
    {
        if (count < 2) return;
        float amp = Mathf.Clamp(loopRadius * 0.5f, trackWidth, length * 0.35f);
        float denom = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = i / denom;
            _points.Add(new Vector3(
                (t - 0.5f) * length,
                BaseHeight(t),
                Mathf.Sin(t * Mathf.PI * 2f) * amp));
        }
    }

    private void BuildOval(int count, float length)
    {
        if (count < 2) return;
        float rx = length * 0.5f;
        float rz = Mathf.Clamp(loopRadius, trackWidth, rx * 0.85f);
        float da = Mathf.PI * 2f / count;
        for (int i = 0; i < count; i++)
        {
            float a = i * da;
            _points.Add(new Vector3(rx * Mathf.Cos(a), 0f, rz * Mathf.Sin(a)));
        }
    }

    private void BuildZigzag(int count, float length)
    {
        if (count < 2) return;
        const int segments = 4;
        float amp = Mathf.Clamp(loopRadius * 0.4f, trackWidth, length * 0.25f);
        float denom = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = i / denom;
            float z = (Mathf.PingPong(t * segments + 0.5f, 1f) * 2f - 1f) * amp;
            _points.Add(new Vector3((t - 0.5f) * length, BaseHeight(t), z));
        }
    }

    private void BuildHairpin(int count, float length)
    {
        if (count < 2) return;
        float R = Mathf.Clamp(loopRadius * 0.5f, trackWidth, length * 0.4f);
        float halfL = length * 0.5f;
        float denom = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = i / denom;
            float x, z;
            if (t < 0.45f) { float k = t / 0.45f; x = Mathf.Lerp(-halfL, halfL, k); z = -R; }
            else if (t < 0.55f) { float k = (t - 0.45f) * 10f; float a = -Mathf.PI * 0.5f + k * Mathf.PI; x = halfL + R * Mathf.Cos(a); z = R * Mathf.Sin(a); }
            else { float k = (t - 0.55f) / 0.45f; x = Mathf.Lerp(halfL, -halfL, k); z = R; }
            _points.Add(new Vector3(x, BaseHeight(t), z));
        }
    }

    private void BuildChicane(int count, float length)
    {
        if (count < 2) return;
        float amp = Mathf.Clamp(loopRadius * 0.6f, trackWidth, length * 0.2f);
        float denom = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = i / denom;
            float x = (t - 0.5f) * length;
            float z = 0f;
            if (t > 0.25f && t < 0.5f) z = amp * Mathf.Sin((t - 0.25f) * 4f * Mathf.PI);
            else if (t >= 0.5f && t < 0.75f) z = -amp * Mathf.Sin((t - 0.5f) * 4f * Mathf.PI);
            _points.Add(new Vector3(x, BaseHeight(t), z));
        }
    }

    private void BuildStadium(int count, float length)
    {
        if (count < 4) return;
        float L = Mathf.Max(length * 0.5f, trackWidth * 2f);
        float R = Mathf.Clamp(loopRadius * 0.5f, trackWidth, L * 0.4f);
        float perimeter = 2f * L + 2f * Mathf.PI * R;
        float s1 = L / perimeter;
        float s2 = (L + Mathf.PI * R) / perimeter;
        float s3 = (2f * L + Mathf.PI * R) / perimeter;
        float halfL = L * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float x, z;
            if (t < s1) { float k = t / s1; x = Mathf.Lerp(-halfL, halfL, k); z = -R; }
            else if (t < s2) { float k = (t - s1) / (s2 - s1); float a = -Mathf.PI * 0.5f + k * Mathf.PI; x = halfL + R * Mathf.Cos(a); z = R * Mathf.Sin(a); }
            else if (t < s3) { float k = (t - s2) / (s3 - s2); x = Mathf.Lerp(halfL, -halfL, k); z = R; }
            else { float k = (t - s3) / (1f - s3); float a = Mathf.PI * 0.5f + k * Mathf.PI; x = -halfL + R * Mathf.Cos(a); z = R * Mathf.Sin(a); }
            _points.Add(new Vector3(x, 0f, z));
        }
    }

    private void BuildSlalom(int count, float length)
    {
        if (count < 2) return;
        float amp = Mathf.Clamp(loopRadius * 0.35f, trackWidth, length * 0.15f);
        float denom = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = i / denom;
            float z = Mathf.Sin(t * Mathf.PI * 4f) * amp;
            _points.Add(new Vector3((t - 0.5f) * length, BaseHeight(t), z));
        }
    }

    private void BuildHill(int count, float length)
    {
        if (count < 2) return;
        float denom = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = i / denom;
            float y = maxHeight * Mathf.Sin(t * Mathf.PI);
            _points.Add(new Vector3((t - 0.5f) * length, y, 0f));
        }
    }

    private void BuildFigureEightBridge(int count, float length)
    {
        if (count < 4) return;
        float R = Mathf.Clamp(loopRadius, trackWidth * 2f, length * 0.5f);
        float bridgeH = Mathf.Max(maxHeight, trackWidth * 1.5f);
        float da = Mathf.PI * 2f / count;
        for (int i = 0; i < count; i++)
        {
            float a = i * da;
            float s = Mathf.Sin(a);
            _points.Add(new Vector3(
                R * s,
                bridgeH * (1f - Mathf.Cos(a)) * 0.5f,
                R * s * Mathf.Cos(a)));
        }
    }

    private void BuildLoopTheLoop(int count, float length)
    {
        if (count < 8) return;
        float R = Mathf.Clamp(maxHeight, trackWidth, length * 0.25f);
        float halfL = length * 0.5f;
        float denom = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = i / denom;
            float x, y;
            if (t < 0.2f)
            {
                float k = t * 5f;
                x = Mathf.Lerp(-halfL, -R, k);
                y = 0f;
            }
            else if (t < 0.8f)
            {
                float k = (t - 0.2f) / 0.6f;
                float theta = k * Mathf.PI * 2f;
                x = -R + R * Mathf.Sin(theta) + 2f * R * k;
                y = R * (1f - Mathf.Cos(theta));
            }
            else
            {
                float k = (t - 0.8f) * 5f;
                x = Mathf.Lerp(R, halfL, k);
                y = 0f;
            }
            _points.Add(new Vector3(x, y, 0f));
        }
    }
}