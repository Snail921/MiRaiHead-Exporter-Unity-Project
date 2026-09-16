# MiRAI Head AssetBundle Template

FBXモデルからMiRAI Head用Prefabを作成し、Windows/Android用AssetBundleとして出力するためのUnityプロジェクトです。

基本的な用途は、モデルのセットアップ、Magica Clothによる揺れもの設定、MiRAI Head用コンポーネントの調整、Prefab化、AssetBundle出力までを行うことです。公開済みAssetBundleのインポーターは補助機能として収録しています。

## 必要環境

- Unity `6000.3.8f1`
- Universal Render Pipeline `17.3.0`
- Android向けにも出力する場合は、Unity HubのAndroid Build Support
- Magica ClothまたはMagica Cloth 2（別途購入が必要な有料Unityアセット）

Magica Cloth本体はこのリポジトリに含まれません。利用者自身が正規に入手したものをプロジェクトへインポートしてください。

## MiRAI Head AssetBundleの作成手順

### 1. FBXをインポートする

使用権限を持つFBX、マテリアル、テクスチャを`Assets`以下へインポートし、必要に応じてRig、Material、BlendShapeなどのImport Settingsを調整します。

### 2. シーンへ配置する

FBXから生成されたモデルをシーンへ配置し、Transform、マテリアル、ボーン構造、SkinnedMeshRendererなどを確認します。ヘッドとして不要なオブジェクトがあれば整理します。

### 3. 揺れものを設定する

正規に購入した次のどちらかをプロジェクトへインポートします。

- Magica Cloth
- Magica Cloth 2

髪、アクセサリーなどへ必要なコンポーネント、Collider、Constraintを設定し、Play Modeで挙動を確認します。このリポジトリでは両アセットをGit管理対象外にしているため、誤って有料アセット本体を公開しないようになっています。

### 4. MiraiHeadを設定する

ヘッドのルートGameObjectへ`MiraiHead`コンポーネントを追加し、少なくとも次の項目をモデルに合わせて設定します。

- Character Name
- Head Bone
- Left Eye Bone / Right Eye Bone
- Head Mesh
- 影を受けないRenderer
- 肌用Material
- 必要に応じて音声、キャラクタープロンプト、個性、Animatorの設定

### 5. BlendshapeMappingを設定する

表情を制御するGameObjectへ`BlendshapeMapping`コンポーネントを追加します。

1. `Skinned Mesh Renderer`へ顔のRendererを設定します。
2. 必要に応じて`Proxy Name List`を設定します。
3. 顔のBlendShapeを目的の表情へ調整します。
4. Inspectorの`Create`で表情Proxyを作成します。
5. `Preview`、`Capture`、`Reset Blendshapes`を使って各表情を確認・調整します。

### 6. SimpleBlinkを設定する

瞬きを制御するGameObjectへ`SimpleBlink`コンポーネントを追加します。

1. `Face Mesh`へ顔のSkinnedMeshRendererを設定します。
2. 瞬き間隔、連続瞬きの発生率、閉じる速度、開く速度などを調整します。
3. 対象Meshに「まばたき」「瞬き」または「blink」を含むBlendShapeがあることを確認します。

### 7. Prefab化する

設定済みのヘッドのルートGameObjectをProjectウィンドウへドラッグし、Prefabを作成します。Prefabを開いて、参照切れやMissing Scriptがないことを確認してください。

### 8. AssetBundle名を設定してエクスポートする

1. Projectウィンドウで作成したPrefabを選択します。
2. Inspector下部の`AssetBundle`へ一意のバンドル名を設定します。
3. `MiraiHead`コンポーネントのInspectorにある`Export AssetBundle`を押します。

出力先は次のとおりです。

```text
MiraiHeadAssetBundle/
├─ Windows/
└─ Android/
```

AssetBundle名が設定されたすべてのアセットを一括出力する場合は、Unityメニューの`MiRAI Head > Build for All Platforms`を使用します。

## 補助機能：公開済みAssetBundleを編集可能な形式へ変換する

この機能は新しいMiRAI Headを作るための主要手順ではありません。既存のAndroid用AssetBundleを編集可能なPrefab・Mesh・Material・Textureへ変換したい場合に使用します。

1. Android用AssetBundleを`Assets/AndroidAssetBundles`へ配置します。
2. Unityメニューから`MiRAI Head > Import Android AssetBundle`を開きます。
3. AssetBundleと出力先を指定し、`Import as Editable MiRAI Head`を押します。
4. 生成されたアセットを`Assets/ImportedMiRaiHeads`で確認します。

インポーターはTextureをPNGへ復元し、利用できないShaderを原則として`URP/Lit`へ置き換えます。元のAssetBundleと同じUnityバージョンでの利用を推奨します。

## 公開・再配布時の注意

- 自分が利用・改変・再配布する権利を持つモデルと素材だけを使用してください。
- Magica ClothおよびMagica Cloth 2の本体をリポジトリや配布物へ含めないでください。
- AssetBundleを展開できることは、その内容を再配布できることを意味しません。
- `Assets/MagicaCloth`、`Assets/MagicaCloth2`、`Assets/AndroidAssetBundles`、`Assets/ImportedMiRaiHeads`、`MiraiHeadAssetBundle`はGit管理対象外です。
- 公開前に`git status`を確認し、Unity生成物、個人アセット、有料アセットが含まれていないことを確認してください。

## ライセンス

公開前に、このリポジトリへ適用するライセンスを決定して`LICENSE`ファイルを追加してください。ライセンスがない状態では、第三者に利用・改変・再配布の許可が明確に付与されません。
