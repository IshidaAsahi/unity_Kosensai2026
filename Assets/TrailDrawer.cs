using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class TrailDrawer : MonoBehaviour
{
    [SerializeField] GameObject Mesh;
    [SerializeField] Material roboColor;
    [SerializeField, Tooltip("軌跡が幅0になるまでの時間（秒）")]
    private float writeDuration = 10f;
    [SerializeField, Tooltip("メッシュセグメント分割間隔（秒）。短いほど重なりの前後関係が細かくなる")]
    private float segmentInterval = 0.5f;
    [SerializeField, Min(0f), Tooltip("Trail width reduction per second. Set to 0 to keep a constant width.")]
    private float widthReduction = 0.3f;

    private MeshMaker currentMesh;
    private List<GameObject> createdTrails = new List<GameObject>();
    private static int globalSortingOrder = 20;
    private Coroutine writeCoroutine;
    private bool isWriting = false;

    void Start() { }

    void Update()
    {
        // Stop an active stroke as soon as play mode ends.
        if (GameManager.Instance != null && !GameManager.Instance.IsPlay && isWriting)
            StopWriting();
    }

    // ボタンから呼ばれる
    public void AccessedFromPaintButton()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlay)
            return;

        bool wasWriting = isWriting;
        MeshMaker prevMesh = currentMesh;

        // 既存のコルーチンを停止
        if (writeCoroutine != null)
        {
            StopCoroutine(writeCoroutine);
            writeCoroutine = null;
            isWriting = false;
        }

        // 書き込み中に再押し → 前のメッシュを現在地まで延長し、オーバーラップ付きで継続
        if (wasWriting && prevMesh != null)
        {
            prevMesh.AddPoint(this.transform.position, prevMesh.GetLastWidth());

            List<Vector3> overlapPts;
            List<float> overlapWs;
            prevMesh.GetRecentPoints(4, out overlapPts, out overlapWs);

            // 前セグメントは確定 → MeshMaker を除去して軽量化
            Destroy(prevMesh);

            CreateNewMesh(this.transform.position, overlapWs[overlapWs.Count - 1], false, overlapPts, overlapWs);
        }
        else
        {
            // 初回 or 書き込み終了後の新規 → 始端キャップ付き
            CreateNewMesh(this.transform.position, -1f, true);
        }

        // 書き込み開始
        writeCoroutine = StartCoroutine(WriteTrail());
    }

    void StopWriting()
    {
        if (writeCoroutine != null)
        {
            StopCoroutine(writeCoroutine);
            writeCoroutine = null;
        }

        if (currentMesh != null)
        {
            Destroy(currentMesh);
            currentMesh = null;
        }

        isWriting = false;
    }

    /// <summary>
    /// 新規メッシュセグメントを生成して currentMesh にセット。
    /// startWidth &lt; 0 の場合はプレハブのデフォルト幅を使用。
    /// isFirst=true なら始端キャップ付き、false なら省略（継続セグメント）。
    /// </summary>
    /// <summary>
    /// 新規メッシュセグメントを生成して currentMesh にセット。
    /// overlapPts が渡された場合、先にそれらを追加してスムージングの連続性を確保する。
    /// </summary>
    void CreateNewMesh(Vector3 startPos, float startWidth, bool isFirst,
                       List<Vector3> overlapPts = null, List<float> overlapWs = null)
    {
        var g = Instantiate(Mesh);
        g.transform.SetParent(null);
        g.transform.position = Vector3.zero;
        g.transform.rotation = Quaternion.identity;
        g.transform.localScale = Vector3.one;

        currentMesh = g.GetComponent<MeshMaker>();
        if (currentMesh != null)
        {
            currentMesh.trailMaterial = roboColor;
            currentMesh.useStartCap = isFirst;
            currentMesh.Initialize();
        }
        currentMesh.SetSortingOrder(globalSortingOrder++);
        MeshRenderer trailRenderer = g.GetComponent<MeshRenderer>();
        if (trailRenderer != null)
            trailRenderer.sortingLayerName = "RoboTrail";

        // オーバーラップ点がある場合はそれらを先に追加（スムージング連続性確保）
        if (overlapPts != null && overlapPts.Count > 0)
        {
            for (int i = 0; i < overlapPts.Count; i++)
                currentMesh.AddPoint(overlapPts[i], overlapWs[i]);
        }
        else
        {
            float w = startWidth < 0f ? currentMesh.width : startWidth;
            currentMesh.AddPoint(startPos, w);
        }

        createdTrails.Add(g);
    }

    IEnumerator WriteTrail()
    {
        if (currentMesh == null) yield break;

        isWriting = true;
        float startTime = Time.time;
        float initialWidth = currentMesh.width;
        float lastSegmentTime = Time.time;

        while (Time.time - startTime < writeDuration)
        {
            float elapsed = Time.time - startTime;
            float currentWidth = Mathf.Max(0f, initialWidth - widthReduction * elapsed);

            if (currentWidth <= 0.001f) break;

            Vector3 pos = this.transform.position;

            // 一定間隔で新しいメッシュセグメントに分割し sorting order を更新
            // → あとから描かれた部分ほど上に表示される
            if (Time.time - lastSegmentTime >= segmentInterval && currentMesh != null)
            {
                currentMesh.AddPoint(pos, currentWidth);

                // 前セグメント末尾をオーバーラップとして引き継ぎ
                // → 新セグメントのスムージングが前セグメントと連続になる
                List<Vector3> overlapPts;
                List<float> overlapWs;
                currentMesh.GetRecentPoints(4, out overlapPts, out overlapWs);

                // 前セグメントは確定 → MeshMaker を除去して軽量化
                Destroy(currentMesh);

                CreateNewMesh(pos, currentWidth, false, overlapPts, overlapWs);
                lastSegmentTime = Time.time;
            }
            else if (currentMesh != null)
            {
                currentMesh.AddPoint(pos, currentWidth);
            }

            yield return new WaitForSeconds(0.1f);
        }

        // 最後のセグメントも確定 → MeshMaker を除去
        if (currentMesh != null)
            Destroy(currentMesh);

        isWriting = false;
        writeCoroutine = null;
    }

    // 全軌跡を削除
    public void ResetAllTrails()
    {
        if (writeCoroutine != null)
        {
            StopCoroutine(writeCoroutine);
            writeCoroutine = null;
        }
        isWriting = false;

        foreach (var go in createdTrails)
        {
            if (go == null) continue;
            Destroy(go);
        }
        createdTrails.Clear();
        currentMesh = null;
    }

    /// <summary>
    /// 現在存在する全軌跡の MeshRenderer を返す（sortingOrder 取得用）
    /// </summary>
    public List<MeshRenderer> GetTrailRenderers()
    {
        var result = new List<MeshRenderer>();
        foreach (var go in createdTrails)
        {
            if (go == null) continue;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) result.Add(mr);
        }
        return result;
    }
}
