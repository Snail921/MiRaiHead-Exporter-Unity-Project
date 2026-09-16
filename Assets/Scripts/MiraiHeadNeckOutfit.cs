using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 首輪、チョーカー、ネクタイなど、共有NeckBoneへ追従するOutfit。
/// AssetBundle側ではRendererを持つGameObjectへ追加し、neckBoneとTriggerを指定する。
/// </summary>
public class MiraiHeadNeckOutfit : MiraiHeadOutfit
{
    [Tooltip("このOutfitを移動させる共有NeckBone")]
    public Transform neckBone;

    [Tooltip("NeckBone自身または子階層にあるGrab判定用Collider")]
    public List<Collider> triggers = new();
}
