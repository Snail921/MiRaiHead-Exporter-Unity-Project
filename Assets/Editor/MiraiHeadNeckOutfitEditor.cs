using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MiraiHeadNeckOutfit))]
public class MiraiHeadNeckOutfitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var outfit = (MiraiHeadNeckOutfit)target;
        Transform previousNeckBone = outfit.neckBone;

        DrawDefaultInspector();

        if (outfit.neckBone != previousNeckBone)
            CollectTriggerColliders(outfit);

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Trigger Colliders"))
            CollectTriggerColliders(outfit);

        DrawInvalidTriggerWarning(outfit);
    }

    static void CollectTriggerColliders(MiraiHeadNeckOutfit outfit)
    {
        Undo.RecordObject(outfit, "Collect Neck Outfit Trigger Colliders");
        outfit.triggers.Clear();

        if (outfit.neckBone != null)
        {
            outfit.neckBone.GetComponentsInChildren(
                true,
                outfit.triggers);
        }

        EditorUtility.SetDirty(outfit);
        PrefabUtility.RecordPrefabInstancePropertyModifications(outfit);
    }

    static void DrawInvalidTriggerWarning(MiraiHeadNeckOutfit outfit)
    {
        if (outfit.neckBone == null || outfit.triggers == null)
            return;

        foreach (Collider collider in outfit.triggers)
        {
            if (collider == null)
                continue;

            if (collider.transform != outfit.neckBone &&
                !collider.transform.IsChildOf(outfit.neckBone))
            {
                EditorGUILayout.HelpBox(
                    $"Trigger '{collider.name}'はNeck Boneの配下ではないため、" +
                    "ランタイムでは使用されません。",
                    MessageType.Warning);
            }
        }
    }
}
