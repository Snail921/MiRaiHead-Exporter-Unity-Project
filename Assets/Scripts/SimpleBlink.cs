using UnityEngine;
using System.Collections;

public class SimpleBlink : MonoBehaviour
{
    [SerializeField]
    SkinnedMeshRenderer faceMesh;

    private int blinkIndex = -1;

    [Header("瞬き間隔（秒）")]
    public float intervalMin = 1.0f;
    public float intervalMax = 5.0f;

    [Header("連続ブリンク発生率")]
    [Range(0f, 1f)]
    public float doubleBlinkRate = 0.2f;

    [Header("瞬きの際に閉じるスピード(所要秒数)")]
    public float baseBlinkSpeedClose = 0.05f;

    [Header("瞬きの際に開くスピード(所要秒数)")]
    public float baseBlinkSpeedOpen = 0.1f;

    [Header("連続瞬き間の間隔（秒）")]
    public float doubleBlinkInterval = 0.1f;

    private bool isBlinking = false;

    void Start()
    {
        // ブレンドシェイプ名が"まばたき"、"瞬き"、または"blink"(小文字化して検索) のインデックスを取得
        if (faceMesh != null)
        {
            blinkIndex = FindBlinkBlendShapeIndex();

            if (blinkIndex >= 0)
            {
                Debug.Log($"瞬きブレンドシェイプを検出: インデックス {blinkIndex}, 名前 '{faceMesh.sharedMesh.GetBlendShapeName(blinkIndex)}'");

                // 初期値を0にリセット
                faceMesh.SetBlendShapeWeight(blinkIndex, 0f);

                // 瞬きコルーチンを開始
                StartCoroutine(BlinkRoutine());
            }
            else
            {
                Debug.LogWarning("瞬き用のブレンドシェイプが見つかりませんでした。'まばたき'、'瞬き'、または'blink'という名前のブレンドシェイプを確認してください。");
            }
        }
        else
        {
            Debug.LogError("SkinnedMeshRendererが設定されていません！");
        }
    }

    /// <summary>
    /// ブレンドシェイプから瞬き用のインデックスを検索
    /// </summary>
    private int FindBlinkBlendShapeIndex()
    {
        if (faceMesh == null || faceMesh.sharedMesh == null)
            return -1;

        int blendShapeCount = faceMesh.sharedMesh.blendShapeCount;

        for (int i = 0; i < blendShapeCount; i++)
        {
            string shapeName = faceMesh.sharedMesh.GetBlendShapeName(i).ToLower();

            // "まばたき"、"瞬き"、"blink" のいずれかを含むか確認
            if (shapeName.Contains("まばたき") ||
                shapeName.Contains("瞬き") ||
                shapeName.Contains("blink"))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 瞬きを定期的に実行するコルーチン
    /// </summary>
    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            // ランダムな間隔を待つ
            float waitTime = Random.Range(intervalMin, intervalMax);
            yield return new WaitForSeconds(waitTime);

            // 瞬きを実行
            yield return StartCoroutine(PerformBlink());

            // ダブルブリンクの判定
            if (Random.value < doubleBlinkRate)
            {
                yield return new WaitForSeconds(doubleBlinkInterval);
                yield return StartCoroutine(PerformBlink());
            }
        }
    }

    /// <summary>
    /// 1回の瞬きアニメーションを実行
    /// </summary>
    private IEnumerator PerformBlink()
    {
        if (blinkIndex < 0 || isBlinking)
            yield break;

        isBlinking = true;

        // 目を閉じる (0 → 100)
        float elapsedTime = 0f;
        while (elapsedTime < baseBlinkSpeedClose)
        {
            elapsedTime += Time.deltaTime;
            float weight = Mathf.Lerp(0f, 100f, elapsedTime / baseBlinkSpeedClose);
            faceMesh.SetBlendShapeWeight(blinkIndex, weight);
            yield return null;
        }
        faceMesh.SetBlendShapeWeight(blinkIndex, 100f);

        // 目を開く (100 → 0)
        elapsedTime = 0f;
        while (elapsedTime < baseBlinkSpeedOpen)
        {
            elapsedTime += Time.deltaTime;
            float weight = Mathf.Lerp(100f, 0f, elapsedTime / baseBlinkSpeedOpen);
            faceMesh.SetBlendShapeWeight(blinkIndex, weight);
            yield return null;
        }
        faceMesh.SetBlendShapeWeight(blinkIndex, 0f);

        isBlinking = false;
    }

    /// <summary>
    /// 外部から手動で瞬きをトリガー
    /// </summary>
    public void TriggerBlink()
    {
        if (!isBlinking && blinkIndex >= 0)
        {
            StartCoroutine(PerformBlink());
        }
    }

    /// <summary>
    /// 瞬きを一時停止/再開
    /// </summary>
    public void SetBlinkEnabled(bool enabled)
    {
        if (enabled)
        {
            if (!isBlinking)
                StartCoroutine(BlinkRoutine());
        }
        else
        {
            StopAllCoroutines();
            isBlinking = false;
            if (blinkIndex >= 0)
                faceMesh.SetBlendShapeWeight(blinkIndex, 0f);
        }
    }

    void OnDestroy()
    {
        // クリーンアップ
        StopAllCoroutines();
    }

#if UNITY_EDITOR
    // デバッグ用：ブレンドシェイプ一覧を表示
    [ContextMenu("Show All BlendShapes")]
    private void ShowAllBlendShapes()
    {
        if (faceMesh != null && faceMesh.sharedMesh != null)
        {
            int count = faceMesh.sharedMesh.blendShapeCount;
            Debug.Log($"=== ブレンドシェイプ一覧 (全{count}個) ===");
            for (int i = 0; i < count; i++)
            {
                string name = faceMesh.sharedMesh.GetBlendShapeName(i);
                Debug.Log($"[{i}] {name}");
            }
        }
    }

    // デバッグ用：手動瞬き
    [ContextMenu("Test Blink")]
    private void TestBlink()
    {
        if (Application.isPlaying)
        {
            TriggerBlink();
        }
    }
#endif
}