using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class MiraiHeadAssetBundleImporterWindow : EditorWindow
{
    private const string DefaultSourceFolder = "Assets/AndroidAssetBundles";
    private const string DefaultOutputFolder = "Assets/ImportedMiRaiHeads";

    [SerializeField] private string bundlePath = "";
    [SerializeField] private string outputFolder = DefaultOutputFolder;
    [SerializeField] private bool assignAssetBundleName = true;

    [MenuItem("MiRAI Head/Import Android AssetBundle")]
    private static void Open()
    {
        var window = GetWindow<MiraiHeadAssetBundleImporterWindow>();
        window.titleContent = new GUIContent("MiRAI Head Importer");
        window.minSize = new Vector2(560f, 245f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Android AssetBundle Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Android用MiRAI Head AssetBundleを、編集可能なPrefab・Mesh・Material・Textureへ変換します。" +
            "TextureはPNGへ復元し、Shaderはこのプロジェクトの同名Shader（通常はURP/Lit）へ置き換えます。",
            MessageType.Info);

        EditorGUILayout.Space();
        DrawPathField("AssetBundle", ref bundlePath, BrowseBundle);
        DrawPathField("出力フォルダー", ref outputFolder, BrowseOutputFolder);
        assignAssetBundleName = EditorGUILayout.ToggleLeft(
            "生成Prefabへ元のAssetBundle名を設定する", assignAssetBundleName);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(bundlePath)))
        {
            if (GUILayout.Button("Import as Editable MiRAI Head", GUILayout.Height(36f)))
                Import();
        }
    }

    private static void DrawPathField(string label, ref string value, Action browse)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            value = EditorGUILayout.TextField(label, value);
            if (GUILayout.Button("Browse...", GUILayout.Width(90f))) browse();
        }
    }

    private void BrowseBundle()
    {
        string initial = ResolveInitialDirectory(bundlePath, DefaultSourceFolder);
        string selected = EditorUtility.OpenFilePanel("Select Android AssetBundle", initial, "");
        if (!string.IsNullOrEmpty(selected)) bundlePath = selected;
    }

    private void BrowseOutputFolder()
    {
        string initial = ResolveInitialDirectory(outputFolder, "Assets");
        string selected = EditorUtility.OpenFolderPanel("Select output folder", initial, "");
        if (string.IsNullOrEmpty(selected)) return;

        string assetPath = MiraiHeadAssetBundleImporter.ToAssetPath(selected);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("MiRAI Head Importer", "出力先はこのUnityプロジェクトのAssets内を選択してください。", "OK");
            return;
        }

        outputFolder = assetPath;
    }

    private void Import()
    {
        try
        {
            MiraiHeadImportResult result = MiraiHeadAssetBundleImporter.Import(
                bundlePath, outputFolder, assignAssetBundleName);

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);

            string message =
                $"インポートが完了しました。\n\n" +
                $"Prefab: {result.prefabPath}\n" +
                $"Mesh: {result.meshCount}\n" +
                $"Material: {result.materialCount}\n" +
                $"Texture: {result.textureCount}";
            if (result.warnings.Count > 0)
                message += $"\n\n警告: {result.warnings.Count}件（Consoleを確認してください）";

            EditorUtility.DisplayDialog("MiRAI Head Importer", message, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("MiRAI Head Importer", "インポートに失敗しました。\n\n" + ex.Message, "OK");
        }
    }

    private static string ResolveInitialDirectory(string value, string fallbackAssetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string candidate = value;
        if (string.IsNullOrWhiteSpace(candidate)) candidate = fallbackAssetPath;
        if (!Path.IsPathRooted(candidate)) candidate = Path.Combine(projectRoot, candidate);
        if (File.Exists(candidate)) candidate = Path.GetDirectoryName(candidate);
        return Directory.Exists(candidate) ? candidate : Application.dataPath;
    }
}

public sealed class MiraiHeadImportResult
{
    public string prefabPath;
    public int meshCount;
    public int materialCount;
    public int textureCount;
    internal int scriptableObjectCount;
    public readonly List<string> warnings = new List<string>();
}

public static class MiraiHeadAssetBundleImporter
{
    private const string DefaultOutputFolder = "Assets/ImportedMiRaiHeads";
    private const string UrpLitPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";

