using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoboEntry
{
    public string name = "ロボ";
    public TrailDrawer drawer;
}

public class ButtonEvent : MonoBehaviour
{
    [SerializeField]
    RoboEntry[] robots = new RoboEntry[]
    {
        new RoboEntry { name = "ロボ1" },
        new RoboEntry { name = "ロボ2" }
    };

    [Header("フィールド設定")]
    [SerializeField, Tooltip("OnDrawGizmosで表示する長方形の基準サイズ")]
    Vector2 field = Vector2.one;
    [SerializeField, Tooltip("長方形のサイズ補正")]
    float offset = 1f;

    [Header("判定グリッド設定")]
    [SerializeField, Min(1), Tooltip("縦横の分割数に掛けるグリッド補正")]
    int grid = 1;

    [Header("フィールドの可視化")]
    [SerializeField, Tooltip("枠線の色")]
    Color fieldLineColor = Color.white;

    void Update() { }

    Vector2 GetFieldHalfSize()
    {
        return new Vector2(Mathf.Abs(field.x * offset), Mathf.Abs(field.y * offset));
    }

    // ─── ボタン（汎用） ───

    public void PaintRobot(int index)
    {
        if (index >= 0 && index < robots.Length && robots[index].drawer != null)
            robots[index].drawer.AccessedFromPaintButton();
    }

    // 既存ボタン互換
    public void OnPaintButton1Clicked() { PaintRobot(0); }
    public void OnPaintButton2Clicked() { PaintRobot(1); }

    public void OnResetButtonClicked()
    {
        foreach (var r in robots)
            if (r.drawer != null) r.drawer.ResetAllTrails();
    }

    // ─── 判定 ───

