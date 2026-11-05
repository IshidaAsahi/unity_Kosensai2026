using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public struct ButtonState
    {
        public Button button;
        public bool isPlayButton;
    }

    public static GameManager Instance;
    [Header("Buttons")]
    [SerializeField] ButtonState[] buttons;
    [Header("Field")]
    [SerializeField] Transform field;
    [SerializeField] Color fieldLineColor = Color.white;
    [Header("Judge")]
    [SerializeField, Min(1)] int grid = 1;
    [SerializeField]
    RoboEntry[] robots = new RoboEntry[]
    {
        new RoboEntry { name = "ロボ1" },
        new RoboEntry { name = "ロボ2" }
    };

    LineRenderer fieldRenderer;

    public Transform Field => field;
    [Header("Time")]
    public int LimitTime;
    [SerializeField] TextMeshProUGUI minute;
    [SerializeField] TextMeshProUGUI second;

    public float startTime, elapsedTime;
    [SerializeField] bool isPlay;
    public bool IsPlay => isPlay;
    public bool IsJudgeComplete { get; private set; }
    public float[] RobotPercentages { get; private set; } = new float[0];//塗った割合
    public string JudgeResult { get; private set; }
    [SerializeField, Min(1)] int judgeWorkPerFrame = 20000;
    Coroutine judgeCoroutine;


    void Start()
    {
        Instance = this;
        reset();
    }

    public void reset()
    {
        ChangeState(false);
        TimeUpdate();
    }

    public void ChangeState(bool b)
    {
        isPlay = b;
        foreach (ButtonState buttonState in buttons)
        {
            if (buttonState.button != null)
                buttonState.button.interactable = buttonState.isPlayButton == b;
        }
    }

    public void ChangeLimitTime(int t)
    {
        LimitTime += t;
        TimeUpdate();
    }

    private void TimeUpdate()
    {
        int m = LimitTime / 60;
        int s = LimitTime - m * 60;
        minute.text = m.ToString("00");
        second.text = s.ToString("00");
    }

    public IEnumerator TimerStart()
    {
        startTime = Time.time;
        elapsedTime = Time.time - startTime;
        StartCoroutine(EffectManager.Instance.UpdateTimerEffect());
        while (elapsedTime < LimitTime)
        {
            if (!isPlay)
            {
                break;
            }
            yield return null;
            elapsedTime = Time.time - startTime;
        }
        if (isPlay)
        {
            GameSet();
        }
    }

    public void GameSet()
    {
        if (!isPlay) return;
        ChangeState(false);
        Judge();
        if (EffectManager.Instance != null)
            StartCoroutine(EffectManager.Instance.FinishCount());
    }

    void OnEnable()
    {
        EnsureFieldRenderer();
        UpdateFieldRenderer();
    }

    void Update()
    {
        EnsureFieldRenderer();
        UpdateFieldRenderer();
    }

    void EnsureFieldRenderer()
    {
        if (field == null)
            return;

        if (fieldRenderer == null)
            fieldRenderer = field.GetComponent<LineRenderer>();
        if (fieldRenderer == null)
            return;

        fieldRenderer.useWorldSpace = true;
        fieldRenderer.loop = true;
        fieldRenderer.positionCount = 4;
        fieldRenderer.startWidth = 0.05f;
        fieldRenderer.endWidth = 0.05f;
        fieldRenderer.sortingLayerName = "Field";
        fieldRenderer.sortingOrder = 100;
    }

    void UpdateFieldRenderer()
    {
        if (fieldRenderer == null || field == null)
            return;

        fieldRenderer.startColor = fieldLineColor;
        fieldRenderer.endColor = fieldLineColor;
        fieldRenderer.SetPositions(new[]
        {
            field.TransformPoint(new Vector3(-0.5f, 0.5f, 0)),
            field.TransformPoint(new Vector3(0.5f, 0.5f, 0)),
            field.TransformPoint(new Vector3(0.5f, -0.5f, 0)),
            field.TransformPoint(new Vector3(-0.5f, -0.5f, 0))
        });
    }

    public Vector2 GetFieldHalfSize()
    {
        if (field == null)
            return Vector2.zero;

        Vector3 size = field.lossyScale;
        return new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f;
    }

    public void PaintRobot(int index)
    {
        if (index >= 0 && index < robots.Length && robots[index] != null && robots[index].drawer != null)
            robots[index].drawer.AccessedFromPaintButton();
    }

    public void ResetRobotTrails()
    {
        foreach (var robot in robots)
            if (robot != null && robot.drawer != null) robot.drawer.ResetAllTrails();
    }

    public void Judge()
    {
        if (judgeCoroutine != null) StopCoroutine(judgeCoroutine);
        IsJudgeComplete = false;
        RobotPercentages = new float[robots.Length];
        JudgeResult = null;
        judgeCoroutine = StartCoroutine(JudgeRoutine());
    }

    IEnumerator JudgeRoutine()
    {
        if (Field == null)
        {
            Debug.LogError("[Judge] GameManager または Field が設定されていません。");
            CompleteJudge();
            yield break;
        }
        Transform field = Field;
        long correction = Mathf.Max(1, grid);
        Vector2 fieldSizeInWorld = GetFieldHalfSize() * 2f;
        long verticalCountLong = Mathf.Max(1, Mathf.RoundToInt(fieldSizeInWorld.y)) * correction;
        long horizontalCountLong = Mathf.Max(1, Mathf.RoundToInt(fieldSizeInWorld.x)) * correction;
        if (verticalCountLong > int.MaxValue || horizontalCountLong > int.MaxValue)
        {
            Debug.LogError("[Judge] 分割数が大きすぎます。");
            CompleteJudge();
            yield break;
        }
        int verticalCount = (int)verticalCountLong;
        int horizontalCount = (int)horizontalCountLong;
        long totalCellCount = (long)verticalCount * horizontalCount;
        if (totalCellCount > int.MaxValue)
        {
            Debug.LogError("[Judge] 分割数が大きすぎます。");
            CompleteJudge();
            yield break;
        }
        int totalCells = (int)totalCellCount;
        Vector2 halfSize = GetFieldHalfSize();
        if (halfSize.x <= Mathf.Epsilon || halfSize.y <= Mathf.Epsilon)
        {
            Debug.LogError("[Judge] フィールドのサイズは0より大きく設定してください。");
            CompleteJudge();
            yield break;
        }
        Vector2 fieldMin = new Vector2(field.position.x, field.position.y) - halfSize;
        Vector2 fieldSize = halfSize * 2f;

        // 1. 長方形グリッドのセル中心を生成
        var centroids = new Vector2[totalCells];
        int work = 0;
        float cellWidth = fieldSize.x / horizontalCount;
        float cellHeight = fieldSize.y / verticalCount;
        for (int y = 0; y < verticalCount; y++)
        {
            for (int x = 0; x < horizontalCount; x++)
            {
                int idx = y * horizontalCount + x;
                centroids[idx] = new Vector2(fieldMin.x + (x + 0.5f) * cellWidth,
                                             fieldMin.y + (y + 0.5f) * cellHeight);
                if (++work >= judgeWorkPerFrame) { work = 0; yield return null; }
            }
        }

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
            if (++work >= judgeWorkPerFrame) { work = 0; yield return null; }
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
            yield return RasterizeMeshRoutine(owners, centroids, buckets, coarseResX, coarseResY,
                                              fieldMin, coarseCell, seg.mf, seg.owner);
        }

        // 5. カウント
        int[] counts = new int[robots.Length];
        for (int i = 0; i < totalCells; i++)
        {
            byte v = owners[i];
            if (v > 0 && v <= robots.Length)
                counts[v - 1]++;
            if (++work >= judgeWorkPerFrame) { work = 0; yield return null; }
        }

        // Store each robot's field percentage for effects and future UI.
        string result = "";
        for (int r = 0; r < robots.Length; r++)
        {
            float pct = totalCells > 0 ? (float)counts[r] / totalCells * 100f : 0f;
            RobotPercentages[r] = pct;
            result += $"{robots[r].name}: {pct:F1}%  ";
        }

        JudgeResult = result;
        CompleteJudge();
        Debug.Log($"[Judge] {result}");
    }

    void CompleteJudge()
    {
        IsJudgeComplete = true;
        judgeCoroutine = null;
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

    IEnumerator RasterizeMeshRoutine(byte[] owners, Vector2[] centroids,
                       List<int>[] buckets, int coarseResX, int coarseResY,
                       Vector2 bboxMin, Vector2 coarseCell,
                       MeshFilter mf, byte owner)
    {
        if (mf == null || mf.sharedMesh == null) yield break;
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

        int work = 0;
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
                        if (++work >= judgeWorkPerFrame) { work = 0; yield return null; }
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

}

