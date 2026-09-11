using System;
using System.Linq;
using R3;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>
    /// アセットへ状態を書き込まず、次に実行するノードの提示とグラフ上の分岐だけを管理します。
    /// シナリオ再生、ゲーム実行、Scene遷移は呼び出し側の責務です。
    /// </summary>
    public sealed class ScenarioGraphRunner : IDisposable
    {
        private readonly Subject<NodeData> nodeChanged = new();
        private readonly Subject<string> error = new();
        private readonly Subject<Unit> completed = new();
        private ScenarioGraph graph;
        private NodeData currentNode;
        private bool running;
        private bool singleNodeDebug;

        public Observable<NodeData> OnNodeChanged => nodeChanged;
        public Observable<string> OnError => error;
        public Observable<Unit> OnCompleted => completed;
        public bool IsRunning => running;

        /// <summary>グラフを開始ノードから開始し、最初の実行対象ノードを通知します。</summary>
        public void Start(ScenarioGraph targetGraph)
        {
            Reset();
            graph = targetGraph;
            var validationErrors = ScenarioGraphValidator.Validate(graph);
            if (validationErrors.Count > 0)
            {
                Fail(validationErrors[0].Message);
                return;
            }

            running = true;
            EnterNode(GetStartNode());
        }

        /// <summary>シナリオまたはゲームノードを単体デバッグ対象として通知します。</summary>
        public void StartAtNode(ScenarioGraph targetGraph, string nodeGuid)
        {
            Reset();
            graph = targetGraph;
            var node = graph != null ? graph.FindNode(nodeGuid) : null;
            if (node == null || node.NodeType is not (ScenarioNodeType.Scenario or ScenarioNodeType.Game))
            {
                Fail("デバッグ対象のシナリオまたはゲームノードを解決できません。");
                return;
            }

            singleNodeDebug = true;
            running = true;
            EnterNode(node);
        }

        /// <summary>現在設定されているグラフの開始ノードを返します。</summary>
        public NodeData GetStartNode() => graph != null ? graph.GetStartNode() : null;

        /// <summary>現在のノードを返します。完了直後はEndノードを返します。</summary>
        public NodeData GetCurrentNode() => currentNode;

        /// <summary>現在のシナリオノードが正常完了したものとして、単一の出力Edgeへ進みます。</summary>
        public void CompleteScenarioNode()
        {
            if (!running || currentNode == null || currentNode.NodeType != ScenarioNodeType.Scenario)
                return;

            if (singleNodeDebug)
            {
                Complete();
                return;
            }

            AdvanceSingleOutput(currentNode, "シナリオノードの出力が未接続です。");
        }

        /// <summary>現在のゲームノードから、結果名に対応する出力Edgeへ進みます。</summary>
        public void SubmitGameResult(string branchName)
        {
            if (!running || currentNode == null || currentNode.NodeType != ScenarioNodeType.Game)
                return;

            if (string.IsNullOrWhiteSpace(branchName))
            {
                Fail("ゲームから有効な分岐名が返されませんでした。");
                return;
            }
            if (!ScenarioBranchResolverUtility.TryGetBranchNames(
                    currentNode.AttachedData, currentNode.BranchResolver, out var branchNames, out var branchError))
            {
                Fail(branchError);
                return;
            }
            if (!branchNames.Contains(branchName))
            {
                Fail($"ゲームからアタッチデータにない分岐『{branchName}』が返されました。");
                return;
            }

            if (singleNodeDebug)
            {
                Complete();
                return;
            }

            var port = currentNode.OutputPorts.FirstOrDefault(candidate => candidate.BranchName == branchName);
            if (port == null)
            {
                Fail($"分岐『{branchName}』に対応する出力ポートがありません。");
                return;
            }

            var edge = graph.Edges.FirstOrDefault(candidate =>
                candidate.OutputNodeGuid == currentNode.Guid && candidate.OutputPortGuid == port.Guid);
            if (edge == null)
            {
                Fail($"分岐『{branchName}』に対応するEdgeがありません。");
                return;
            }

            EnterNode(graph.FindNode(edge.InputNodeGuid));
        }

        /// <summary>実行状態を破棄します。外部で実行中の処理は呼び出し側で停止してください。</summary>
        public void Reset()
        {
            running = false;
            singleNodeDebug = false;
            graph = null;
            currentNode = null;
        }

        private void EnterNode(NodeData node)
        {
            if (!running)
                return;
            if (node == null)
            {
                Fail("遷移先ノードを解決できません。");
                return;
            }

            currentNode = node;
            switch (node.NodeType)
            {
                case ScenarioNodeType.Start:
                    AdvanceSingleOutput(node, "開始ノードが未接続です。");
                    break;
                case ScenarioNodeType.Scenario:
                case ScenarioNodeType.Game:
                    nodeChanged.OnNext(node);
                    break;
                case ScenarioNodeType.End:
                    Complete();
                    break;
                default:
                    Fail($"未対応のノード種別です: {node.NodeType}");
                    break;
            }
        }

        private void AdvanceSingleOutput(NodeData node, string message)
        {
            var edge = graph.Edges.FirstOrDefault(candidate => candidate.OutputNodeGuid == node.Guid);
            if (edge == null)
            {
                Fail(message);
                return;
            }
            EnterNode(graph.FindNode(edge.InputNodeGuid));
        }

        private void Fail(string message)
        {
            running = false;
            singleNodeDebug = false;
            Debug.LogError($"[ScenarioGraphRunner] {message}");
            error.OnNext(message);
        }

        private void Complete()
        {
            if (!running)
                return;
            running = false;
            singleNodeDebug = false;
            completed.OnNext(Unit.Default);
        }

        public void Dispose()
        {
            Reset();
            nodeChanged.Dispose();
            completed.Dispose();
            error.Dispose();
        }
    }
}