    public void OnJudgeButtonClicked()
    {
        long correction = Mathf.Max(1, grid);
        long verticalCountLong = Mathf.Max(1, Mathf.RoundToInt(field.y)) * correction;
        long horizontalCountLong = Mathf.Max(1, Mathf.RoundToInt(field.x)) * correction;
        if (verticalCountLong > int.MaxValue || horizontalCountLong > int.MaxValue)
        {
            Debug.LogError("[Judge] 分割数が大きすぎます。");
            return;
        }
        int verticalCount = (int)verticalCountLong;
        int horizontalCount = (int)horizontalCountLong;
        long totalCellCount = (long)verticalCount * horizontalCount;
        if (totalCellCount > int.MaxValue)
        {
            Debug.LogError("[Judge] 分割数が大きすぎます。");
            return;
        }
        int totalCells = (int)totalCellCount;
        Vector2 halfSize = GetFieldHalfSize();
        if (halfSize.x <= Mathf.Epsilon || halfSize.y <= Mathf.Epsilon)
        {
            Debug.LogError("[Judge] フィールドのサイズは0より大きく設定してください。");
            return;
        }
        Vector2 fieldMin = -halfSize;
        Vector2 fieldSize = halfSize * 2f;

        // 1. 長方形グリッドのセル中心を生成
        var centroids = new Vector2[totalCells];
        BuildRectangularGrid(centroids, verticalCount, horizontalCount, fieldMin, fieldSize);

        // 2. 粗いグリッドにバケッティング（空間検索の高速化）
        const int coarseResX = 50;
        const int coarseResY = 50;
        Vector2 coarseCell = new Vector2(fieldSize.x / coarseResX, fieldSize.y / coarseResY);

        var buckets = new List<int>[coarseResX * coarseResY];
        for (int i = 0; i < buckets.Length; i++)
            buckets[i] = new List<int>();

        for (int i = 0; i < totalCells; i++)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt((centroids[i].x - fieldMin.x) / coarseCell.x), 0, coarseResX - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt((centroids[i].y - fieldMin.y) / coarseCell.y), 0, coarseResY - 1);
            buckets[cy * coarseResX + cx].Add(i);
        }

        // 3. 全メッシュセグメントを収集
        var segments = new List<(MeshFilter mf, int order, byte owner)>();
        for (int r = 0; r < robots.Length && r < 255; r++)
            CollectSegments(robots[r].drawer, (byte)(r + 1), segments);

        segments.Sort((a, b) => a.order.CompareTo(b.order));

        // 4. 描画順にラスタライズ（後のセグメントが上書き）
        var owners = new byte[totalCells];
        foreach (var seg in segments)
        {
            if (seg.mf == null || seg.mf.sharedMesh == null) continue;
            RasterizeMesh(owners, centroids, buckets, coarseResX, coarseResY, fieldMin, coarseCell,
                          seg.mf, seg.owner);
        }

        // 5. カウント
        int[] counts = new int[robots.Length];
        for (int i = 0; i < totalCells; i++)
        {
            byte v = owners[i];
            if (v > 0 && v <= robots.Length)
                counts[v - 1]++;
        }

        // 6. 結果出力（割合表示 + 最多判定）
        int maxCount = 0;
        int winnerIdx = -1;
        bool tie = false;
        for (int r = 0; r < robots.Length; r++)
        {
            if (counts[r] > maxCount)
            {
                maxCount = counts[r];
                winnerIdx = r;
                tie = false;
            }
            else if (counts[r] == maxCount && maxCount > 0)
            {
                tie = true;
            }
        }

        string result = "";
        for (int r = 0; r < robots.Length; r++)
        {
            float pct = totalCells > 0 ? (float)counts[r] / totalCells * 100f : 0f;
            result += $"{robots[r].name}: {pct:F1}%  ";
        }

        string label = (tie || winnerIdx < 0) ? "同じ" : $"{robots[winnerIdx].name}の方が多い";
        EffectManager.Instance.WinnerEffect(winnerIdx);
        Debug.Log($"[Judge] {result}→ {label}");
    }

    // ─── 長方形グリッド生成 ───

    void BuildRectangularGrid(Vector2[] centroids, int verticalCount, int horizontalCount,
                              Vector2 fieldMin, Vector2 fieldSize)
    {
        int idx = 0;
        float cellWidth = fieldSize.x / horizontalCount;
        float cellHeight = fieldSize.y / verticalCount;
        for (int y = 0; y < verticalCount; y++)
        {
            for (int x = 0; x < horizontalCount; x++)
            {
                centroids[idx++] = new Vector2(
                    fieldMin.x + (x + 0.5f) * cellWidth,
                    fieldMin.y + (y + 0.5f) * cellHeight);
            }
        }
    }

    // ─── セグメント収集 ───

    void CollectSegments(TrailDrawer drawer, byte owner,
                         List<(MeshFilter, int, byte)> segments)
    {
        if (drawer == null) return;
        foreach (var mr in drawer.GetTrailRenderers())
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                segments.Add((mf, mr.sortingOrder, owner));
        }
    }

    // ─── ラスタライズ ───

    void RasterizeMesh(byte[] owners, Vector2[] centroids,
                       List<int>[] buckets, int coarseResX, int coarseResY,
                       Vector2 bboxMin, Vector2 coarseCell,
                       MeshFilter mf, byte owner)
    {
        var mesh = mf.sharedMesh;
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        var tr = mf.transform;

        var wv = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 wp = tr.TransformPoint(verts[i]);
            wv[i] = new Vector2(wp.x, wp.y);
        }

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector2 a = wv[tris[i]];
            Vector2 b = wv[tris[i + 1]];
            Vector2 c = wv[tris[i + 2]];

            // バウンディングボックス → 粗グリッド範囲
            int cxMin = Mathf.Max(0,
                Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - bboxMin.x) / coarseCell.x));
            int cxMax = Mathf.Min(coarseResX - 1,
                Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - bboxMin.x) / coarseCell.x));
            int cyMin = Mathf.Max(0,
                Mathf.FloorToInt((Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - bboxMin.y) / coarseCell.y));
            int cyMax = Mathf.Min(coarseResY - 1,
                Mathf.FloorToInt((Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - bboxMin.y) / coarseCell.y));

            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                for (int cx = cxMin; cx <= cxMax; cx++)
                {
                    var bucket = buckets[cy * coarseResX + cx];
                    for (int bi = 0; bi < bucket.Count; bi++)
                    {
                        int ci = bucket[bi];
                        if (PointInTriangle(centroids[ci].x, centroids[ci].y, a, b, c))
                            owners[ci] = owner;
                    }
                }
            }
        }
    }

    // ─── 判定ユーティリティ ───

    static bool PointInTriangle(float px, float py, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross2D(px, py, a, b);
        float d2 = Cross2D(px, py, b, c);
        float d3 = Cross2D(px, py, c, a);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }

    static float Cross2D(float px, float py, Vector2 e0, Vector2 e1)
    {
        return (px - e1.x) * (e0.y - e1.y) - (e0.x - e1.x) * (py - e1.y);
    }

    // ─── Gizmos（エディタのシーンビューのみ） ───

    void OnDrawGizmos()
    {
        Gizmos.color = fieldLineColor;
        Vector2 halfSize = GetFieldHalfSize();
        Vector3 v1, v2, v3, v4;
        v1 = new Vector3(-halfSize.x, halfSize.y, 0);
        v2 = new Vector3(halfSize.x, halfSize.y, 0);
        v3 = new Vector3(halfSize.x, -halfSize.y, 0);
        v4 = new Vector3(-halfSize.x, -halfSize.y, 0);
        Gizmos.DrawLine(v1, v2);
        Gizmos.DrawLine(v2, v3);
        Gizmos.DrawLine(v3, v4);
        Gizmos.DrawLine(v4, v1);

    }
}
