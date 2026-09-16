using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Blendshape/EmotionTagDefinition", fileName = "EmotionTagDefinition")]
public class EmotionTagDefinition : ScriptableObject
{
    public ProxyNameList proxyNameList;
    [Header("感情セット名")]
    public List<string> emotionTags = new List<string>
    {
        "joy",
        "sorrow",
        "fear",
        "love",
        "angry",
        "disgust",
        "surprised",
        "shy"
    };

    public void Copy()
    {
        if (proxyNameList == null)
        {
            Debug.LogError("Please assign ProxyNameList scriptable object to copy from.");
            return;
        }
        foreach (string proxyName in proxyNameList.names)
        {
            emotionTags.Add(proxyName);
        }
    }

    public void Clear()
    {
        emotionTags.Clear();
    }
}