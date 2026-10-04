using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BlendshapeIndexSyncReport
{
    public int mappingCount;
    public int valueCount;
    public int changedIndexCount;
    public readonly List<string> errors = new List<string>();

    public bool Succeeded => errors.Count == 0;

    public string GetSummary()
    {
        return $"BlendshapeMapping: {mappingCount}, values: {valueCount}, " +
               $"updated indices: {changedIndexCount}";
    }

    public string GetErrorMessage(int maxErrors = 12)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BlendShape名からインデックスを解決できませんでした。");

        int count = Mathf.Min(errors.Count, maxErrors);
        for (int i = 0; i < count; i++)
            builder.AppendLine($"- {errors[i]}");

        if (errors.Count > count)
            builder.AppendLine($"- ...ほか {errors.Count - count} 件");

        return builder.ToString().TrimEnd();
    }
}

public static class BlendshapeIndexSyncUtility
{
    private sealed class PendingUpdate
    {
        public BaseBlendshapeManager.BlendshapeValue value;
        public int resolvedIndex;
    }

    public static BlendshapeIndexSyncReport SynchronizeMapping(BlendshapeMapping mapping)
    {
        var report = new BlendshapeIndexSyncReport();
        SynchronizeMapping(mapping, report);

        if (report.Succeeded && report.changedIndexCount > 0)
            AssetDatabase.SaveAssets();

        return report;
    }

    public static bool SynchronizePrefabAtPath(
        string assetPath,
        out BlendshapeIndexSyncReport report)
    {
        report = new BlendshapeIndexSyncReport();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            report.errors.Add($"Prefabを読み込めません: {assetPath}");
            return false;
        }

        SynchronizeHierarchy(prefab, report);
        if (report.Succeeded && report.changedIndexCount > 0)
            AssetDatabase.SaveAssets();

        return report.Succeeded;
    }

    public static bool SynchronizeAllAssetBundlePrefabs(
        out BlendshapeIndexSyncReport report)
    {
        report = new BlendshapeIndexSyncReport();
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string bundleName in AssetDatabase.GetAllAssetBundleNames())
        {
            foreach (string assetPath in AssetDatabase.GetAssetPathsFromAssetBundle(bundleName))
            {
                if (!assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    !processedPaths.Add(assetPath))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    report.errors.Add($"Prefabを読み込めません: {assetPath}");
                    continue;
                }

                SynchronizeHierarchy(prefab, report);
            }
        }

        if (report.Succeeded && report.changedIndexCount > 0)
            AssetDatabase.SaveAssets();

        return report.Succeeded;
    }

    private static void SynchronizeHierarchy(
        GameObject root,
        BlendshapeIndexSyncReport report)
    {
        BlendshapeMapping[] mappings = root.GetComponentsInChildren<BlendshapeMapping>(true);
        foreach (BlendshapeMapping mapping in mappings)
            SynchronizeMapping(mapping, report);
    }

    private static void SynchronizeMapping(
        BlendshapeMapping mapping,
        BlendshapeIndexSyncReport report)
    {
        int errorCountBefore = report.errors.Count;
        report.mappingCount++;
        string context = GetContext(mapping);

        if (mapping == null)
        {
            report.errors.Add("Missing BlendshapeMapping component");
            return;
        }

        if (mapping.skinnedMeshRenderer == null ||
            mapping.skinnedMeshRenderer.sharedMesh == null)
        {
            report.errors.Add($"{context}: SkinnedMeshRendererまたはMeshが未設定です");
            return;
        }

        Mesh mesh = mapping.skinnedMeshRenderer.sharedMesh;
        var nameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicateNames = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            if (nameToIndex.ContainsKey(name))
                duplicateNames.Add(name);
            else
                nameToIndex.Add(name, i);
        }

        var updates = new List<PendingUpdate>();
        if (mapping.proxies != null)
        {
            for (int proxyIndex = 0; proxyIndex < mapping.proxies.Count; proxyIndex++)
            {
                BaseBlendshapeManager.BlendshapeProxy proxy = mapping.proxies[proxyIndex];
                if (proxy == null || proxy.blendshapes == null)
                    continue;

                string proxyName = string.IsNullOrEmpty(proxy.name)
                    ? $"Proxy #{proxyIndex}"
                    : proxy.name;

                for (int valueIndex = 0; valueIndex < proxy.blendshapes.Count; valueIndex++)
                {
                    BaseBlendshapeManager.BlendshapeValue value = proxy.blendshapes[valueIndex];
                    report.valueCount++;

                    if (value == null)
                    {
                        report.errors.Add($"{context} / {proxyName}: 空のBlendShape設定があります");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(value.blendshapeName))
                    {
                        report.errors.Add($"{context} / {proxyName}: BlendShape名が空です");
                        continue;
                    }

                    if (duplicateNames.Contains(value.blendshapeName))
                    {
                        report.errors.Add(
                            $"{context} / {proxyName}: Mesh内でBlendShape名 " +
                            $"'{value.blendshapeName}' が重複しています");
                        continue;
                    }

                    if (!nameToIndex.TryGetValue(value.blendshapeName, out int resolvedIndex))
                    {
                        report.errors.Add(
                            $"{context} / {proxyName}: BlendShape " +
                            $"'{value.blendshapeName}' が現在のMeshにありません");
                        continue;
                    }

                    if (value.index != resolvedIndex)
                    {
                        updates.Add(new PendingUpdate
                        {
                            value = value,
                            resolvedIndex = resolvedIndex
                        });
                    }
                }
            }
        }

        // エラーがある場合は、このMappingを部分的に変更しない。
        if (report.errors.Count > errorCountBefore)
            return;

        if (updates.Count == 0)
            return;

        Undo.RecordObject(mapping, "Sync BlendShape Indices From Names");
        foreach (PendingUpdate update in updates)
            update.value.index = update.resolvedIndex;

        report.changedIndexCount += updates.Count;
        EditorUtility.SetDirty(mapping);

        if (PrefabUtility.IsPartOfPrefabInstance(mapping))
            PrefabUtility.RecordPrefabInstancePropertyModifications(mapping);

        if (mapping.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(mapping.gameObject.scene);
    }

    private static string GetContext(BlendshapeMapping mapping)
    {
        if (mapping == null)
            return "BlendshapeMapping";

        string assetPath = AssetDatabase.GetAssetPath(mapping);
        string hierarchyPath = mapping.name;
        Transform current = mapping.transform.parent;
        while (current != null)
        {
            hierarchyPath = current.name + "/" + hierarchyPath;
            current = current.parent;
        }

        return string.IsNullOrEmpty(assetPath)
            ? hierarchyPath
            : $"{assetPath} ({hierarchyPath})";
    }
}
