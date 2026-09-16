using UnityEditor;
using UnityEngine;
using System.IO;
using System;

[CustomEditor(typeof(MiraiHead))]
public class MiraiHeadEditor : Editor
{
    private const string SafeOutDir = "MiraiHeadAssetBundle";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Export Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Export AssetBundle", GUILayout.Height(30)))
        {
            ExportSpecificBundle();
        }
    }

    private void ExportSpecificBundle()
    {
        MiraiHead script = (MiraiHead)target;
        string assetPath = AssetDatabase.GetAssetPath(script.gameObject);

        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("このオブジェクトはプレハブではありません。プロジェクト内のプレハブを選択してください。");
            return;
        }

        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        string bundleName = importer.assetBundleName;

        if (string.IsNullOrEmpty(bundleName))
        {
            Debug.LogError($"{script.name} にAssetBundle名が設定されていません！インスペクター下部で設定してください。");
            return;
        }

        // 修正ポイント：assetPathを直接渡す
        BuildBundle(bundleName, assetPath);
    }

    private void BuildBundle(string bundleName, string assetPath)
    {
        try
        {
            string winPath = Path.Combine(SafeOutDir, "Windows");
            string andPath = Path.Combine(SafeOutDir, "Android");

            if (!Directory.Exists(winPath)) Directory.CreateDirectory(winPath);
            if (!Directory.Exists(andPath)) Directory.CreateDirectory(andPath);

            AssetBundleBuild[] buildMap = new AssetBundleBuild[1];
            buildMap[0].assetBundleName = bundleName;

            // 修正箇所：UnityのDBに頼らず、今取得したパスをそのまま配列に入れる
            buildMap[0].assetNames = new string[] { assetPath };

            BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle;

            Debug.Log($"[{bundleName}] ビルド対象パス: {assetPath}");

            // Windows
            var winManifest = BuildPipeline.BuildAssetBundles(winPath, buildMap, options, BuildTarget.StandaloneWindows64);
            if (winManifest == null) Debug.LogWarning("Windowsビルドで何も生成されませんでした。");

            // Android
            var andManifest = BuildPipeline.BuildAssetBundles(andPath, buildMap, options, BuildTarget.Android);
            if (andManifest == null) Debug.LogWarning("Androidビルドで何も生成されませんでした。");

            Debug.Log($"エクスポート完了: {bundleName}\n出力先: {Path.GetFullPath(SafeOutDir)}");
            EditorUtility.RevealInFinder(SafeOutDir);
        }
        catch (Exception e)
        {
            Debug.LogError($"ビルド失敗: {e.Message}");
        }
    }
}
//using UnityEditor;
//using UnityEngine;
//using System.IO;
//using System;

//[CustomEditor(typeof(MiraiHead))]
//public class MiraiHeadEditor : Editor
//{
//    private const string SafeOutDir = "MiraiHeadAssetBundle";

//    public override void OnInspectorGUI()
//    {
//        // 元のインスペクターを表示（public変数などを出す）
//        DrawDefaultInspector();

//        EditorGUILayout.Space();
//        EditorGUILayout.LabelField("Export Tools", EditorStyles.boldLabel);

//        // Exportボタンを描画
//        if (GUILayout.Button("Export AssetBundle", GUILayout.Height(30)))
//        {
//            ExportSpecificBundle();
//        }
//    }

//    private void ExportSpecificBundle()
//    {
//        // 1. 対象のプレハブ（アセット）のパスを取得
//        MiraiHead script = (MiraiHead)target;
//        string assetPath = AssetDatabase.GetAssetPath(script.gameObject);

//        if (string.IsNullOrEmpty(assetPath))
//        {
//            Debug.LogError("このオブジェクトはプレハブではありません。プロジェクト内のプレハブを選択してください。");
//            return;
//        }

//        // 2. アセットインポーターからAssetBundle名を取得
//        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
//        string bundleName = importer.assetBundleName;

//        if (string.IsNullOrEmpty(bundleName))
//        {
//            Debug.LogError($"{script.name} にAssetBundle名が設定されていません！インスペクター下部で設定してください。");
//            return;
//        }

//        // 3. ビルド実行
//        BuildBundle(bundleName);
//    }

//    private void BuildBundle(string bundleName)
//    {
//        try
//        {
//            // ディレクトリ準備
//            string winPath = Path.Combine(SafeOutDir, "Windows");
//            string andPath = Path.Combine(SafeOutDir, "Android");

//            if (!Directory.Exists(winPath)) Directory.CreateDirectory(winPath);
//            if (!Directory.Exists(andPath)) Directory.CreateDirectory(andPath);

//            // ビルドマップ作成（特定の1つだけを対象にする）
//            AssetBundleBuild[] buildMap = new AssetBundleBuild[1];
//            buildMap[0].assetBundleName = bundleName;
//            buildMap[0].assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);

//            BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle;

//            // Windowsビルド
//            Debug.Log($"[{bundleName}] Windowsビルド開始...");
//            BuildPipeline.BuildAssetBundles(winPath, buildMap, options, BuildTarget.StandaloneWindows64);

//            // Androidビルド
//            Debug.Log($"[{bundleName}] Androidビルド開始...");
//            BuildPipeline.BuildAssetBundles(andPath, buildMap, options, BuildTarget.Android);

//            Debug.Log($"エクスポート完了: {bundleName}\n出力先: {Path.GetFullPath(SafeOutDir)}");
//            EditorUtility.RevealInFinder(SafeOutDir);
//        }
//        catch (Exception e)
//        {
//            Debug.LogError($"ビルド失敗: {e.Message}");
//        }
//    }
//}