using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// BlendshapeMapping 用カスタムインスペクター
/// ・proxyNames から選択して Create
/// ・proxies 配列を ReorderableList で描画
///   ├─ 行 1 : name フィールド + Capture / Preview ボタン
///   └─ 行 2 : blendshapes 配列（折りたたみ可）
///— スクリプト実行時に blendshapeMapping.Capture(i) / Preview(i) 呼び出し
/// </summary>
[CustomEditor(typeof(BlendshapeMapping))]
public sealed class BlendshapeMappingEditor : Editor
{
    // ───────── プロパティ保持 ─────────
    private int selectedNameIndex;
    private BlendshapeMapping blendshapeMapping;
    private string proxyName;

    private ReorderableList proxiesList;
    // ────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        blendshapeMapping = (BlendshapeMapping)target;

        // ───── ReorderableList セットアップ ─────
        SerializedProperty proxiesProp = serializedObject.FindProperty("proxies");

        proxiesList = new ReorderableList(serializedObject, proxiesProp,
                                          draggable: true, displayHeader: true,
                                          displayAddButton: true, displayRemoveButton: true);

        // ヘッダー
        proxiesList.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, "Proxies");

        proxiesList.elementHeightCallback = index =>
        {
            var element = proxiesProp.GetArrayElementAtIndex(index);
            var blendsProp = element.FindPropertyRelative("blendshapes");
            float lineH = EditorGUIUtility.singleLineHeight;
            float spacing = 6f;

            // 1 行目（name + buttons）
            float height = lineH + spacing;

            // foldout が開いているときだけ子要素ぶんを足す
            if (blendsProp.isExpanded)
            {
                height += EditorGUI.GetPropertyHeight(blendsProp, true);
            } else
            {
                height += lineH;
            }

            return height + 4;   // 余白
        };

        proxiesList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = proxiesProp.GetArrayElementAtIndex(index);
            var nameProp = element.FindPropertyRelative("name");
            var blendsProp = element.FindPropertyRelative("blendshapes");

            const float btnWidth = 70f;
            const float spacing = 4f;
            float lineH = EditorGUIUtility.singleLineHeight;

            // 1 行目 : name + buttons
            var nameRect = new Rect(rect.x, rect.y + 2, rect.width - (btnWidth * 2 + spacing * 3), lineH);
            EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);

            if (GUI.Button(new Rect(nameRect.xMax + spacing, rect.y + 2, btnWidth, lineH), "Capture"))
                blendshapeMapping.Capture(index);
            if (GUI.Button(new Rect(nameRect.xMax + spacing + btnWidth + spacing, rect.y + 2, btnWidth, lineH), "Preview"))
                blendshapeMapping.Preview(index);

            // 2 行目以降 : blendshapes（開いているときだけ）
            if (blendsProp.isExpanded)
            {
                var blendsRect = new Rect(
                    rect.x + 12,
                    rect.y + lineH + 6,
                    rect.width - 12,
                    EditorGUI.GetPropertyHeight(blendsProp, true));

                EditorGUI.PropertyField(blendsRect, blendsProp, new GUIContent("Blendshapes"), true);
            }
        };

        //// 要素の高さ（折りたたみ開閉に追従）
        //proxiesList.elementHeightCallback = index =>
        //{
        //    SerializedProperty element = proxiesProp.GetArrayElementAtIndex(index);
        //    return EditorGUI.GetPropertyHeight(element, includeChildren: true) + 6;
        //};

        // 要素の描画
        proxiesList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            SerializedProperty element = proxiesProp.GetArrayElementAtIndex(index);

            // 子プロパティ
            SerializedProperty nameProp = element.FindPropertyRelative("name");
            SerializedProperty blendsProp = element.FindPropertyRelative("blendshapes");

            // レイアウト計算
            const float btnWidth = 70f;
            const float spacing = 4f;
            float lineH = EditorGUIUtility.singleLineHeight;

            // ----- 1 行目 : name + ボタン 2 つ
            Rect nameRect = new Rect(
                rect.x,
                rect.y + 2,
                rect.width - (btnWidth * 2 + spacing * 3),
                lineH);

            EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);

            if (GUI.Button(new Rect(nameRect.xMax + spacing, rect.y + 2, btnWidth, lineH), "Capture"))
                blendshapeMapping.Capture(index);

            if (GUI.Button(new Rect(nameRect.xMax + spacing + btnWidth + spacing, rect.y + 2, btnWidth, lineH), "Preview"))
                blendshapeMapping.Preview(index);

            // ----- 2 行目以降 : blendshapes
            if (blendsProp != null)
            {
                Rect blendsRect = new Rect(
                    rect.x + 12,                         // インデント
                    rect.y + lineH + 6,
                    rect.width - 12,
                    EditorGUI.GetPropertyHeight(blendsProp, includeChildren: true));

                EditorGUI.PropertyField(blendsRect, blendsProp, new GUIContent("Blendshapes"), includeChildren: true);
            }
        };
    }
    // ─────────────────────────────────────────────────────────────

    // ───────── インスペクター描画 ─────────
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawProperty("skinnedMeshRenderer");
        DrawProperty("proxyNames");
        if(blendshapeMapping.proxyNames != null)
        {
            // proxyNames 選択
            var names = blendshapeMapping.proxyNames.names;
            if (names == null || names.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "proxyNames スクリプタブルオブジェクトが空です。\n(Asset > Create > Blendshape > ProxyNameList)",
                    MessageType.Info);
            }
            else
            {
                if (selectedNameIndex >= names.Count) selectedNameIndex = 0;
                selectedNameIndex = EditorGUILayout.Popup("Proxy Name", selectedNameIndex, names.ToArray());
                proxyName = names[selectedNameIndex];
            }
        }

        EditorGUILayout.BeginHorizontal();
        // Create ボタン
        if (GUILayout.Button("Create"))
            blendshapeMapping.Create(proxyName);
        if (GUILayout.Button("Reset Blendshapes"))
            blendshapeMapping.ResetAllBlendshapes();
        EditorGUILayout.EndHorizontal();

        // proxies 配列を ReorderableList で描画
        proxiesList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }
    // ──────────────────────────────────────

    /// <summary>任意フィールドを通常の PropertyField として描画</summary>
    private void DrawProperty(string name)
    {
        SerializedProperty prop = serializedObject.FindProperty(name);
        if (prop != null)
            EditorGUILayout.PropertyField(prop, includeChildren: true);
        else
            Debug.LogWarning($"Property '{name}' not found. Check spelling.");
    }
}
