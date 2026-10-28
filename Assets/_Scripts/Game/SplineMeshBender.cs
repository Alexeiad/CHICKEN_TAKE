using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Splines;

// Повторяет Source Mesh вдоль сплайна и изгибает его по кривой.
// Исходный меш: длина вдоль оси Z, ширина вдоль X, центр в нуле (как road_4lane_1m).
// Верх дороги остаётся на высоте сплайна, плита (бока + низ) уходит вниз на Slab Thickness.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SplineMeshBender : MonoBehaviour
{
    public SplineContainer container;
    public Mesh sourceMesh;

    [Tooltip("Длина исходного куска вдоль Z, метры")]
    public float segmentLength = 1f;

    [Tooltip("Включи, если верх дороги виден только снизу")]
    public bool flipTriangles = false;

    [Header("Толщина дороги (плита)")]
    [FormerlySerializedAs("generateUnderside")]
    public bool generateSlab = true;

    [Min(0f)]
    [Tooltip("Толщина плиты вниз от поверхности, метры (0.5 или 1)")]
    public float slabThickness = 0.5f;

    [FormerlySerializedAs("undersideMaterial")]
    [Tooltip("Материал боков и низа (бетон)")]
    public Material slabMaterial;

    [Tooltip("Сколько метров занимает одна повторка текстуры на боках и низу")]
    public float slabTileSize = 1f;

    Mesh generated;

    void OnEnable()
    {
        Spline.Changed += OnSplineChanged;
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

    static void AddTri(List<int> list, int a, int b, int c)
    {
        list.Add(a);
        list.Add(b);
        list.Add(c);
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (container == null || sourceMesh == null || segmentLength <= 0f)
            return;

        float length = container.CalculateLength(0);
        if (length < 0.01f)
            return;

        Vector3[] srcV = sourceMesh.vertices;
        Vector3[] srcN = sourceMesh.normals;
        Vector2[] srcUV = sourceMesh.uv;
        int[] srcT = sourceMesh.GetTriangles(0);

        bool hasN = srcN != null && srcN.Length == srcV.Length;
        bool hasUV = srcUV != null && srcUV.Length == srcV.Length;
        bool hasSlab = generateSlab && slabThickness > 0.001f && slabMaterial != null;
        float T = slabThickness;
        float tile = Mathf.Max(0.01f, slabTileSize);

        // Крайние вершины исходного меша (левый и правый край), по Z
        float xMin = sourceMesh.bounds.min.x;
        float xMax = sourceMesh.bounds.max.x;
        var leftEdge = new List<int>();
        var rightEdge = new List<int>();
        for (int i = 0; i < srcV.Length; i++)
        {
            if (Mathf.Abs(srcV[i].x - xMin) < 0.001f) leftEdge.Add(i);
            if (Mathf.Abs(srcV[i].x - xMax) < 0.001f) rightEdge.Add(i);
        }
        leftEdge.Sort((a, b) => srcV[a].z.CompareTo(srcV[b].z));
        rightEdge.Sort((a, b) => srcV[a].z.CompareTo(srcV[b].z));

        // Целое число кусков, слегка растягиваем, чтобы замкнутая трасса сходилась без шва
        int tiles = Mathf.Max(1, Mathf.RoundToInt(length / segmentLength));
        float step = length / tiles;

        int vc = srcV.Length;
        int topCount = vc * tiles;

        var wpArr = new Vector3[topCount];
        var upArr = new Vector3[topCount];
        var rtArr = new Vector3[topCount];
        var dArr = new float[topCount];

        var verts = new List<Vector3>(topCount * 2);
        var norms = new List<Vector3>(topCount * 2);
        var uvs = new List<Vector2>(topCount * 2);
        var tris = new List<int>(srcT.Length * tiles);
        var slabTris = new List<int>();

        Matrix4x4 w2l = transform.worldToLocalMatrix;

        // ---- Верх дороги ----
        for (int k = 0; k < tiles; k++)
        {
            for (int i = 0; i < vc; i++)
            {
                float d = (k + srcV[i].z / segmentLength + 0.5f) * step;
                float t = Mathf.Clamp01(d / length);

                container.Evaluate(0, t, out var p, out var tan, out var upv);

                Vector3 fwd = ((Vector3)tan).normalized;
                Vector3 u = ((Vector3)upv).normalized;
                Vector3 rt = Vector3.Cross(u, fwd).normalized;

                Vector3 wp = (Vector3)p + rt * srcV[i].x + u * srcV[i].y;
                int idx = k * vc + i;

                wpArr[idx] = wp;
                upArr[idx] = u;
                rtArr[idx] = rt;
                dArr[idx] = d;

                verts.Add(w2l.MultiplyPoint3x4(wp));

                if (hasN)
                {
                    Vector3 n = rt * srcN[i].x + u * srcN[i].y + fwd * srcN[i].z;
                    norms.Add(w2l.MultiplyVector(n).normalized);
                }
                else
                {
                    norms.Add(w2l.MultiplyVector(u).normalized);
                }

                uvs.Add(hasUV ? srcUV[i] : Vector2.zero);
            }

            for (int j = 0; j < srcT.Length; j += 3)
            {
                int a = srcT[j] + k * vc;
                int b = srcT[j + 1] + k * vc;
                int c = srcT[j + 2] + k * vc;
                if (flipTriangles) { int tmp = b; b = c; c = tmp; }
                AddTri(tris, a, b, c);
            }
        }

        // ---- Плита: низ + два боковых борта ----
        if (hasSlab)
        {
            // Низ: копия верха, сдвинутая вниз, с перевёрнутыми треугольниками
            int bottomStart = verts.Count;
            for (int k = 0; k < tiles; k++)
            {
                for (int i = 0; i < vc; i++)
                {
                    int idx = k * vc + i;
                    verts.Add(w2l.MultiplyPoint3x4(wpArr[idx] - upArr[idx] * T));
                    norms.Add(w2l.MultiplyVector(-upArr[idx]).normalized);
                    uvs.Add(new Vector2((srcV[i].x - xMin) / tile, dArr[idx] / tile));
                }
            }
            for (int j = 0; j < tris.Count; j += 3)
                AddTri(slabTris, tris[j] + bottomStart, tris[j + 2] + bottomStart, tris[j + 1] + bottomStart);

            // Борта: отдельные вершины с горизонтальными нормалями
            for (int side = 0; side < 2; side++)
            {
                List<int> edge = side == 0 ? leftEdge : rightEdge;
                float sgn = side == 0 ? -1f : 1f;
                if (edge.Count < 2) continue;

                for (int k = 0; k < tiles; k++)
                {
                    int baseIdx = verts.Count;
                    for (int e = 0; e < edge.Count; e++)
                    {
                        int idx = k * vc + edge[e];
                        Vector3 n = w2l.MultiplyVector(rtArr[idx] * sgn).normalized;
                        verts.Add(w2l.MultiplyPoint3x4(wpArr[idx]));
                        verts.Add(w2l.MultiplyPoint3x4(wpArr[idx] - upArr[idx] * T));
                        norms.Add(n);
                        norms.Add(n);
                        float uu = dArr[idx] / tile;
                        uvs.Add(new Vector2(uu, 0f));
                        uvs.Add(new Vector2(uu, T / tile));
                    }

                    for (int e = 0; e < edge.Count - 1; e++)
                    {
                        int a = baseIdx + e * 2;   // верх e
                        int b = a + 1;             // низ e
                        int c = a + 2;             // верх e+1
                        int d = a + 3;             // низ e+1
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

        if (generated == null)
            generated = new Mesh { name = "BentRoad" };
        generated.Clear();
        generated.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        generated.SetVertices(verts);
        generated.SetNormals(norms);
        generated.SetUVs(0, uvs);
        generated.subMeshCount = hasSlab ? 2 : 1;
        generated.SetTriangles(tris, 0);
        if (hasSlab)
            generated.SetTriangles(slabTris, 1);
        generated.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = generated;

        // Материалы: 0 = верх (асфальт), 1 = бока и низ (бетон)
        var mr = GetComponent<MeshRenderer>();
        var mats = mr.sharedMaterials;
        if (hasSlab)
        {
            if (mats.Length != 2 || mats[1] != slabMaterial)
                mr.sharedMaterials = new[] { mats.Length > 0 ? mats[0] : null, slabMaterial };
        }
        else if (mats.Length > 1)
        {
            mr.sharedMaterials = new[] { mats[0] };
        }

        var mc = GetComponent<MeshCollider>();
        if (mc != null)
            mc.sharedMesh = generated;
    }
}