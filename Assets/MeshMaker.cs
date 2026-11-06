using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshMaker : MonoBehaviour
{
    public Material trailMaterial;
    public float width = 0.3f;

    // 継続セグメントでは false にして始端キャップを省略
    [HideInInspector] public bool useStartCap = true;
    // smoothingIterations は Chaikin 反復回数として使用（デフォルト3）
    [Range(0, 5)] public int smoothingIterations = 3;

    private List<Vector3> points = new List<Vector3>();
    private List<float> widths = new List<float>();
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private bool initialized = false;

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (trailMaterial != null)
        {
            // Trails overlap in the same plane, so depth writes make the first
            // opaque material drawn hide every later trail regardless of its
            // sorting order. Keep the color opaque, but use the transparent
            // queue (and disable depth writes) so Renderer.sortingOrder wins.
            meshRenderer.material = trailMaterial;
            meshRenderer.material.renderQueue = 3000;
            if (meshRenderer.material.HasProperty("_ZWrite"))
                meshRenderer.material.SetInt("_ZWrite", 0);
        }

        mesh = new Mesh();
        mesh.name = gameObject.name + "_trailMesh";
        meshFilter.mesh = mesh;
    }

    public void AddPoint(Vector3 worldPoint, float pointWidth)
    {
        if (mesh == null) return;
        points.Add(worldPoint);
        widths.Add(pointWidth);
        UpdateMesh();
    }

    public float GetLastWidth()
    {
        return widths.Count > 0 ? widths[widths.Count - 1] : width;
    }

    public List<Vector3[]> GetSegmentsWorld()
    {
        var segs = new List<Vector3[]>();
        for (int i = 1; i < points.Count; i++)
            segs.Add(new Vector3[] { points[i - 1], points[i] });
        return segs;
    }

    public void SetSortingOrder(int order)
    {
        if (meshRenderer != null)
            meshRenderer.sortingOrder = order;
    }

    /// <summary>
    /// 末尾の count 個のポイントを返す（セグメント分割時のオーバーラップ用）
    /// </summary>
    public void GetRecentPoints(int count, out List<Vector3> outPts, out List<float> outWs)
    {
        int c = Mathf.Min(count, points.Count);
        int start = points.Count - c;
        outPts = points.GetRange(start, c);
        outWs  = widths.GetRange(start, c);
    }

    public void ClearMesh()
    {
        points.Clear();
        widths.Clear();
        if (mesh != null) mesh.Clear();
    }

    // ─────────────────────────────────────────────
    // メッシュ生成
    // ─────────────────────────────────────────────

    // 全点の間に中点を1つずつ挿入して密度を2倍にする
    static void InsertMidpoints(List<Vector3> p, List<float> w)
    {
        for (int i = p.Count - 2; i >= 0; i--)
        {
            p.Insert(i + 1, (p[i] + p[i + 1]) * 0.5f);
            w.Insert(i + 1, (w[i] + w[i + 1]) * 0.5f);
        }
    }

    // Chaikin コーナーカッティング（端点固定）
    static void ChaikinPass(List<Vector3> p, List<float> w)
    {
        if (p.Count < 3) return;
        var np = new List<Vector3>(p.Count * 2);
        var nw = new List<float>(w.Count * 2);
        np.Add(p[0]); nw.Add(w[0]); // 始端固定
        for (int i = 0; i < p.Count - 1; i++)
        {
            np.Add(p[i] * 0.75f + p[i + 1] * 0.25f);
            np.Add(p[i] * 0.25f + p[i + 1] * 0.75f);
            nw.Add(w[i] * 0.75f + w[i + 1] * 0.25f);
            nw.Add(w[i] * 0.25f + w[i + 1] * 0.75f);
        }
        np.Add(p[p.Count - 1]); nw.Add(w[w.Count - 1]); // 終端固定
        p.Clear(); p.AddRange(np);
        w.Clear(); w.AddRange(nw);
    }

    void UpdateMesh()
    {
        if (points.Count < 2 || mesh == null) return;

        // 1. 近接点の除去（重複点でメッシュが壊れるのを防ぐ）
        float minDist = Mathf.Max(0.005f, width * 0.015f);
        var raw  = new List<Vector3> { points[0] };
        var rawW = new List<float>   { widths[0] };
        for (int i = 1; i < points.Count; i++)
        {
            if ((points[i] - raw[raw.Count - 1]).sqrMagnitude > minDist * minDist)
            {
                raw.Add(points[i]);
                rawW.Add(i < widths.Count ? widths[i] : width);
            }
        }
        // 最終点が除去されないよう強制追加
        if ((points[points.Count - 1] - raw[raw.Count - 1]).sqrMagnitude > 1e-8f)
        {
            raw.Add(points[points.Count - 1]);
            rawW.Add(widths[widths.Count - 1]);
        }
        if (raw.Count < 2) return;

        // 2. 中点挿入 ×3（コーナー付近の点密度を8倍に増やす）
        //    Chaikin は点間距離が均一なほど効果が高いため先に密度を上げる
        InsertMidpoints(raw, rawW);
        InsertMidpoints(raw, rawW);
        InsertMidpoints(raw, rawW);

        // 3. Chaikin スムージング（smoothingIterations 回、デフォルト3）
        int iter = Mathf.Clamp(smoothingIterations, 0, 5);
        for (int k = 0; k < iter; k++)
            ChaikinPass(raw, rawW);

        // 4. 頂点生成（前後セグメントの平均法線でオフセット）
        var pts = raw;
        var ws  = rawW;
        int n   = pts.Count;

        var verts = new List<Vector3>(n * 2);
        var tris  = new List<int>((n - 1) * 6);
        var uvs   = new List<Vector2>(n * 2);
        var norms = new List<Vector3>(n * 2);

        Vector3 prevDir = Vector3.right;

        for (int i = 0; i < n; i++)
        {
            Vector3 p = pts[i];
            Vector3 dA, dB;
            if      (i == 0)     { dA = dB = pts[1] - pts[0]; }
            else if (i == n - 1) { dA = dB = pts[n-1] - pts[n-2]; }
            else                 { dA = pts[i] - pts[i-1]; dB = pts[i+1] - pts[i]; }

            if (dA.sqrMagnitude < 1e-8f) dA = prevDir;
            if (dB.sqrMagnitude < 1e-8f) dB = dA;
            dA.Normalize(); dB.Normalize();
            prevDir = dB;

            Vector3 nA   = Vector3.Cross(dA, Vector3.forward).normalized;
            Vector3 nB   = Vector3.Cross(dB, Vector3.forward).normalized;
            Vector3 norm = nA + nB;
            if (norm.sqrMagnitude < 1e-6f) norm = nA;
            norm.Normalize();

            // マイター長補正: コーナーで幅が細く見える問題を修正
            // 1/cos(θ/2) だが最大3倍にクランプしてスパイクを防ぐ
            float cosA = Vector3.Dot(norm, nA);
            float miter = (cosA > 0.33f) ? (1.0f / cosA) : 3.0f;

            float w = i < ws.Count ? ws[i] : width;
            verts.Add(p + norm * (w * 0.5f * miter));
            verts.Add(p - norm * (w * 0.5f * miter));

            float u2 = (float)i / Mathf.Max(1, n - 1);
            uvs.Add(new Vector2(u2, 0f));
            uvs.Add(new Vector2(u2, 1f));
            // The robot body uses Unity's Quad front face, which points toward
            // -Z. Match that normal so the shared material receives the same
            // lighting on the robot and its trail.
            norms.Add(Vector3.back);
            norms.Add(Vector3.back);

            if (i > 0)
            {
                int idx = i * 2;
                tris.Add(idx-2); tris.Add(idx-1); tris.Add(idx);
                tris.Add(idx);   tris.Add(idx-1); tris.Add(idx+1);
            }
        }

        // 5. 始端キャップ（四角）— 継続セグメントではスキップ
        if (useStartCap && n >= 2)
        {
            Vector3 dir0  = (pts[1] - pts[0]).normalized;
            Vector3 norm0 = Vector3.Cross(dir0, Vector3.forward).normalized;
            float w0 = ws.Count > 0 ? ws[0] : width;
            int ci = verts.Count;
            verts.Add(pts[0] - dir0 * (w0 * 0.5f) + norm0 * (w0 * 0.5f));
            verts.Add(pts[0] - dir0 * (w0 * 0.5f) - norm0 * (w0 * 0.5f));
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f));
            norms.Add(Vector3.back);      norms.Add(Vector3.back);
            tris.Add(ci); tris.Add(0); tris.Add(ci + 1);
            tris.Add(ci + 1); tris.Add(0); tris.Add(1);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(norms);
    }
}
