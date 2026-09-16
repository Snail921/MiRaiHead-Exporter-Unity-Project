using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MiraiHeadOutfit : MonoBehaviour
{
    [System.Serializable]
    public struct Blendshape
    {
        public string name;
        public float value;
    }

    public MiraiHead miraiHead;
    [Tooltip(
    "UIには表示せず、他の表示中Outfitから非表示指定されていない場合に" +
    "自動表示します。")]
    public bool autoOn = false;
    public List<MiraiHeadOutfit> outfitsToHide;
    public string prompt;
    public List<Blendshape> blendshapes = new List<Blendshape>();
    public List<GameObject> associatedGameObjects = new List<GameObject>();
    private void Reset()
    {
        AutoAssignMiraiHead();
    }

    private void AutoAssignMiraiHead()
    {
        miraiHead = GetComponentInParent<MiraiHead>(true);

        if (miraiHead == null)
        {
            Debug.LogWarning(
                $"{name}: 親階層にMiraiHeadが見つかりません。",
                this);
        }
    }
}