using UnityEditor;
using System.IO;
using System;
using UnityEngine;

public class UserDataExporter
{
    private const string SafeOutDir = "MiraiHeadAssetBundle";

    [MenuItem("MiRAI Head/Build for All Platforms")]
    static void Build()
    {
        try
        {
            if (!Directory.Exists(SafeOutDir))
                Directory.CreateDirectory(SafeOutDir);

            // 2. 必要なディレクトリを再作成
            string winPath = Path.Combine(SafeOutDir, "Windows");
            string andPath = Path.Combine(SafeOutDir, "Android");

            if(!Directory.Exists(winPath))
                Directory.CreateDirectory(winPath);
            if(!Directory.Exists (andPath))
                Directory.CreateDirectory(andPath);


            // 3. ビルド実行
            Debug.Log("Windowsビルド開始...");
            BuildPipeline.BuildAssetBundles(winPath,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);

            Debug.Log("Androidビルド開始...");
            BuildPipeline.BuildAssetBundles(andPath,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.Android);

            Debug.Log($"すべてのビルドが成功しました！ 出力先: {Path.GetFullPath(SafeOutDir)}");

            // Finder/エクスプローラーで開く
            EditorUtility.RevealInFinder(SafeOutDir);
        }
        catch (Exception e)
        {
            Debug.LogError($"ビルド失敗: {e.Message}\n{e.StackTrace}");
        }
    }
}