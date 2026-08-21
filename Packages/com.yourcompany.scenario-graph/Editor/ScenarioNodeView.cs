using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScenarioGraphSystem.Editor
{
    /// <summary>NodeDataを編集するGraphView表示オブジェクトです。永続データ自体は保持しません。</summary>
    internal sealed class ScenarioNodeView : Node
    {
        private readonly ScenarioGraphView owner;
        private readonly Dictionary<string, Port> outputPorts = new();

        public ScenarioNodeView(ScenarioGraphView owner, NodeData data)
        {
            this.owner = owner;
            Data = data;
            userData = data.Guid;
            viewDataKey = data.Guid;
            capabilities |= Capabilities.Resizable | Capabilities.Collapsible;
            title = data.DisplayName;

            if (data.NodeType != ScenarioNodeType.Start)
            {
                Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                Input.portName = "入力";
                inputContainer.Add(Input);
            }

            BuildTitleEditor();
            BuildContent();
            BuildOutputPorts();
            SetPosition(data.Position);
            expanded = !data.Collapsed;
            RefreshExpandedState();
            RefreshPorts();
            RegisterCallback<GeometryChangedEvent>(_ => PersistGeometry());
        }

        public NodeData Data { get; }
        public Port Input { get; }
        public IReadOnlyDictionary<string, Port> OutputPorts => outputPorts;

        private void BuildTitleEditor()
        {
            var nameField = new TextField { value = Data.DisplayName, tooltip = "ノード表示名" };
            nameField.RegisterValueChangedCallback(evt => owner.Mutate("ノード名を変更", () =>
            {
                Data.DisplayName = evt.newValue;
                title = evt.newValue;
            }));
            titleButtonContainer.Add(nameField);
        }

        private void BuildContent()
        {
            switch (Data.NodeType)
            {
                case ScenarioNodeType.Start:
                    extensionContainer.Add(new Label("グラフの実行開始地点"));
                    break;
                case ScenarioNodeType.Scenario:
                    BuildScenarioFields();
                    break;
                case ScenarioNodeType.Game:
                    BuildGameFields();
                    break;
            }
        }

        private void BuildScenarioFields()
        {
            var definitionField = new ObjectField("Scenario Definition")
            {
                objectType = typeof(ScenarioDefinition),
                value = Data.ScenarioDefinition,
                allowSceneObjects = false
            };
            definitionField.RegisterValueChangedCallback(evt => owner.Mutate(
                "ScenarioDefinitionを変更",
                () => Data.ScenarioDefinition = evt.newValue as ScenarioDefinition));
            extensionContainer.Add(definitionField);
        }

        private void BuildGameFields()
        {
            var registryField = new ObjectField("Registry") { objectType = typeof(GameRegistry), value = Data.GameRegistry, allowSceneObjects = false };
            registryField.RegisterValueChangedCallback(evt => owner.Mutate("GameRegistryを変更", () =>
            {
                Data.GameRegistry = evt.newValue as GameRegistry;
                Data.GameId = string.Empty;
                owner.Reload();
            }));
            extensionContainer.Add(registryField);

            var registrations = Data.GameRegistry != null ? Data.GameRegistry.Games : new List<GameRegistration>();
            var choices = new List<string> { "(未選択)" };
            choices.AddRange(registrations.Select(game => game.DisplayName));
            var current = registrations.FindIndex(game => game.GameId == Data.GameId) + 1;
            var popup = new PopupField<string>("ゲーム", choices, Mathf.Clamp(current, 0, choices.Count - 1));
            popup.SetEnabled(Data.GameRegistry != null);
            popup.RegisterValueChangedCallback(evt => owner.Mutate("ゲームを選択", () =>
            {
                var selectedIndex = choices.IndexOf(evt.newValue) - 1;
                Data.GameId = selectedIndex >= 0 ? registrations[selectedIndex].GameId : string.Empty;
                owner.Reload();
            }));
            extensionContainer.Add(popup);

            var definitionField = new ObjectField("Definition") { objectType = typeof(GameData), value = Data.GameData, allowSceneObjects = false };
            definitionField.RegisterValueChangedCallback(evt => owner.Mutate("GameDataを変更", () => Data.GameData = evt.newValue as GameData));
            extensionContainer.Add(definitionField);

            var addButton = new Button(() => owner.AddGameOutput(Data.Guid)) { text = "+ 出力ポート" };
            extensionContainer.Add(addButton);
        }

        private void BuildOutputPorts()
        {
            outputPorts.Clear();
            foreach (var portData in Data.OutputPorts)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                port.portName = Data.NodeType == ScenarioNodeType.Game ? portData.GameResult.ToString() : portData.DisplayName;
                port.userData = portData.Guid;
                outputPorts[portData.Guid] = port;
                row.Add(port);

                if (Data.NodeType == ScenarioNodeType.Game)
                {
                    var resultField = new EnumField(portData.GameResult) { style = { width = 95 } };
                    resultField.RegisterValueChangedCallback(evt => owner.Mutate("GameResultを変更", () =>
                    {
                        var result = (GameResult)evt.newValue;
                        if (Data.OutputPorts.Any(port => port != portData && port.GameResult == result))
                        {
                            EditorUtility.DisplayDialog("GameResult重複", $"GameResult.{result}は同じノード内ですでに使用されています。", "OK");
                            owner.Reload();
                            return;
                        }
                        portData.GameResult = result;
                        owner.Reload();
                    }));
                    row.Add(resultField);
                    var remove = new Button(() => owner.RemoveGameOutput(Data.Guid, portData.Guid)) { text = "−" };
                    row.Add(remove);
                }
                outputContainer.Add(row);
            }
        }

        private void PersistGeometry()
        {
            var rect = GetPosition();
            if (rect.width <= 0 || rect.height <= 0 || Data.Position == rect && Data.Collapsed == !expanded)
                return;
            owner.Mutate("ノード表示を変更", () =>
            {
                Data.Position = rect;
                Data.Collapsed = !expanded;
            }, false);
        }
    }

    /// <summary>本文と矩形を永続化する独立コメント表示です。</summary>
    internal sealed class ScenarioCommentView : GraphElement
    {
        private readonly ScenarioGraphView owner;

        public ScenarioCommentView(ScenarioGraphView owner, CommentData data)
        {
            this.owner = owner;
            Data = data;
            userData = data.Guid;
            viewDataKey = data.Guid;
            capabilities = Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable | Capabilities.Resizable;
            style.backgroundColor = new Color(0.42f, 0.36f, 0.12f, 0.96f);
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 6;
            style.paddingBottom = 6;
            var field = new TextField("コメント") { value = data.Text, multiline = true };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => owner.Mutate("コメントを編集", () => Data.Text = evt.newValue));
            Add(field);
            SetPosition(data.Position);
            RegisterCallback<GeometryChangedEvent>(_ => PersistGeometry());
        }

        public CommentData Data { get; }

        private void PersistGeometry()
        {
            var rect = GetPosition();
            if (rect.width <= 0 || rect.height <= 0 || rect == Data.Position)
                return;
            owner.Mutate("コメントを移動", () => Data.Position = rect, false);
        }
    }
}
