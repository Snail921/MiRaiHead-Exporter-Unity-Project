using System.Collections.Generic;
using UnityEngine;

/// <summary>AssetBundleのインポート時に見つからなかったスクリプトの記録。</summary>
[AddComponentMenu("")]
public sealed class MissingScript : MonoBehaviour
{
    [Tooltip("元のスクリプト名。Unityから取得できなかった場合は取得不可と記録します。")]
    public List<string> info = new List<string>();
}
