using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Blendshape/Proxy Name List", fileName = "ProxyNameList")]
public class ProxyNameList : ScriptableObject
{
    [Header("表情セット名")]
    // リストの宣言時に初期値を代入しておく
    public List<string> names = new List<string>
    {
        "blink",
        "blink_joy",
        "joy",
        "sorrow",
        "angry",
        "fear",
        "disgust",
        "surprised",
        "shy",
        "arousal",
        "tongue_out",
        "kiss",
        "french_kiss"
    };
}