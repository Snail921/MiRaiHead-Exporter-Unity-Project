using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class BlendshapeSynchronizer : MonoBehaviour
{
    public SkinnedMeshRenderer referenceSkinnedmeshRenderer;
    SkinnedMeshRenderer selfRenderer;

    [System.Serializable]
    public class Pair
    {
        public string blendshapeName;
        public int indexInSelf;
        public int indexInReference;
    }

    [SerializeField]
    List<Pair> blendshapes = new List<Pair>();

    private float[] previousWeights;

    private void OnValidate()
    {
        selfRenderer = GetComponent<SkinnedMeshRenderer>();
        InitializeBlendshapeList();
    }

    private void Awake()
    {
        selfRenderer = GetComponent<SkinnedMeshRenderer>();
        InitializeBlendshapeList();
    }
    public void InitializeBlendshapeList()
    {
        blendshapes.Clear();

        if (referenceSkinnedmeshRenderer == null || selfRenderer == null)
            return;

        Mesh selfMesh = selfRenderer.sharedMesh;
        Mesh referenceMesh = referenceSkinnedmeshRenderer.sharedMesh;

        if (selfMesh == null || referenceMesh == null)
            return;

        int selfBlendShapeCount = selfMesh.blendShapeCount;

        for (int i = 0; i < selfBlendShapeCount; i++)
        {
            string selfBlendShapeName = selfMesh.GetBlendShapeName(i);
            int referenceIndex = FindBlendShapeIndex(referenceMesh, selfBlendShapeName);

            if (referenceIndex >= 0)
            {
                Pair pair = new Pair
                {
                    blendshapeName = selfBlendShapeName,
                    indexInSelf = i,
                    indexInReference = referenceIndex
                };
                blendshapes.Add(pair);
            }
        }

        // ← ココで previousWeightsも初期化しておく！
        previousWeights = new float[blendshapes.Count];
    }

    // 参考用のメッシュから名前でインデックスを探すヘルパー関数
    private int FindBlendShapeIndex(Mesh mesh, string blendShapeName)
    {
        int count = mesh.blendShapeCount;
        for (int i = 0; i < count; i++)
        {
            if (mesh.GetBlendShapeName(i) == blendShapeName)
            {
                return i;
            }
        }
        return -1; // 見つからなかった場合
    }

    void LateUpdate()
    {
        if (referenceSkinnedmeshRenderer == null || selfRenderer == null)
            return;

        for (int i = 0; i < blendshapes.Count; i++)
        {
            var pair = blendshapes[i];
            float value = referenceSkinnedmeshRenderer.GetBlendShapeWeight(pair.indexInReference);

            if (!Mathf.Approximately(previousWeights[i], value))
            {
                previousWeights[i] = value;
                selfRenderer.SetBlendShapeWeight(pair.indexInSelf, value);
            }
        }
    }
}