    private sealed class TextureUsage
    {
        public bool color;
        public bool data;
        public bool normal;

        public bool IsLinear => normal || (data && !color);
    }

    public static MiraiHeadImportResult Import(string bundlePath, string outputRoot = DefaultOutputFolder,
        bool assignAssetBundleName = true)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
            throw new ArgumentException("AssetBundleを選択してください。", nameof(bundlePath));

        string absoluteBundlePath = Path.IsPathRooted(bundlePath)
            ? Path.GetFullPath(bundlePath)
            : Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, bundlePath));
        if (!File.Exists(absoluteBundlePath))
            throw new FileNotFoundException("AssetBundleが見つかりません。", absoluteBundlePath);

        outputRoot = NormalizeAssetFolder(outputRoot);
        Dictionary<string, Shader> localShaders = CollectLocalShaders();
        Dictionary<Type, MonoScript> localScripts = CollectLocalScripts();
        var result = new MiraiHeadImportResult();
        string importFolder = null;
        AssetBundle bundle = null;
        GameObject instance = null;

        try
        {
            EditorUtility.DisplayProgressBar("MiRAI Head Importer", "AssetBundleを読み込んでいます...", 0.02f);
            bundle = AssetBundle.LoadFromFile(absoluteBundlePath);
            if (bundle == null)
                throw new InvalidOperationException(
                    "AssetBundleを読み込めませんでした。別のUnityバージョン、破損、または既にロード済みの可能性があります。");

            GameObject sourcePrefab = bundle.LoadAllAssets<GameObject>()
                .FirstOrDefault(go => go != null && go.GetComponentInChildren<MiraiHead>(true) != null);
            if (sourcePrefab == null)
                throw new InvalidOperationException("MiraiHeadコンポーネントを持つPrefabがAssetBundle内に見つかりません。");

            string bundleFileName = Path.GetFileName(absoluteBundlePath);
            string characterFolderName = SanitizeFileName(
                string.IsNullOrWhiteSpace(sourcePrefab.name) ? bundleFileName : sourcePrefab.name);
            importFolder = CreateUniqueFolder(outputRoot, characterFolderName);

            string meshFolder = EnsureFolder(importFolder + "/Meshes");
            string materialFolder = EnsureFolder(importFolder + "/Materials");
            string textureFolder = EnsureFolder(importFolder + "/Textures");
            string dataFolder = EnsureFolder(importFolder + "/Data");

            UnityEngine.Object[] dependencies = EditorUtility.CollectDependencies(
                    new UnityEngine.Object[] { sourcePrefab })
                .Where(o => o != null)
                .Distinct()
                .ToArray();
            var remap = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var clonedDataAssets = new List<UnityEngine.Object>();
            Dictionary<Texture2D, TextureUsage> textureUsage = CollectTextureUsage(dependencies.OfType<Material>());

            Mesh[] meshes = dependencies.OfType<Mesh>().Distinct().ToArray();
            for (int i = 0; i < meshes.Length; i++)
            {
                EditorUtility.DisplayProgressBar("MiRAI Head Importer", $"Meshを保存しています ({i + 1}/{meshes.Length})", Progress(i, meshes.Length, 0.08f, 0.25f));
                Mesh clone = UnityEngine.Object.Instantiate(meshes[i]);
                clone.name = meshes[i].name;
                string path = UniqueAssetPath(meshFolder, clone.name, ".asset");
                AssetDatabase.CreateAsset(clone, path);
                remap[meshes[i]] = clone;
                result.meshCount++;
            }

            Texture2D[] textures = dependencies.OfType<Texture2D>().Distinct().ToArray();
            for (int i = 0; i < textures.Length; i++)
            {
                EditorUtility.DisplayProgressBar("MiRAI Head Importer", $"Textureを復元しています ({i + 1}/{textures.Length})", Progress(i, textures.Length, 0.25f, 0.57f));
                TextureUsage usage;
                if (!textureUsage.TryGetValue(textures[i], out usage)) usage = new TextureUsage { color = true };
                Texture2D imported = ExportTexture(textures[i], usage, textureFolder);
                remap[textures[i]] = imported;
                result.textureCount++;
            }

            CloneDataAssets(dependencies, dataFolder, remap, clonedDataAssets, result);

            Material[] materials = dependencies.OfType<Material>().Distinct().ToArray();
            for (int i = 0; i < materials.Length; i++)
            {
                EditorUtility.DisplayProgressBar("MiRAI Head Importer", $"Materialを再構築しています ({i + 1}/{materials.Length})", Progress(i, materials.Length, 0.62f, 0.76f));
                Material clone = CloneMaterial(materials[i], localShaders, remap, result);
                string path = UniqueAssetPath(materialFolder, clone.name, ".mat");
                AssetDatabase.CreateAsset(clone, path);
                remap[materials[i]] = clone;
                result.materialCount++;
            }

            foreach (UnityEngine.Object dataAsset in clonedDataAssets)
                RemapSerializedReferences(dataAsset, remap, localShaders, localScripts, result);

            EditorUtility.DisplayProgressBar("MiRAI Head Importer", "Prefabを再構築しています...", 0.80f);
            instance = UnityEngine.Object.Instantiate(sourcePrefab);
            instance.name = sourcePrefab.name;
            foreach (Component component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component != null)
                    RemapSerializedReferences(component, remap, localShaders, localScripts, result);
            }

            string prefabPath = UniqueAssetPath(importFolder, sourcePrefab.name, ".prefab");
            bool prefabSaved;
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out prefabSaved);
            if (!prefabSaved)
                throw new InvalidOperationException("再構築したPrefabを保存できませんでした。");

            if (assignAssetBundleName)
            {
                AssetImporter importer = AssetImporter.GetAtPath(prefabPath);
                importer.assetBundleName = bundleFileName.ToLowerInvariant();
                importer.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            result.prefabPath = prefabPath;
        }
        catch
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            instance = null;
            if (bundle != null) bundle.Unload(true);
            bundle = null;
            if (!string.IsNullOrEmpty(importFolder) && AssetDatabase.IsValidFolder(importFolder))
                AssetDatabase.DeleteAsset(importFolder);
            AssetDatabase.Refresh();
            throw;
        }
        finally
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            if (bundle != null) bundle.Unload(true);
            EditorUtility.ClearProgressBar();
        }

        try
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateImportedPrefab(result);
        }
        catch
        {
            if (!string.IsNullOrEmpty(importFolder) && AssetDatabase.IsValidFolder(importFolder))
                AssetDatabase.DeleteAsset(importFolder);
            AssetDatabase.Refresh();
            throw;
        }

        foreach (string warning in result.warnings.Distinct())
            Debug.LogWarning("[MiRAI Head Importer] " + warning);
        Debug.Log($"[MiRAI Head Importer] Import completed: {result.prefabPath}");
        return result;
    }

    public static string ToAssetPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath)) return null;
        string projectRoot = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(absolutePath);
        if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)) return null;
        return fullPath.Substring(projectRoot.Length).Replace('\\', '/').TrimEnd('/');
    }

    private static Dictionary<string, Shader> CollectLocalShaders()
    {
        var shaders = new Dictionary<string, Shader>(StringComparer.Ordinal);
        Shader urpLit = AssetDatabase.LoadAssetAtPath<Shader>(UrpLitPath);
        if (urpLit != null) shaders[urpLit.name] = urpLit;

        foreach (string guid in AssetDatabase.FindAssets("t:Shader"))
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (shader != null && !string.IsNullOrEmpty(shader.name) && !shaders.ContainsKey(shader.name))
                shaders.Add(shader.name, shader);
        }
        return shaders;
    }

    private static Dictionary<Type, MonoScript> CollectLocalScripts()
    {
        var scripts = new Dictionary<Type, MonoScript>();
        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Type type = script != null ? script.GetClass() : null;
            if (type == null) continue;

            MonoScript current;
            if (!scripts.TryGetValue(type, out current) ||
                IsPreferredScriptPath(path, AssetDatabase.GetAssetPath(current)))
                scripts[type] = script;
        }
        return scripts;
    }

    private static bool IsPreferredScriptPath(string candidate, string current)
    {
        bool candidateIsAsset = candidate.StartsWith("Assets/", StringComparison.Ordinal);
        bool currentIsAsset = current.StartsWith("Assets/", StringComparison.Ordinal);
        return candidateIsAsset && !currentIsAsset;
    }

    private static Dictionary<Texture2D, TextureUsage> CollectTextureUsage(IEnumerable<Material> materials)
    {
        var result = new Dictionary<Texture2D, TextureUsage>();
        foreach (Material material in materials)
        {
            foreach (string property in material.GetTexturePropertyNames())
            {
                Texture2D texture = material.GetTexture(property) as Texture2D;
                if (texture == null) continue;
                TextureUsage usage;
                if (!result.TryGetValue(texture, out usage))
                {
                    usage = new TextureUsage();
                    result.Add(texture, usage);
                }

                string lower = property.ToLowerInvariant();
                if (lower.Contains("bump") || lower.Contains("normal")) usage.normal = true;
                else if (lower.Contains("metal") || lower.Contains("smooth") || lower.Contains("occlusion") ||
                         lower.Contains("mask") || lower.Contains("spec") || lower.Contains("parallax")) usage.data = true;
                else usage.color = true;
            }
        }
        return result;
    }

    private static Texture2D ExportTexture(Texture2D source, TextureUsage usage, string textureFolder)
    {
        bool linear = usage.IsLinear;
        byte[] png = ReadTextureAsPng(source, linear);
        string path = UniqueAssetPath(textureFolder, source.name, ".png");
        File.WriteAllBytes(ToAbsolutePath(path), png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("TextureImporterを取得できません: " + path);
        importer.textureType = usage.normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !linear;
        importer.mipmapEnabled = source.mipmapCount > 1;
        importer.wrapMode = source.wrapMode;
        importer.filterMode = source.filterMode;
        importer.anisoLevel = source.anisoLevel;
        // Alphaの有無は保持するが、透明境界の色を変更するalphaIsTransparency処理は行わない。
        importer.alphaIsTransparency = false;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();

        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (imported == null) throw new InvalidOperationException("復元したTextureを読み込めません: " + path);
        return imported;
    }

    private static byte[] ReadTextureAsPng(Texture2D source, bool linear)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = null;
        Texture2D readable = null;
        try
        {
            temporary = RenderTexture.GetTemporary(
                source.width, source.height, 0, RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, linear);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            readable.Apply(false, false);
            byte[] png = ImageConversion.EncodeToPNG(readable);
            if (png == null || png.Length == 0)
                throw new InvalidOperationException("PNGエンコードに失敗しました: " + source.name);
            return png;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Textureの復元に失敗しました: {source.name} ({source.format}, {source.width}x{source.height})。" +
                "このPCでAndroid Texture形式をGPU変換できない可能性があります。", ex);
        }
        finally
        {
            RenderTexture.active = previous;
            if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
        }
    }

    private static void CloneDataAssets(IEnumerable<UnityEngine.Object> dependencies, string dataFolder,
        IDictionary<UnityEngine.Object, UnityEngine.Object> remap, IList<UnityEngine.Object> clones,
        MiraiHeadImportResult result)
    {
        foreach (UnityEngine.Object source in dependencies)
        {
            if (source == null || remap.ContainsKey(source) || source is GameObject || source is Component ||
                source is Material || source is Texture || source is Mesh || source is Shader || source is MonoScript)
                continue;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(source))) continue;

            bool supported = source is ScriptableObject || source is AnimationClip || source is Avatar ||
                             source is RuntimeAnimatorController || source is PhysicsMaterial ||
                             source is PhysicsMaterial2D;
            if (!supported)
            {
                AddWarning(result, $"未対応の依存アセットをスキップしました: {source.name} ({source.GetType().Name})");
                continue;
            }

            try
            {
                UnityEngine.Object clone;
                if (source is ScriptableObject)
                {
                    // InstantiateするとAssetBundle内MonoScriptへのm_Script参照まで複製される。
                    // ローカル型から新規作成し、m_Script以外のシリアライズ値だけをコピーする。
                    clone = ScriptableObject.CreateInstance(source.GetType());
                    CopySerializedPropertiesExceptScript(source, clone);
                }
                else
                {
                    clone = UnityEngine.Object.Instantiate(source);
                }
                clone.name = source.name;
                string path = UniqueAssetPath(dataFolder, clone.name, ".asset");
                AssetDatabase.CreateAsset(clone, path);
                remap[source] = clone;
                clones.Add(clone);
                if (source is ScriptableObject) result.scriptableObjectCount++;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"依存アセットを複製できませんでした: {source.name} ({source.GetType().Name})", ex);
            }
        }
    }

    private static void CopySerializedPropertiesExceptScript(UnityEngine.Object source,
        UnityEngine.Object destination)
    {
        var sourceSerialized = new SerializedObject(source);
        var destinationSerialized = new SerializedObject(destination);
        SerializedProperty property = sourceSerialized.GetIterator();
        bool enterChildren = true;
        while (property.Next(enterChildren))
        {
            enterChildren = false;
            if (property.propertyPath == "m_Script") continue;
            destinationSerialized.CopyFromSerializedProperty(property);
        }
        destinationSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material CloneMaterial(Material source, IDictionary<string, Shader> localShaders,
        IDictionary<UnityEngine.Object, UnityEngine.Object> remap, MiraiHeadImportResult result)
    {
        string shaderName = source.shader != null ? source.shader.name : "";
        Shader shader;
        if (!localShaders.TryGetValue(shaderName, out shader) || shader == null)
        {
            localShaders.TryGetValue("Universal Render Pipeline/Lit", out shader);
            if (shader == null)
                throw new InvalidOperationException($"置換先Shaderが見つかりません: {shaderName}");
            AddWarning(result, $"Shader '{shaderName}' がプロジェクトにないためURP/Litへ置き換えました: {source.name}");
        }

        var clone = new Material(shader) { name = source.name };
        clone.CopyPropertiesFromMaterial(source);
        clone.shader = shader;
        clone.renderQueue = source.renderQueue;
        clone.enableInstancing = source.enableInstancing;
        clone.doubleSidedGI = source.doubleSidedGI;
        clone.globalIlluminationFlags = source.globalIlluminationFlags;
        clone.shaderKeywords = source.shaderKeywords;

        foreach (string property in source.GetTexturePropertyNames())
        {
            Texture texture = source.GetTexture(property);
            UnityEngine.Object replacement;
            if (texture != null && remap.TryGetValue(texture, out replacement) && replacement is Texture)
            {
                clone.SetTexture(property, (Texture)replacement);
                clone.SetTextureScale(property, source.GetTextureScale(property));
                clone.SetTextureOffset(property, source.GetTextureOffset(property));
            }
        }
        return clone;
    }

    private static void RemapSerializedReferences(UnityEngine.Object target,
        IDictionary<UnityEngine.Object, UnityEngine.Object> remap,
        IDictionary<string, Shader> localShaders, IDictionary<Type, MonoScript> localScripts,
        MiraiHeadImportResult result)
    {
        if (target == null) return;
        string targetName = target.name;
        string targetTypeName = target.GetType().Name;
        try
        {
            var serialized = new SerializedObject(target);
            SetLocalScriptReference(serialized, target.GetType(), localScripts);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                UnityEngine.Object sourceReference;
                try
                {
                    sourceReference = property.objectReferenceValue;
                }
                catch (NullReferenceException)
                {
                    // 一部のネイティブコンポーネントはnull要素の取得時に例外を投げる。
                    // その要素だけを無視し、後続プロパティの再マップを継続する。
                    continue;
                }
                if (sourceReference == null) continue;

                UnityEngine.Object replacement;
                if (remap.TryGetValue(sourceReference, out replacement))
                {
                    property.objectReferenceValue = replacement;
                    continue;
                }

                Shader sourceShader = sourceReference as Shader;
                Shader localShader;
                if (sourceShader != null && localShaders.TryGetValue(sourceShader.name, out localShader))
                    property.objectReferenceValue = localShader;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // m_Scriptの差し替え時はUnityがMonoBehaviourを再生成し、元のtargetを破棄することがある。
            // Apply済みなので、ここで破棄済みtargetへSetDirtyを呼ぶ必要はない。
        }
        catch (Exception ex)
        {
            AddWarning(result,
                $"参照の再マップを一部適用できませんでした: {targetName} ({targetTypeName}): {ex.Message}");
        }
    }

    private static void SetLocalScriptReference(SerializedObject serialized, Type targetType,
        IDictionary<Type, MonoScript> localScripts)
    {
        SerializedProperty scriptProperty = serialized.FindProperty("m_Script");
        if (scriptProperty == null) return;
        MonoScript localScript;
        if (!localScripts.TryGetValue(targetType, out localScript) || localScript == null)
            throw new InvalidOperationException("ローカルScriptが見つかりません: " + targetType.FullName);
        if (scriptProperty.objectReferenceValue == localScript) return;

        // 他のAssetBundle参照も同じSerializedObject上で置換してから一度だけApplyする。
        scriptProperty.objectReferenceValue = localScript;
    }

    private static void ValidateImportedPrefab(MiraiHeadImportResult result)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabPath);
        if (prefab == null) throw new InvalidOperationException("保存後のPrefabを読み込めません: " + result.prefabPath);
        if (prefab.GetComponentInChildren<MiraiHead>(true) == null)
            throw new InvalidOperationException("保存後のPrefabからMiraiHeadコンポーネントが失われています。");

        int missingScripts = prefab.GetComponentsInChildren<Transform>(true)
            .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
        if (missingScripts > 0)
            throw new InvalidOperationException($"生成PrefabにMissing Scriptが{missingScripts}件あります。");

        string absolutePrefabPath = ToAbsolutePath(result.prefabPath);
        int bundleReferenceCount = File.ReadLines(absolutePrefabPath).Count(line =>
            line.Contains("guid: 00000000000000000000000000000000") &&
            !line.Contains("fileID: 0,"));
        if (bundleReferenceCount > 0)
            throw new InvalidOperationException(
                $"生成PrefabにAssetBundle内への無効な参照が{bundleReferenceCount}件残っています。");

        string importFolder = Path.GetDirectoryName(result.prefabPath)?.Replace('\\', '/');
        string dataFolder = importFolder + "/Data";
        if (AssetDatabase.IsValidFolder(dataFolder))
        {
            int importedScriptableObjectCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { dataFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) == null)
                    throw new InvalidOperationException("保存後のDataアセットからScriptが失われています: " + path);
                importedScriptableObjectCount++;
            }
            if (importedScriptableObjectCount != result.scriptableObjectCount)
                throw new InvalidOperationException(
                    $"保存後のDataアセットからScriptが失われています。" +
                    $" expected={result.scriptableObjectCount}, actual={importedScriptableObjectCount}");
        }

        foreach (UnityEngine.Object dependency in EditorUtility.CollectDependencies(new UnityEngine.Object[] { prefab }))
        {
            if (dependency == null || dependency is GameObject || dependency is Component) continue;
            string path = AssetDatabase.GetAssetPath(dependency);
            if (string.IsNullOrEmpty(path) && !(dependency is MonoScript))
                AddWarning(result, $"Project内へ保存されていない参照が残っています: {dependency.name} ({dependency.GetType().Name})");
        }
    }

    private static string NormalizeAssetFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) path = DefaultOutputFolder;
        if (Path.IsPathRooted(path)) path = ToAssetPath(path);
        path = (path ?? "").Replace('\\', '/').TrimEnd('/');
        if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException("出力先はAssets内を指定してください。", nameof(path));
        return EnsureFolder(path);
    }

    private static string EnsureFolder(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetPath)) return assetPath;
        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string name = Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            throw new InvalidOperationException("フォルダーを作成できません: " + assetPath);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
        return assetPath;
    }

    private static string CreateUniqueFolder(string parent, string preferredName)
    {
        parent = EnsureFolder(parent);
        string baseName = string.IsNullOrWhiteSpace(preferredName) ? "ImportedMiRaiHead" : preferredName;
        string candidate = parent + "/" + baseName;
        int suffix = 1;
        while (AssetDatabase.IsValidFolder(candidate)) candidate = parent + "/" + baseName + "_" + suffix++;
        string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(candidate));
        if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("インポート先フォルダーを作成できません: " + candidate);
        return candidate;
    }

    private static string UniqueAssetPath(string folder, string name, string extension)
    {
        string safeName = SanitizeFileName(name);
        if (string.IsNullOrEmpty(safeName)) safeName = "Asset";
        return AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeName + extension);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        char[] invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).Distinct().ToArray();
        foreach (char character in invalid) value = value.Replace(character, '_');
        return value.Trim().TrimEnd('.');
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static float Progress(int index, int count, float from, float to)
    {
        if (count <= 0) return to;
        return Mathf.Lerp(from, to, (index + 1f) / count);
    }

    private static void AddWarning(MiraiHeadImportResult result, string message)
    {
        if (!result.warnings.Contains(message)) result.warnings.Add(message);
    }
}
