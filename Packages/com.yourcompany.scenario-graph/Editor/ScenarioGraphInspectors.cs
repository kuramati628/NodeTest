using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ScenarioGraphSystem.Editor
{
    /// <summary>SceneAsset選択をRuntime用のGUIDとパスへ同期するPropertyDrawerです。</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    internal sealed class SceneReferencePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var guid = property.FindPropertyRelative("sceneGuid");
            var path = property.FindPropertyRelative("scenePath");
            var resolvedPath = string.IsNullOrEmpty(guid.stringValue)
                ? path.stringValue
                : AssetDatabase.GUIDToAssetPath(guid.stringValue);

            if (!string.IsNullOrEmpty(resolvedPath) && path.stringValue != resolvedPath)
                path.stringValue = resolvedPath;

            var current = AssetDatabase.LoadAssetAtPath<SceneAsset>(resolvedPath);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var selected = (SceneAsset)EditorGUI.ObjectField(position, label, current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                var selectedPath = selected != null ? AssetDatabase.GetAssetPath(selected) : string.Empty;
                path.stringValue = selectedPath;
                guid.stringValue = string.IsNullOrEmpty(selectedPath)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(selectedPath);
            }
            EditorGUI.EndProperty();
        }
    }

    /// <summary>グラフの内部リストをInspectorで壊さず、専用エディタへ誘導するInspectorです。</summary>
    [CustomEditor(typeof(ScenarioGraph))]
    internal sealed class ScenarioGraphInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var graph = (ScenarioGraph)target;
            EditorGUILayout.LabelField("Graph GUID", graph.GraphGuid);
            EditorGUILayout.LabelField("ノード", graph.Nodes.Count.ToString());
            EditorGUILayout.LabelField("Edge", graph.Edges.Count.ToString());
            EditorGUILayout.LabelField("グループ", graph.Groups.Count.ToString());
            EditorGUILayout.LabelField("コメント", graph.Comments.Count.ToString());
            if (GUILayout.Button("Scenario Graph Editorで開く"))
                ScenarioGraphEditorWindow.OpenWindowFor(graph);
            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>不変ゲームIDを安全に生成し、重複名を即時表示するGameRegistry Inspectorです。</summary>
    [CustomEditor(typeof(GameRegistry))]
    internal sealed class GameRegistryInspector : UnityEditor.Editor
    {
        private ReorderableList list;
        private SerializedProperty games;

        private void OnEnable()
        {
            games = serializedObject.FindProperty("games");
            list = new ReorderableList(serializedObject, games, true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "ゲーム登録");
            list.elementHeight = EditorGUIUtility.singleLineHeight * 4 + 12;
            list.drawElementCallback = DrawElement;
            list.onAddCallback = _ => AddRegistration();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();

            var registry = (GameRegistry)target;
            var duplicates = registry.Games.Where(game => !string.IsNullOrWhiteSpace(game.DisplayName))
                .GroupBy(game => game.DisplayName, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
            if (duplicates.Length > 0)
                EditorGUILayout.HelpBox($"ゲーム名が重複しています: {string.Join(", ", duplicates)}", MessageType.Error);
        }

        private void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            var element = games.GetArrayElementAtIndex(index);
            var line = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(line, element.FindPropertyRelative("gameId"), new GUIContent("ゲームID"));
            line.y += EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(line, element.FindPropertyRelative("displayName"), new GUIContent("表示名"));
            line.y += EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(line, element.FindPropertyRelative("scene"), new GUIContent("ゲームシーン"));
            line.y += EditorGUIUtility.singleLineHeight;
            var name = element.FindPropertyRelative("displayName").stringValue;
            var duplicate = Enumerable.Range(0, games.arraySize).Count(i => games.GetArrayElementAtIndex(i).FindPropertyRelative("displayName").stringValue == name) > 1;
            if (duplicate && !string.IsNullOrWhiteSpace(name)) EditorGUI.HelpBox(line, "表示名は一意である必要があります。", MessageType.Error);
        }

        private void AddRegistration()
        {
            serializedObject.Update();
            var index = games.arraySize;
            games.InsertArrayElementAtIndex(index);
            var element = games.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("gameId").stringValue = Guid.NewGuid().ToString("N");
            element.FindPropertyRelative("displayName").stringValue = "New Game";
            element.FindPropertyRelative("scene").FindPropertyRelative("sceneGuid").stringValue = string.Empty;
            element.FindPropertyRelative("scene").FindPropertyRelative("scenePath").stringValue = string.Empty;
            serializedObject.ApplyModifiedProperties();
        }
    }

}
