using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScenarioGraphSystem.Editor
{
    /// <summary>ScenarioGraphを編集し、すべてのUI変更を永続モデルへ同期するGraphViewです。</summary>
    internal sealed class ScenarioGraphView : GraphView
    {
        [Serializable]
        private sealed class ClipboardData
        {
            public List<NodeData> nodes = new();
            public List<EdgeData> edges = new();
        }

        private readonly ScenarioGraphEditorWindow window;
        private readonly Dictionary<string, ScenarioNodeView> nodeViews = new();
        private bool rebuilding;
        private bool delayedReloadQueued;

        public ScenarioGraphView(ScenarioGraphEditorWindow window)
        {
            this.window = window;
            style.flexGrow = 1;
            Insert(0, new GridBackground());
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ClickSelector());
            graphViewChanged = OnGraphViewChanged;
            groupTitleChanged = (group, title) =>
            {
                if (group.userData is not string guid) return;
                var data = Graph?.Groups.Find(candidate => candidate.Guid == guid);
                if (data != null) Mutate("グループ名を変更", () => data.Title = title);
            };
            elementsAddedToGroup = (group, elements) => UpdateGroupMembers(group, elements, true);
            elementsRemovedFromGroup = (group, elements) => UpdateGroupMembers(group, elements, false);
            serializeGraphElements = SerializeSelection;
            canPasteSerializedData = data => !string.IsNullOrEmpty(data) && data.Contains("\"nodes\"");
            unserializeAndPaste = Paste;
            deleteSelection = DeleteSelectionPreservingGroupChildren;
            viewTransformChanged += _ => PersistViewTransform();

            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                var graphPosition = this.ChangeCoordinatesTo(contentViewContainer, evt.localMousePosition);
                evt.menu.AppendAction("ノード/シナリオ", _ => CreateNode(ScenarioNodeType.Scenario, graphPosition));
                evt.menu.AppendAction("ノード/ゲーム", _ => CreateNode(ScenarioNodeType.Game, graphPosition));
                evt.menu.AppendAction("ノード/終了", _ => CreateNode(ScenarioNodeType.End, graphPosition));
                evt.menu.AppendAction("グループ", _ => CreateGroup(graphPosition));
                evt.menu.AppendAction("コメント", _ => CreateComment(graphPosition));
            }));
        }

        public ScenarioGraph Graph { get; private set; }

        /// <summary>保存データからGraphViewを完全再構築します。</summary>
        public void Load(ScenarioGraph graph)
        {
            Graph = graph;
            Reload();
            if (graph != null)
                UpdateViewTransform(graph.EditorState.ViewPosition, graph.EditorState.ViewScale);
        }

        public void Reload()
        {
            if (Graph == null || rebuilding)
                return;
            if (SynchronizeAllSentenceOutputs())
                EditorUtility.SetDirty(Graph);
            rebuilding = true;
            DeleteElements(graphElements.Where(element => element is not MiniMap).ToList());
            nodeViews.Clear();

            foreach (var node in Graph.Nodes)
            {
                var view = new ScenarioNodeView(this, node);
                nodeViews[node.Guid] = view;
                AddElement(view);
            }
            foreach (var edgeData in Graph.Edges)
                AddEdgeView(edgeData);
            foreach (var groupData in Graph.Groups)
                AddGroupView(groupData);
            foreach (var commentData in Graph.Comments)
                AddElement(new ScenarioCommentView(this, commentData));

            rebuilding = false;
        }

        /// <summary>SentenceDataのInspector編集後に、enum分岐と出力ポートを同期します。</summary>
        public bool SynchronizeSentenceOutputsFromInspector()
        {
            if (Graph == null || rebuilding || !SynchronizeAllSentenceOutputs())
                return false;
            EditorUtility.SetDirty(Graph);
            Reload();
            return true;
        }

        /// <summary>Undo登録、Dirty化、Window更新を一箇所で行います。</summary>
        public void Mutate(string undoName, Action change, bool refreshValidation = true)
        {
            if (Graph == null || rebuilding)
                return;
            Undo.RecordObject(Graph, undoName);
            change();
            EditorUtility.SetDirty(Graph);
            if (refreshValidation)
                window.RefreshValidation(false);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            // 自己接続とループを許可し、方向が反対のポートだけを候補にします。
            return ports.Where(port => port != startPort && port.direction != startPort.direction).ToList();
        }

        public void CreateNode(ScenarioNodeType type, Vector2 position)
        {
            if (Graph == null)
            {
                window.ShowGraphRequired();
                return;
            }
            if (type == ScenarioNodeType.Start && Graph.Nodes.Any(node => node.NodeType == ScenarioNodeType.Start))
            {
                EditorUtility.DisplayDialog("開始ノード", "開始ノードはグラフにつき1個だけ配置できます。", "OK");
                return;
            }
            if (type == ScenarioNodeType.End && Graph.Nodes.Any(node => node.NodeType == ScenarioNodeType.End))
            {
                EditorUtility.DisplayDialog("終了ノード", "終了ノードはグラフにつき1個だけ配置できます。", "OK");
                return;
            }
            Mutate("ノードを作成", () =>
            {
                var node = NodeData.Create(type, position);
                Graph.Nodes.Add(node);
                if (type == ScenarioNodeType.Start)
                    Graph.StartNodeGuid = node.Guid;
            });
            Reload();
        }

        public void CreateGroup(Vector2 position)
        {
            Mutate("グループを作成", () => Graph.Groups.Add(GroupData.Create(position)));
            Reload();
        }

        public void CreateComment(Vector2 position)
        {
            Mutate("コメントを作成", () => Graph.Comments.Add(CommentData.Create(position)));
            Reload();
        }

        public void SetSentenceData(string nodeGuid, SentenceData sentenceData)
        {
            var node = Graph.FindNode(nodeGuid);
            if (node == null)
                return;
            Mutate("SentenceDataを変更", () =>
            {
                node.SentenceData = sentenceData;
                SynchronizeSentenceOutputs(node);
            });
            Reload();
        }

        public void DebugNode(string nodeGuid)
        {
            var node = Graph.FindNode(nodeGuid);
            if (node == null)
                return;
            if (node.NodeType == ScenarioNodeType.Scenario &&
                (node.ScenarioDefinition == null || node.ScenarioDefinition.Csv == null))
            {
                EditorUtility.DisplayDialog("ノードデバッグ", "ノードにScenarioDefinitionとCSVを設定してください。", "OK");
                return;
            }
            if (node.NodeType == ScenarioNodeType.Game &&
                (node.GameRegistry == null || string.IsNullOrEmpty(node.GameId) || node.SentenceData == null ||
                 node.SentenceData.GetBranchNames().Count == 0))
            {
                EditorUtility.DisplayDialog("ノードデバッグ", "ノードにゲームと、分岐を持つSentenceDataを設定してください。", "OK");
                return;
            }
            ScenarioGraphNodeDebugSession.Start(Graph, node);
        }

        private bool SynchronizeAllSentenceOutputs()
        {
            var changed = false;
            foreach (var node in Graph.Nodes.Where(node => node.NodeType == ScenarioNodeType.Game))
                changed |= SynchronizeSentenceOutputs(node);
            return changed;
        }

        private bool SynchronizeSentenceOutputs(NodeData node)
        {
            var before = node.OutputPorts.Select(port => (port.Guid, port.DisplayName, port.BranchName)).ToList();
            var unused = node.OutputPorts.ToList();
            var synchronized = new List<OutputPortData>();
            var branchNames = node.SentenceData?.GetBranchNames() ?? Array.Empty<string>();
            foreach (var branchName in branchNames)
            {
                var existing = unused.FirstOrDefault(port => port.BranchName == branchName) ??
                               unused.FirstOrDefault(port => string.IsNullOrEmpty(port.BranchName) && port.DisplayName == branchName);
                if (existing != null)
                {
                    unused.Remove(existing);
                    existing.BranchName = branchName;
                    existing.DisplayName = branchName;
                    synchronized.Add(existing);
                }
                else
                {
                    synchronized.Add(OutputPortData.Create(branchName, branchName));
                }
            }

            var removedGuids = unused.Select(port => port.Guid).ToHashSet();
            node.OutputPorts.Clear();
            node.OutputPorts.AddRange(synchronized);
            if (removedGuids.Count > 0)
                Graph.Edges.RemoveAll(edge => edge.OutputNodeGuid == node.Guid && removedGuids.Contains(edge.OutputPortGuid));

            var after = node.OutputPorts.Select(port => (port.Guid, port.DisplayName, port.BranchName)).ToList();
            return !before.SequenceEqual(after) || removedGuids.Count > 0;
        }

        /// <summary>検索語に一致するノード、グループ、コメントを選択してフレーム表示します。</summary>
        public int Search(string query)
        {
            ClearSelection();
            if (string.IsNullOrWhiteSpace(query))
                return 0;
            var comparison = StringComparison.OrdinalIgnoreCase;
            var matches = graphElements.Where(element => Matches(element, query, comparison)).ToList();
            foreach (var match in matches)
                AddToSelection(match);
            if (matches.Count > 0)
                FrameSelection();
            return matches.Count;
        }

        /// <summary>開始ノードから幅優先で階層を決定し、ループをvisited集合で打ち切って左から右へ整列します。</summary>
        public void AutoLayout()
        {
            if (Graph == null)
                return;
            var start = Graph.GetStartNode();
            if (start == null)
            {
                EditorUtility.DisplayDialog("自動整列", "開始ノードがありません。", "OK");
                return;
            }

            var levels = new Dictionary<string, int> { [start.Guid] = 0 };
            var queue = new Queue<NodeData>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var source = queue.Dequeue();
                foreach (var edge in Graph.Edges.Where(item => item.OutputNodeGuid == source.Guid))
                {
                    var target = Graph.FindNode(edge.InputNodeGuid);
                    if (target == null || levels.ContainsKey(target.Guid))
                        continue;
                    levels[target.Guid] = levels[source.Guid] + 1;
                    queue.Enqueue(target);
                }
            }
            var unreachableLevel = levels.Values.DefaultIfEmpty(-1).Max() + 1;
            foreach (var node in Graph.Nodes.Where(node => !levels.ContainsKey(node.Guid)))
                levels[node.Guid] = unreachableLevel;

            Mutate("グラフを自動整列", () =>
            {
                foreach (var level in levels.GroupBy(item => item.Value).OrderBy(group => group.Key))
                {
                    var index = 0;
                    foreach (var item in level.OrderBy(item => Graph.Nodes.FindIndex(node => node.Guid == item.Key)))
                    {
                        var node = Graph.FindNode(item.Key);
                        node.Position = new Rect(new Vector2(80 + level.Key * 360, 80 + index++ * 270), node.Position.size);
                    }
                }
            });
            Reload();
            FrameAll();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (rebuilding || Graph == null)
                return change;

            if (change.elementsToRemove != null)
            {
                var material = change.elementsToRemove.Any(element => element is ScenarioNodeView || element is Group || element is ScenarioCommentView);
                if (material && !EditorUtility.DisplayDialog("要素を削除", "選択した要素を削除します。グループ内のノードは残ります。", "削除", "キャンセル"))
                {
                    change.elementsToRemove.Clear();
                    return change;
                }
                Mutate("グラフ要素を削除", () => RemoveData(change.elementsToRemove), false);
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                Mutate("Edgeを接続", () =>
                {
                    foreach (var edge in change.edgesToCreate)
                    {
                        var outputNode = edge.output?.node as ScenarioNodeView;
                        var inputNode = edge.input?.node as ScenarioNodeView;
                        var portGuid = edge.output?.userData as string;
                        if (outputNode != null && inputNode != null && !string.IsNullOrEmpty(portGuid))
                            Graph.Edges.Add(EdgeData.Create(outputNode.Data.Guid, portGuid, inputNode.Data.Guid));
                    }
                }, false);
                QueueReload();
            }

            if (change.movedElements != null)
            {
                Mutate("グラフ要素を移動", () =>
                {
                    foreach (var element in change.movedElements)
                    {
                        if (element is ScenarioNodeView node)
                            node.Data.Position = node.GetPosition();
                        else if (element is ScenarioCommentView comment)
                            comment.Data.Position = comment.GetPosition();
                        else if (element is Group group && group.userData is string groupGuid)
                        {
                            var data = Graph.Groups.Find(candidate => candidate.Guid == groupGuid);
                            if (data != null) data.Position = group.GetPosition();
                        }
                    }
                }, false);
            }
            window.RefreshValidation(false);
            return change;
        }

        /// <summary>Groupの標準再帰削除を避け、Groupだけを削除して内部ノードを残します。</summary>
        private void DeleteSelectionPreservingGroupChildren(string operationName, AskUser askUser)
        {
            var elements = new HashSet<GraphElement>();
            foreach (var selected in selection.OfType<GraphElement>())
            {
                if ((selected.capabilities & Capabilities.Deletable) == 0)
                    continue;
                elements.Add(selected);
                if (selected is ScenarioNodeView node)
                {
                    foreach (var edge in edges.ToList().Where(edge => edge.input?.node == node || edge.output?.node == node))
                        elements.Add(edge);
                }
            }
            if (elements.Count == 0)
                return;
            DeleteElements(elements);
            ClearSelection();
        }

        private void RemoveData(IEnumerable<GraphElement> elements)
        {
            foreach (var element in elements.ToList())
            {
                switch (element)
                {
                    case ScenarioEdge edge when edge.userData is string edgeGuid:
                        Graph.Edges.RemoveAll(item => item.Guid == edgeGuid);
                        break;
                    case Edge edge:
                        Graph.Edges.RemoveAll(item => item.OutputNodeGuid == (edge.output?.node as ScenarioNodeView)?.Data.Guid &&
                                                          item.OutputPortGuid == edge.output?.userData as string &&
                                                          item.InputNodeGuid == (edge.input?.node as ScenarioNodeView)?.Data.Guid);
                        break;
                    case ScenarioNodeView node:
                        Graph.Nodes.RemoveAll(item => item.Guid == node.Data.Guid);
                        Graph.Edges.RemoveAll(item => item.OutputNodeGuid == node.Data.Guid || item.InputNodeGuid == node.Data.Guid);
                        foreach (var group in Graph.Groups) group.NodeGuids.Remove(node.Data.Guid);
                        if (Graph.StartNodeGuid == node.Data.Guid) Graph.StartNodeGuid = string.Empty;
                        break;
                    case Group group when group.userData is string groupGuid:
                        Graph.Groups.RemoveAll(item => item.Guid == groupGuid);
                        break;
                    case ScenarioCommentView comment:
                        Graph.Comments.RemoveAll(item => item.Guid == comment.Data.Guid);
                        break;
                }
            }
        }

        private void AddEdgeView(EdgeData data)
        {
            if (!nodeViews.TryGetValue(data.OutputNodeGuid, out var outputNode) ||
                !nodeViews.TryGetValue(data.InputNodeGuid, out var inputNode) || inputNode.Input == null ||
                !outputNode.OutputPorts.TryGetValue(data.OutputPortGuid, out var outputPort))
                return;
            var edge = outputPort.ConnectTo<ScenarioEdge>(inputNode.Input);
            edge.userData = data.Guid;
            AddElement(edge);
        }

        private void AddGroupView(GroupData data)
        {
            var group = new Group { title = data.Title, userData = data.Guid, viewDataKey = data.Guid };
            group.SetPosition(data.Position);
            AddElement(group);
            foreach (var guid in data.NodeGuids)
                if (nodeViews.TryGetValue(guid, out var node)) group.AddElement(node);
        }

        private string SerializeSelection(IEnumerable<GraphElement> elements)
        {
            var selectedNodes = elements.OfType<ScenarioNodeView>().Select(view => view.Data)
                .Where(node => node.NodeType != ScenarioNodeType.Start && node.NodeType != ScenarioNodeType.End).ToList();
            var guids = selectedNodes.Select(node => node.Guid).ToHashSet();
            var data = new ClipboardData { nodes = selectedNodes, edges = Graph.Edges.Where(edge => guids.Contains(edge.OutputNodeGuid) && guids.Contains(edge.InputNodeGuid)).ToList() };
            return JsonUtility.ToJson(data);
        }

        private void Paste(string operationName, string json)
        {
            var clipboard = JsonUtility.FromJson<ClipboardData>(json);
            if (clipboard?.nodes == null || clipboard.nodes.Count == 0)
                return;
            var nodeMap = new Dictionary<string, NodeData>();
            var portMap = new Dictionary<string, string>();
            Mutate(operationName, () =>
            {
                foreach (var source in clipboard.nodes)
                {
                    var clone = source.CloneWithNewGuids(new Vector2(40, 40));
                    nodeMap[source.Guid] = clone;
                    for (var i = 0; i < Mathf.Min(source.OutputPorts.Count, clone.OutputPorts.Count); i++)
                        portMap[source.OutputPorts[i].Guid] = clone.OutputPorts[i].Guid;
                    Graph.Nodes.Add(clone);
                }
                foreach (var sourceEdge in clipboard.edges)
                {
                    if (nodeMap.TryGetValue(sourceEdge.OutputNodeGuid, out var output) && nodeMap.TryGetValue(sourceEdge.InputNodeGuid, out var input) && portMap.TryGetValue(sourceEdge.OutputPortGuid, out var port))
                        Graph.Edges.Add(EdgeData.Create(output.Guid, port, input.Guid));
                }
            });
            Reload();
            ClearSelection();
            foreach (var clone in nodeMap.Values)
                if (nodeViews.TryGetValue(clone.Guid, out var view)) AddToSelection(view);
        }

        private bool Matches(GraphElement element, string query, StringComparison comparison)
        {
            if (element is ScenarioNodeView node)
            {
                var scenario = node.Data.ScenarioDefinition;
                if (node.Data.DisplayName.IndexOf(query, comparison) >= 0 ||
                    scenario != null && scenario.name.IndexOf(query, comparison) >= 0 ||
                    scenario != null && scenario.Csv != null && scenario.Csv.name.IndexOf(query, comparison) >= 0)
                    return true;
                return node.Data.GameRegistry != null && node.Data.GameRegistry.TryGet(node.Data.GameId, out var game) && game.DisplayName.IndexOf(query, comparison) >= 0;
            }
            if (element is ScenarioCommentView comment)
                return comment.Data.Text.IndexOf(query, comparison) >= 0;
            return element is Group group && group.title.IndexOf(query, comparison) >= 0;
        }

        private void PersistViewTransform()
        {
            if (Graph == null || rebuilding)
                return;
            var serialized = new SerializedObject(Graph);
            serialized.Update();
            var state = serialized.FindProperty("editorState");
#pragma warning disable CS0618 // GraphViewの公開viewTransform APIはUnity 6でobsoleteですが、互換GraphViewではこれが正規経路です。
            state.FindPropertyRelative("viewPosition").vector3Value = viewTransform.position;
            state.FindPropertyRelative("viewScale").vector3Value = viewTransform.scale;
#pragma warning restore CS0618
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void UpdateGroupMembers(Group group, IEnumerable<GraphElement> elements, bool added)
        {
            if (rebuilding || Graph == null || group.userData is not string guid)
                return;
            var data = Graph.Groups.Find(candidate => candidate.Guid == guid);
            if (data == null)
                return;
            Mutate(added ? "グループへ追加" : "グループから除外", () =>
            {
                foreach (var node in elements.OfType<ScenarioNodeView>())
                {
                    if (added && !data.NodeGuids.Contains(node.Data.Guid)) data.NodeGuids.Add(node.Data.Guid);
                    if (!added) data.NodeGuids.Remove(node.Data.Guid);
                }
            });
        }

        private void QueueReload()
        {
            if (delayedReloadQueued)
                return;
            delayedReloadQueued = true;
            EditorApplication.delayCall += () =>
            {
                delayedReloadQueued = false;
                if (window != null) Reload();
            };
        }
    }
}
