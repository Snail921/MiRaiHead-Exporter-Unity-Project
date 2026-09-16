using UnityEngine;
using System.Collections.Generic;
public class MiraiHead : MonoBehaviour
{
    public string characterName;
    [SerializeField]
    Transform headBone;
    [SerializeField]
    Transform leftEyeBone;
    [SerializeField]
    Transform rightEyeBone;
    [SerializeField]
    SkinnedMeshRenderer headMesh;
    [Header("影を受けないレンダラーを登録")]
    [SerializeField]
    List<Renderer> dontReceiveShadowRenderers = new List<Renderer>();
    [Header("肌用マテリアルを登録")]
    [SerializeField]
    List<Material> skinMaterials = new List<Material>();
    [Header("SBV2 Settings")]
    public string voiceNameSBV2 = "";
    [Header("Irodori TTS Settings")]
    public string irodoriTtsSpeakerId = "";
    [Header("ElevenLabs Settings")]
    public string voiceId = "";
    [Header("キャラクター・プロンプト")]
    [TextArea(5, 15)]
    public string characterPrompts = "";
    [Header("個性")]
    [Tooltip("会話：0: 無口 / 0.5: 標準/ 1: お喋り")]
    [Range(0f, 1f)]
    public float spontaneousSpeechFrequency = 0.5f;
    [Tooltip("所作：0: ゆったり / 0.5: 標準/ 1: 俊敏")]
    [Range(0f, 1f)]
    public float motionTempo = 0.5f;
    [Tooltip("利き目：-1: 左目 / 0: 両利き / 1: 右目")]
    [Range(-1f, 1f)]
    public float chinTuckEyeBias = 0f;
    [Tooltip("感受性：0: イキにくい / 1: イキやすい")]
    [Range(0f, 1f)]
    public float orgasmEase = 0.5f;
    [SerializeField, HideInInspector]
    int personalitySettingsVersion = CurrentPersonalitySettingsVersion;

    const int CurrentPersonalitySettingsVersion = 3;
    [Header("独自のアニメーションを指定する場合は以下を設定")]
    public Animator animator;
    public RuntimeAnimatorController animatorController;

}
