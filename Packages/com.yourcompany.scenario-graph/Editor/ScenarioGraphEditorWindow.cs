using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScenarioGraphSystem.Editor
{
    /// <summary>ScenarioGraphアセット専用のGraphView EditorWindowです。</summary>
    public sealed class ScenarioGraphEditorWindow : EditorWindow
    {
        [SerializeField] private ScenarioGraph graph;
        private ScenarioGraphView graphView;
        private MiniMap miniMap;
        private ScrollView validationList;
        private Label statusLabel;

        [MenuItem("Window/Scenario/Scenario Graph Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<ScenarioGraphEditorWindow>();
            window.titleContent = new GUIContent("Scenario Graph");
            if (Selection.activeObject is ScenarioGraph selected)
                window.Open(selected);
        }

        /// <summary>指定アセットを専用Windowで開きます。</summary>
        public static void OpenWindowFor(ScenarioGraph target)
        {
            var window = GetWindow<ScenarioGraphEditorWindow>();
            window.Open(target);
            window.Show();
        }

        /// <summary>指定したグラフをこのWindowで開きます。</summary>
        public void Open(ScenarioGraph target)
        {
            graph = target;
            titleContent = new GUIContent(target != null ? $"Scenario: {target.name}" : "Scenario Graph");
            BuildUi();
            Repaint();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            BuildUi();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Save();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            var toolbar = new Toolbar();
            var graphField = new ObjectField("グラフ")
            {
                objectType = typeof(ScenarioGraph),
                value = graph,
                allowSceneObjects = false,
                tooltip = "編集するScenarioGraphアセットを指定します。"
            };
            graphField.style.minWidth = 260;
            graphField.RegisterValueChangedCallback(evt => Open(evt.newValue as ScenarioGraph));
            toolbar.Add(graphField);
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(() => graphView?.CreateNode(ScenarioNodeType.Start, ViewCenter())) { text = "開始" });
            toolbar.Add(new ToolbarButton(() => graphView?.CreateNode(ScenarioNodeType.Scenario, ViewCenter())) { text = "シナリオ" });
            toolbar.Add(new ToolbarButton(() => graphView?.CreateNode(ScenarioNodeType.Game, ViewCenter())) { text = "ゲーム" });
            toolbar.Add(new ToolbarButton(() => graphView?.CreateGroup(ViewCenter())) { text = "グループ" });
            toolbar.Add(new ToolbarButton(() => graphView?.CreateComment(ViewCenter())) { text = "コメント" });
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(() => graphView?.AutoLayout()) { text = "左→右へ整列" });
            toolbar.Add(new ToolbarButton(() => RefreshValidation(true)) { text = "検証" });
            toolbar.Add(new ToolbarButton(Save) { text = "保存" });

            var minimapToggle = new ToolbarToggle { text = "ミニマップ", value = graph == null || graph.EditorState.MinimapVisible };
            minimapToggle.RegisterValueChangedCallback(evt =>
            {
                if (miniMap != null) miniMap.visible = evt.newValue;
                if (graph != null)
                {
                    Undo.RecordObject(graph, "ミニマップ表示を変更");
                    graph.EditorState.MinimapVisible = evt.newValue;
                    EditorUtility.SetDirty(graph);
                }
            });
            toolbar.Add(minimapToggle);

            var search = new ToolbarSearchField { tooltip = "ノード名、CSV名、ゲーム名、コメント、グループ名を検索" };
            search.RegisterValueChangedCallback(evt =>
            {
                var count = graphView?.Search(evt.newValue) ?? 0;
                statusLabel.text = string.IsNullOrEmpty(evt.newValue) ? string.Empty : $"検索: {count}件";
            });
            toolbar.Add(search);
            rootVisualElement.Add(toolbar);

            statusLabel = new Label { style = { minHeight = 20, paddingLeft = 6 } };
            if (graph == null)
                statusLabel.text = "ScenarioGraphアセットを選択するか、Assets > Create > Scenario > Scenario Graph で作成してください。";
            rootVisualElement.Add(statusLabel);
            graphView = new ScenarioGraphView(this);
            rootVisualElement.Add(graphView);
            graphView.Load(graph);
            miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(new Rect(10, 30, 220, 140));
            miniMap.visible = graph == null || graph.EditorState.MinimapVisible;
            graphView.Add(miniMap);

            validationList = new ScrollView { style = { height = 110, borderTopWidth = 1, paddingLeft = 4 } };
            rootVisualElement.Add(validationList);
            RefreshValidation(false);
        }

        /// <summary>検証結果一覧を更新し、必要なら完了ダイアログを表示します。</summary>
        internal void RefreshValidation(bool showDialog)
        {
            if (validationList == null)
                return;
            validationList.Clear();
            var errors = graph != null ? ScenarioGraphEditorValidator.Validate(graph) : new List<GraphValidationError> { new("ScenarioGraphアセットを開いてください。") };
            if (errors.Count == 0)
            {
                validationList.Add(new Label("✓ エラーはありません。"));
                if (showDialog) EditorUtility.DisplayDialog("バリデーション", "エラーはありません。", "OK");
                return;
            }

            foreach (var error in errors)
            {
                var button = new Button(() => SelectElement(error.ElementGuid)) { text = $"• {error.Message}" };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                validationList.Add(button);
            }
            if (showDialog) EditorUtility.DisplayDialog("バリデーション", $"{errors.Count}件のエラーがあります。下部一覧を確認してください。", "OK");
        }

        /// <summary>編集対象未選択時に、無言で操作を失敗させず作成手順を案内します。</summary>
        internal void ShowGraphRequired()
        {
            EditorUtility.DisplayDialog(
                "ScenarioGraphを選択してください",
                "先に Assets > Create > Scenario > Scenario Graph でアセットを作成し、このWindow上部の「グラフ」欄で選択してください。",
                "OK");
        }

        private void SelectElement(string guid)
        {
            if (string.IsNullOrEmpty(guid) || graphView == null)
                return;
            graphView.ClearSelection();
            foreach (var element in graphView.graphElements)
            {
                if (element.userData as string != guid) continue;
                graphView.AddToSelection(element);
                graphView.FrameSelection();
                break;
            }
        }

        private Vector2 ViewCenter()
        {
            return graphView == null ? Vector2.zero : graphView.contentViewContainer.WorldToLocal(graphView.worldBound.center);
        }

        private void Save()
        {
            if (graph == null)
                return;
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssetIfDirty(graph);
            if (statusLabel != null) statusLabel.text = $"保存しました: {System.DateTime.Now:HH:mm:ss}";
        }

        private void OnUndoRedo()
        {
            graphView?.Reload();
            RefreshValidation(false);
        }

        [OnOpenAsset(1)]
#pragma warning disable CS0618 // OnOpenAssetはintを渡すため、Unity 6.0互換APIを使用します。
        private static bool OpenScenarioGraphAsset(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is not ScenarioGraph target)
                return false;
            var window = GetWindow<ScenarioGraphEditorWindow>();
            window.Open(target);
            window.Show();
            return true;
        }
#pragma warning restore CS0618
    }
}
