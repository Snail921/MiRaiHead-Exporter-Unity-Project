using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BlendshapeMapping : BaseBlendshapeManager
{
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public List<BlendshapeProxy> proxies = new List<BlendshapeProxy>();
    public ProxyNameList proxyNames;
    public EmotionTagDefinition emotionTagDefinitionOverride;

    // ----------------------------------------------------------------------
    //  現在のメッシュ状態 → proxies へ書き出す
    // ----------------------------------------------------------------------
    [ContextMenu("Create Blendshape Proxies From Current Weights")]
    public void Create(string expressionName)      // ← 引数をそのまま Proxy 名に転用
    {
        if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null)
        {
            Debug.LogWarning("SkinnedMeshRenderer が設定されていません");
            return;
        }

        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        int count = mesh.blendShapeCount;

        // まとめ役となる Proxy を 1 個だけ用意
        var proxy = new BlendshapeProxy
        {
            name = string.IsNullOrEmpty(expressionName) ? "Expression" : expressionName,
            blendshapes = new List<BlendshapeValue>()
        };

        // 0 でないブレンドシェイプをすべて詰め込む
        for (int i = 0; i < count; i++)
        {
            float w = skinnedMeshRenderer.GetBlendShapeWeight(i);
            if (Mathf.Approximately(w, 0f)) continue;      // 0 はスキップ

            string bsName = mesh.GetBlendShapeName(i);

            proxy.blendshapes.Add(new BlendshapeValue(i, bsName, w));
        }

        proxies.Add(proxy);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }


    // ==================================================================
    //  ✨  NEW FUNCTIONALITY  ✨
    // ==================================================================

    /// <summary>
    /// Sets every blend‑shape weight on <see cref="skinnedMeshRenderer"/> to 0.
    /// </summary>
    public void ResetAllBlendshapes()
    {
        if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null) return;

        int count = skinnedMeshRenderer.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(i, 0f);
        }
    }

    /// <summary>
    /// Overwrites an existing proxy ( <paramref name="index"/> ) with the *current* mesh state.
    /// Only non‑zero weights are stored. The proxy's <c>name</c> field remains untouched.
    /// </summary>
    public void Capture(int index)
    {
        if (index < 0 || index >= proxies.Count)
        {
            Debug.LogWarning($"Capture failed: index {index} is out of range.");
            return;
        }
        if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null)
        {
            Debug.LogWarning("SkinnedMeshRenderer が設定されていません");
            return;
        }

        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        int count = mesh.blendShapeCount;

        BlendshapeProxy proxy = proxies[index];
        proxy.blendshapes ??= new List<BlendshapeValue>();
        proxy.blendshapes.Clear();

        for (int i = 0; i < count; i++)
        {
            float w = skinnedMeshRenderer.GetBlendShapeWeight(i);
            if (Mathf.Approximately(w, 0f)) continue; // skip zeros

            string bsName = mesh.GetBlendShapeName(i);
            proxy.blendshapes.Add(new BlendshapeValue(i, bsName, w));
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
        Debug.Log($"Proxy \"{proxy.name}\" captured from current mesh state.");
    }

    /// <summary>
    /// Applies the stored values in proxy[ <paramref name="index"/> ] to the mesh for preview.
    /// The match is performed *by name*, not by stored index, allowing mesh re‑ordering.
    /// </summary>
    public void Preview(int index)
    {
        if (index < 0 || index >= proxies.Count)
        {
            Debug.LogWarning($"Preview failed: index {index} is out of range.");
            return;
        }
        if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null)
        {
            Debug.LogWarning("SkinnedMeshRenderer が設定されていません");
            return;
        }

        // 1) Reset everything to 0 first (prevents additive influence)
        ResetAllBlendshapes();

        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        int blendCount = mesh.blendShapeCount;
        var proxy = proxies[index];

        // Build a quick lookup: blendshape name ➔ index on the *current* mesh
        var nameToIdx = new Dictionary<string, int>(blendCount);
        for (int i = 0; i < blendCount; i++)
        {
            nameToIdx[mesh.GetBlendShapeName(i)] = i;
        }

        // 2) Apply weights stored in the proxy
        foreach (var bs in proxy.blendshapes)
        {
            if (nameToIdx.TryGetValue(bs.blendshapeName, out int meshIdx))
            {
                skinnedMeshRenderer.SetBlendShapeWeight(meshIdx, bs.value);
            }
            else
            {
                Debug.LogWarning($"Blendshape \"{bs.blendshapeName}\" not found on the current mesh; skipped.");
            }
        }
    }
}
