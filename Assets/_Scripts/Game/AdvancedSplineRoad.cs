using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AdvancedSplineRoad : MonoBehaviour
{
    [Header("Spline & Mesh")]
    public SplineContainer container;
    public Mesh sourceMesh;
    [Tooltip("Длина исходного куска вдоль Z")]
    public float segmentLength = 1f;
    public bool flipTriangles = false;

    [Header("Adaptive Tessellation")]
    [Tooltip("Добавлять больше подразбиений там, где террейн круто меняет высоту.")]
    public bool adaptiveTerrainDetail = true;
    [Tooltip("Разница высот террейна на один базовый тайл (м), выше которой происходит подразбиение.")]
    [Min(0.05f)] public float adaptiveHeightThreshold = 0.4f;
    [Tooltip("Максимальное подразбиение одного базового тайла.")]
    [Range(1, 16)] public int adaptiveMaxSubdivision = 8;

    [Header("Плита дороги")]
    public bool generateSlab = true;
    [Min(0f)] public float slabThickness = 0.5f;
    public Material slabMaterial;
    public float slabTileSize = 1f;

    [Header("Terrain")]
    public Terrain terrain;
    public bool snapToTerrain = true;
    [Range(0f, 1f)] public float snapStrength = 1f;
    public float minHeightAboveTerrain = 0.2f;

    [Header("Hills (опережающий подъём)")]
    public float hillClearance = 0.6f;
    public float hillDetectionDistance = 0f;
    [Min(0f)] public float hillLookAheadDistance = 40f;
    [Min(0f)] public float hillSmoothDistance = 12f;

    [Header("Embankments (скосы)")]
    public bool generateEmbankments = true;
    public float embankmentMaxHeight = 4f;
    public float embankmentSlopeAngle = 35f;
    public Material embankmentMaterial;
    public float embankmentTileSize = 2f;

    [Header("Bridge / Pillars")]
    public bool generatePillars = true;
    public float pillarStartHeight = 3.5f;
    [Min(0.5f)] public float pillarSpacing = 12f;
    public GameObject pillarPrefab;
    [Min(0.05f)] public float pillarDiameter = 0.8f;
    public float pillarPrefabBaseHeight = 0f;
    [Min(0f)] public float pillarTopOffset = 0.6f;
    [Min(0f)] public float pillarColliderTopShrink = 0.3f;
    public Material pillarMaterial;

    Mesh generated;
    List<GameObject> spawnedPillars = new List<GameObject>();
    Transform pillarsRoot;

    void OnEnable()
    {
        Spline.Changed += OnSplineChanged;
        EnsurePillarsRoot();
        Rebuild();
    }

    void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
    }

    void OnValidate()
    {
        Rebuild();
    }

    void OnSplineChanged(Spline s, int knotIndex, SplineModification mod)
    {
        if (container != null && s == container.Spline)
            Rebuild();
    }

    void EnsurePillarsRoot()
    {
        if (pillarsRoot == null)
        {
            var t = transform.Find("Pillars");
            if (t == null)
            {
                var go = new GameObject("Pillars");
                go.transform.SetParent(transform, false);
                pillarsRoot = go.transform;
            }
            else pillarsRoot = t;
        }
    }

    static void AddTri(List<int> list, int a, int b, int c)
    {
        list.Add(a); list.Add(b); list.Add(c);
    }

    float GetTerrainHeight(Vector3 worldPos)
    {
        if (terrain == null) return worldPos.y;
        return terrain.SampleHeight(worldPos) + terrain.transform.position.y;
    }

    static int WrapTile(int k, int tiles, bool wrap)
    {
        if (!wrap) return Mathf.Clamp(k, 0, tiles - 1);
        int m = k % tiles;
        if (m < 0) m += tiles;
        return m;
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (container == null || sourceMesh == null || segmentLength <= 0f)
            return;

        float length = container.CalculateLength(0);
        if (length < 0.01f) return;

        ClearPillars();

        Vector3[] srcV = sourceMesh.vertices;
        Vector3[] srcN = sourceMesh.normals;
        Vector2[] srcUV = sourceMesh.uv;
        int[] srcT = sourceMesh.GetTriangles(0);

        if (srcV.Length < 3) return;

        bool hasN = srcN != null && srcN.Length == srcV.Length;
        bool hasUV = srcUV != null && srcUV.Length == srcV.Length;
        bool hasSlab = generateSlab && slabThickness > 0.001f && slabMaterial != null;

        float T = slabThickness;
        float tile = Mathf.Max(0.01f, slabTileSize);

        float xMin = sourceMesh.bounds.min.x;
        float xMax = sourceMesh.bounds.max.x;
        float roadWidth = Mathf.Max(0.01f, xMax - xMin);

        var leftEdge = new List<int>();
        var rightEdge = new List<int>();
        for (int i = 0; i < srcV.Length; i++)
        {
            if (Mathf.Abs(srcV[i].x - xMin) < 0.001f) leftEdge.Add(i);
            if (Mathf.Abs(srcV[i].x - xMax) < 0.001f) rightEdge.Add(i);
        }
        leftEdge.Sort((a, b) => srcV[a].z.CompareTo(srcV[b].z));
        rightEdge.Sort((a, b) => srcV[a].z.CompareTo(srcV[b].z));

        // ---- 1) Базовые тайлы + адаптивное подразбиение ----
        int baseTiles = Mathf.Max(1, Mathf.RoundToInt(length / segmentLength));
        float step = length / baseTiles;
        int vc = srcV.Length;
        bool isClosed = container.Spline.Closed;

        int[] subdiv = new int[baseTiles];
        for (int k = 0; k < baseTiles; k++) subdiv[k] = 1;

        if (adaptiveTerrainDetail && terrain != null && adaptiveHeightThreshold > 0f)
        {
            for (int k = 0; k < baseTiles; k++)
            {
                float dStart = k * step;
                float dEnd = (k + 1) * step;
                float dMid = (dStart + dEnd) * 0.5f;

                float t0 = Mathf.Clamp01(dStart / length);
                float t1 = Mathf.Clamp01(dEnd / length);
                float tm = Mathf.Clamp01(dMid / length);

                container.Evaluate(0, t0, out var p0, out _, out _);
                container.Evaluate(0, t1, out var p1, out _, out _);
                container.Evaluate(0, tm, out var pm, out _, out _);

                float h0 = GetTerrainHeight((Vector3)p0);
                float h1 = GetTerrainHeight((Vector3)p1);
                float hm = GetTerrainHeight((Vector3)pm);

                float slope = Mathf.Abs(h1 - h0);
                float curvature = Mathf.Abs(hm - (h0 + h1) * 0.5f) * 2f;
                float metric = Mathf.Max(slope, curvature);

                if (metric > adaptiveHeightThreshold)
                {
                    int mult = Mathf.CeilToInt(metric / adaptiveHeightThreshold);
                    subdiv[k] = Mathf.Clamp(mult, 1, Mathf.Max(1, adaptiveMaxSubdivision));
                }
            }
        }

        int totalSubTiles = 0;
        for (int k = 0; k < baseTiles; k++) totalSubTiles += subdiv[k];

        int[] subTileBase = new int[totalSubTiles];
        int[] subTileN = new int[totalSubTiles];
        int[] subTileIndex = new int[totalSubTiles];
        {
            int st = 0;
            for (int k = 0; k < baseTiles; k++)
            {
                int n = subdiv[k];
                for (int s = 0; s < n; s++)
                {
                    subTileBase[st] = k;
                    subTileN[st] = n;
                    subTileIndex[st] = s;
                    st++;
                }
            }
        }

        float[] subTileDBoundary = new float[totalSubTiles + 1];
        for (int i = 0; i < totalSubTiles; i++)
        {
            int k = subTileBase[i];
            int n = subTileN[i];
            int s = subTileIndex[i];
            subTileDBoundary[i] = (k + s / (float)n + 0.5f) * step;
        }
        {
            int lastK = subTileBase[totalSubTiles - 1];
            subTileDBoundary[totalSubTiles] = (lastK + 1.5f) * step;
        }

        // ---- 2) Look-ahead + smoothing по холмам (уже по ПОДтайлам) ----
        int[] baseTileOfSub = subTileBase;
        float[] rawLift = new float[baseTiles];
        if (snapToTerrain && terrain != null && hillClearance > 0f)
        {
            float detectionRadius = hillDetectionDistance > 0.01f
                ? hillDetectionDistance
                : Mathf.Max(roadWidth * 1.5f, 8f);

            for (int k = 0; k < baseTiles; k++)
            {
                float dist = (k + 0.5f) * step;
                float tt = Mathf.Clamp01(dist / length);
                container.Evaluate(0, tt, out var cp, out var ctan, out var cup);
                Vector3 c = (Vector3)cp;
                Vector3 cfwd = ((Vector3)ctan).normalized;
                Vector3 cu = ((Vector3)cup).normalized;
                Vector3 crt = Vector3.Cross(cu, cfwd).normalized;

                float centerTh = GetTerrainHeight(c);
                float sidesAvg = (
                    GetTerrainHeight(c + crt * detectionRadius) +
                    GetTerrainHeight(c - crt * detectionRadius) +
                    GetTerrainHeight(c + cfwd * detectionRadius) +
                    GetTerrainHeight(c - cfwd * detectionRadius)
                ) * 0.25f;

                float hillAmount = Mathf.Max(0f, centerTh - sidesAvg);
                float factor = Mathf.Clamp01(hillAmount / 1.5f);
                rawLift[k] = hillClearance * factor;
            }
        }

        float[] baseTileHillLift = new float[baseTiles];
        if (baseTiles > 1 && hillClearance > 0f)
        {
            int lookAheadTiles = hillLookAheadDistance > 0.01f
                ? Mathf.Max(1, Mathf.RoundToInt(hillLookAheadDistance / step))
                : 1;

            float[] rampedLift = new float[baseTiles];
            for (int k = 0; k < baseTiles; k++)
            {
                float best = 0f;
                for (int j = -lookAheadTiles; j <= lookAheadTiles; j++)
                {
                    int jj = WrapTile(k + j, baseTiles, isClosed);
                    float ratio = Mathf.Abs(j) / (float)lookAheadTiles;
                    float f = 1f - ratio;
                    f = f * f * (3f - 2f * f);
                    float contrib = rawLift[jj] * f;
                    if (contrib > best) best = contrib;
                }
                rampedLift[k] = best;
            }

            int smoothR = hillSmoothDistance > 0.01f
                ? Mathf.Max(1, Mathf.RoundToInt(hillSmoothDistance / step))
                : 1;

            for (int pass = 0; pass < 2; pass++)
            {
                float[] smoothed = new float[baseTiles];
                for (int k = 0; k < baseTiles; k++)
                {
                    float sum = 0f, wsum = 0f;
                    for (int j = -smoothR; j <= smoothR; j++)
                    {
                        int jj = WrapTile(k + j, baseTiles, isClosed);
                        float w = 1f - Mathf.Abs(j) / (float)(smoothR + 1);
                        sum += rampedLift[jj] * w;
                        wsum += w;
                    }
                    smoothed[k] = sum / Mathf.Max(0.0001f, wsum);
                }
                rampedLift = smoothed;
            }

            for (int k = 0; k < baseTiles; k++)
                baseTileHillLift[k] = Mathf.Max(rawLift[k], rampedLift[k]);
        }

        int topCount = vc * totalSubTiles;

        var wpArr = new Vector3[topCount];
        var upArr = new Vector3[topCount];
        var rtArr = new Vector3[topCount];
        var dArr = new float[topCount];
        var heightAbove = new float[topCount];

        var verts = new List<Vector3>(topCount * 3);
        var norms = new List<Vector3>(topCount * 3);
        var uvs = new List<Vector2>(topCount * 3);
        var tris = new List<int>(srcT.Length * totalSubTiles);
        var slabTris = new List<int>();
        var embankTris = new List<int>();

        Matrix4x4 w2l = transform.worldToLocalMatrix;

        // ---- 3) Верх дороги ----
        for (int st = 0; st < totalSubTiles; st++)
        {
            int k = subTileBase[st];
            int n = subTileN[st];
            int s = subTileIndex[st];
            float subStart = s / (float)n;
            float subEnd = (s + 1) / (float)n;
            float extraHillLift = baseTileHillLift[k];

            for (int i = 0; i < vc; i++)
            {
                float localSrcZ = srcV[i].z / segmentLength;
                float localT = Mathf.Lerp(subStart, subEnd, localSrcZ);
                float d = (k + localT + 0.5f) * step;
                float t = Mathf.Clamp01(d / length);

                container.Evaluate(0, t, out var p, out var tan, out var upv);
                Vector3 fwd = ((Vector3)tan).normalized;
                Vector3 u = ((Vector3)upv).normalized;
                Vector3 rt = Vector3.Cross(u, fwd).normalized;

                Vector3 wp = (Vector3)p + rt * srcV[i].x + u * srcV[i].y;

                if (snapToTerrain && terrain != null)
                {
                    float th = GetTerrainHeight(wp);
                    float desiredY = Mathf.Max(th + minHeightAboveTerrain + extraHillLift, wp.y);
                    wp.y = Mathf.Lerp(wp.y, desiredY, snapStrength);
                }

                float hAbove = wp.y - GetTerrainHeight(wp);
                int idx = st * vc + i;
                heightAbove[idx] = hAbove;
                wpArr[idx] = wp;
                upArr[idx] = u;
                rtArr[idx] = rt;
                dArr[idx] = d;

                verts.Add(w2l.MultiplyPoint3x4(wp));
                if (hasN)
                {
                    Vector3 nrm = rt * srcN[i].x + u * srcN[i].y + fwd * srcN[i].z;
                    norms.Add(w2l.MultiplyVector(nrm).normalized);
                }
                else
                {
                    norms.Add(w2l.MultiplyVector(u).normalized);
                }
                uvs.Add(hasUV ? srcUV[i] : Vector2.zero);
            }

            for (int j = 0; j < srcT.Length; j += 3)
            {
                int a = srcT[j] + st * vc;
                int b = srcT[j + 1] + st * vc;
                int c = srcT[j + 2] + st * vc;
                if (flipTriangles) { int tmp = b; b = c; c = tmp; }
                AddTri(tris, a, b, c);
            }
        }

        // ---- 4) Плита ----
        if (hasSlab)
        {
            int bottomStart = verts.Count;
            for (int st = 0; st < totalSubTiles; st++)
            {
                for (int i = 0; i < vc; i++)
                {
                    int idx = st * vc + i;
                    verts.Add(w2l.MultiplyPoint3x4(wpArr[idx] - upArr[idx] * T));
                    norms.Add(w2l.MultiplyVector(-upArr[idx]).normalized);
                    uvs.Add(new Vector2((srcV[i].x - xMin) / tile, dArr[idx] / tile));
                }
            }
            for (int j = 0; j < tris.Count; j += 3)
                AddTri(slabTris, tris[j] + bottomStart, tris[j + 2] + bottomStart, tris[j + 1] + bottomStart);

            for (int side = 0; side < 2; side++)
            {
                List<int> edge = side == 0 ? leftEdge : rightEdge;
                float sgn = side == 0 ? -1f : 1f;
                if (edge.Count < 2) continue;

                for (int st = 0; st < totalSubTiles; st++)
                {
                    int baseIdx = verts.Count;
                    for (int e = 0; e < edge.Count; e++)
                    {
                        int idx = st * vc + edge[e];
                        Vector3 n = w2l.MultiplyVector(rtArr[idx] * sgn).normalized;
                        verts.Add(w2l.MultiplyPoint3x4(wpArr[idx]));
                        verts.Add(w2l.MultiplyPoint3x4(wpArr[idx] - upArr[idx] * T));
                        norms.Add(n); norms.Add(n);
                        float uu = dArr[idx] / tile;
                        uvs.Add(new Vector2(uu, 0f));
                        uvs.Add(new Vector2(uu, T / tile));
                    }
                    for (int e = 0; e < edge.Count - 1; e++)
                    {
                        int a = baseIdx + e * 2;
                        int b = a + 1;
                        int c = a + 2;
                        int d = a + 3;
                        if (side == 0)
                        {
                            AddTri(slabTris, a, b, c);
                            AddTri(slabTris, c, b, d);
                        }
                        else
                        {
                            AddTri(slabTris, a, c, b);
                            AddTri(slabTris, c, d, b);
                        }
                    }
                }
            }
        }

        // ---- 5) Флаг скосов для ПОДтайлов ----
        bool[] subTileHasEmb = new bool[totalSubTiles];
        if (generateEmbankments && embankmentMaterial != null && leftEdge.Count >= 2 && rightEdge.Count >= 2)
        {
            for (int st = 0; st < totalSubTiles; st++)
            {
                float avg = 0f;
                int cnt = 0;
                foreach (int ei in leftEdge) { avg += heightAbove[st * vc + ei]; cnt++; }
                foreach (int ei in rightEdge) { avg += heightAbove[st * vc + ei]; cnt++; }
                avg /= Mathf.Max(1, cnt);
                subTileHasEmb[st] = avg >= 0.15f && avg <= embankmentMaxHeight;
            }
        }

        // ---- 6) Скосы ----
        if (generateEmbankments && embankmentMaterial != null)
        {
            float slopeRad = embankmentSlopeAngle * Mathf.Deg2Rad;
            float slopeTan = Mathf.Tan(slopeRad);

            for (int side = 0; side < 2; side++)
            {
                List<int> edge = side == 0 ? leftEdge : rightEdge;
                float sgn = side == 0 ? -1f : 1f;
                if (edge.Count < 2) continue;

                for (int st = 0; st < totalSubTiles; st++)
                {
                    if (!subTileHasEmb[st]) continue;

                    int baseIdx = verts.Count;
                    for (int e = 0; e < edge.Count; e++)
                    {
                        int idx = st * vc + edge[e];
                        Vector3 top = wpArr[idx];
                        float h = heightAbove[idx];
                        float run = h / slopeTan;

                        Vector3 bottom = top - upArr[idx] * h + rtArr[idx] * (sgn * run);
                        bottom.y = GetTerrainHeight(bottom);

                        verts.Add(w2l.MultiplyPoint3x4(top));
                        verts.Add(w2l.MultiplyPoint3x4(bottom));

                        Vector3 n = (rtArr[idx] * sgn).normalized;
                        norms.Add(w2l.MultiplyVector(n));
                        norms.Add(w2l.MultiplyVector(n));

                        float uu = dArr[idx] / embankmentTileSize;
                        uvs.Add(new Vector2(uu, 0f));
                        uvs.Add(new Vector2(uu, h / embankmentTileSize));
                    }

                    for (int e = 0; e < edge.Count - 1; e++)
                    {
                        int a = baseIdx + e * 2;
                        int b = a + 1;
                        int c = a + 2;
                        int d = a + 3;
                        if (side == 0)
                        {
                            AddTri(embankTris, a, b, c);
                            AddTri(embankTris, c, b, d);
                        }
                        else
                        {
                            AddTri(embankTris, a, c, b);
                            AddTri(embankTris, c, d, b);
                        }
                    }
                }
            }

            AddEmbankmentEndCaps(totalSubTiles, subTileDBoundary, length, xMin, xMax,
                                 subTileHasEmb, w2l, verts, norms, uvs, embankTris);
        }

        // ---- 7) Столбы ----
        if (generatePillars && pillarStartHeight > 0f && pillarSpacing > 0.01f)
        {
            float spacing = Mathf.Max(0.5f, pillarSpacing);
            float topOffset = Mathf.Max(pillarTopOffset, slabThickness + 0.05f);

            for (float dist = spacing * 0.5f; dist < length; dist += spacing)
            {
                float t = dist / length;
                container.Evaluate(0, t, out var p, out _, out _);
                Vector3 pos = (Vector3)p;
                float h = pos.y - GetTerrainHeight(pos);

                if (h < pillarStartHeight) continue;

                float pillarH = h - topOffset;
                if (pillarH <= 0.05f) continue;

                Vector3 top = new Vector3(pos.x, pos.y - topOffset, pos.z);
                SpawnPillar(top, pillarH);
            }
        }

        // ---- 8) Сборка меша ----
        if (generated == null)
            generated = new Mesh { name = "AdvancedRoad" };

        generated.Clear();
        generated.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        generated.SetVertices(verts);
        generated.SetNormals(norms);
        generated.SetUVs(0, uvs);

        int subCount = 1;
        if (hasSlab) subCount++;
        if (embankTris.Count > 0) subCount++;

        generated.subMeshCount = subCount;
        generated.SetTriangles(tris, 0);
        int sub = 1;
        if (hasSlab)
        {
            generated.SetTriangles(slabTris, sub);
            sub++;
        }
        if (embankTris.Count > 0)
            generated.SetTriangles(embankTris, sub);

        generated.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = generated;

        var mr = GetComponent<MeshRenderer>();
        var mats = new List<Material>();
        mats.Add(mr.sharedMaterials.Length > 0 ? mr.sharedMaterials[0] : null);
        if (hasSlab) mats.Add(slabMaterial);
        if (embankTris.Count > 0) mats.Add(embankmentMaterial);
        mr.sharedMaterials = mats.ToArray();

        var mc = GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = generated;
    }

    void AddEmbankmentEndCaps(int totalSubTiles, float[] subTileDBoundary, float length,
                              float xMin, float xMax, bool[] subTileHasEmb,
                              Matrix4x4 w2l,
                              List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        float slopeTan = Mathf.Tan(Mathf.Max(1f, embankmentSlopeAngle) * Mathf.Deg2Rad);
        float T = slabThickness;
        float tileSize = Mathf.Max(0.01f, embankmentTileSize);

        for (int st = 0; st <= totalSubTiles; st++)
        {
            bool prevHasEmb = st > 0 && subTileHasEmb[st - 1];
            bool currHasEmb = st < totalSubTiles && subTileHasEmb[st];
            if (prevHasEmb == currHasEmb) continue;

            float d = subTileDBoundary[st];
            float t = Mathf.Clamp01(d / length);

            container.Evaluate(0, t, out var p, out var tan, out var upv);
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

            int baseIdx = verts.Count;
            Vector3[] capVerts = { lTop, lSlabBot, lBot, rBot, rSlabBot, rTop };
            Vector3[] capNormals = {
                (-rt + u).normalized,
                (-rt - u).normalized,
                (-rt + Vector3.down).normalized,
                ( rt + Vector3.down).normalized,
                ( rt - u).normalized,
                ( rt + u).normalized
            };

            for (int i = 0; i < 6; i++)
            {
                verts.Add(w2l.MultiplyPoint3x4(capVerts[i]));
                norms.Add(w2l.MultiplyVector(capNormals[i]).normalized);
            }

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, T / tileSize));
            uvs.Add(new Vector2(0f, lH / tileSize));
            uvs.Add(new Vector2(1f, rH / tileSize));
            uvs.Add(new Vector2(1f, T / tileSize));
            uvs.Add(new Vector2(1f, 0f));

            int L = baseIdx + 0, LS = baseIdx + 1, LB = baseIdx + 2;
            int RB = baseIdx + 3, RS = baseIdx + 4, R = baseIdx + 5;

            AddTri(tris, L, LS, LB);
            AddTri(tris, LS, RB, LB);
            AddTri(tris, LS, RS, RB);
            AddTri(tris, RS, R, RB);

            AddTri(tris, LB, LS, L);
            AddTri(tris, LB, RB, LS);
            AddTri(tris, RB, RS, LS);
            AddTri(tris, RB, R, RS);
        }
    }

    void SpawnPillar(Vector3 topWorld, float height)
    {
        EnsurePillarsRoot();
        Vector3 center = new Vector3(topWorld.x, topWorld.y - height * 0.5f, topWorld.z);

        GameObject go;
        if (pillarPrefab != null)
        {
            go = Instantiate(pillarPrefab, pillarsRoot);
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
            go.transform.SetParent(pillarsRoot, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(pillarDiameter, height * 0.5f, pillarDiameter);

            var r = go.GetComponent<Renderer>();
            if (pillarMaterial != null) r.sharedMaterial = pillarMaterial;
        }

        LowerPillarColliders(go, pillarColliderTopShrink);
        spawnedPillars.Add(go);
    }

    void LowerPillarColliders(GameObject pillar, float shrinkWorld)
    {
        if (pillar == null || shrinkWorld <= 0.0001f) return;

        var colliders = pillar.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            if (c == null) continue;

            float sy = c.transform.lossyScale.y;
            if (sy < 0.0001f) sy = 0.0001f;
            float localShrink = shrinkWorld / sy;

            if (c is CapsuleCollider cap)
            {
                cap.height = Mathf.Max(0.01f, cap.height - localShrink);
                var ctr = cap.center;
                ctr.y -= localShrink * 0.5f;
                cap.center = ctr;
            }
            else if (c is BoxCollider box)
            {
                var sz = box.size;
                sz.y = Mathf.Max(0.01f, sz.y - localShrink);
                box.size = sz;
                var ctr = box.center;
                ctr.y -= localShrink * 0.5f;
                box.center = ctr;
            }
            else
            {
                c.transform.position += Vector3.down * (shrinkWorld * 0.5f);
            }
        }
    }

    void ClearPillars()
    {
        var toDestroy = new HashSet<GameObject>();

        foreach (var p in spawnedPillars)
            if (p != null) toDestroy.Add(p);
        spawnedPillars.Clear();

        if (pillarsRoot == null)
        {
            var existing = transform.Find("Pillars");
            if (existing != null) pillarsRoot = existing;
        }
        if (pillarsRoot != null)
        {
            for (int i = pillarsRoot.childCount - 1; i >= 0; i--)
            {
                var child = pillarsRoot.GetChild(i);
                if (child != null) toDestroy.Add(child.gameObject);
            }
        }

        foreach (var go in toDestroy)
        {
            if (go == null) continue;
            go.transform.SetParent(null, false);
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(go);
            else Destroy(go);
#else
            Destroy(go);
#endif
        }
    }
}