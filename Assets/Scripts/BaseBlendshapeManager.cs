using System.Collections.Generic;
using UnityEngine;

public class BaseBlendshapeManager : MonoBehaviour
{
    [System.Serializable]
    public class BlendshapeProxy
    {
        public string name;                          // 表情名（例: "Joy"）
        public List<BlendshapeValue> blendshapes;    // ブレンドシェイプのリスト
    }

    [System.Serializable]
    public class BlendshapeValue
    {
        public string blendshapeName; // デバッグ／表示用
        public int index;                               // ブレンドシェイプのインデックス
        public float value;                             // 重み (0‥100)

        /// <summary>インスペクタから追加するときに楽をする簡易コンストラクタ</summary>
        public BlendshapeValue(int idx, string name, float v)
        {
            index = idx;
            blendshapeName = name;
            value = v;
        }
    }
}
