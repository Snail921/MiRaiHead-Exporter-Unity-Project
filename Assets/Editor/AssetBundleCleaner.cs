using UnityEditor;
using UnityEngine;

public class AssetBundleCleaner
{
    [MenuItem("SDK/Force Clear All AssetBundle Names")]
    static void ClearNames()
    {
        // プロジェクト内のすべてのアセットバンドル名を取得
        string[] bundleNames = AssetDatabase.GetAllAssetBundleNames();

        foreach (var name in bundleNames)
        {
            // trueを指定して、未使用の名前も含めて強制削除
            AssetDatabase.RemoveAssetBundleName(name, true);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("すべてのアセットバンドル名を強制消去しました。");
    }
}