using System;
using R3;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>
    /// Sceneやゲームを起動せず、外部ホストからRunnerを進める方法を確認するモックです。
    /// </summary>
    public sealed class ScenarioGraphMockRunner : MonoBehaviour, IScenarioGraphDebugHost
    {
        [SerializeField] private ScenarioGraph graph;
        [SerializeField, Min(0f)] private float scenarioCompletionDelaySeconds = 0.25f;
        [SerializeField, Min(0)] private int autoCompleteScenarioCount = 1;
        [SerializeField] private bool autoStart = true;

        private ScenarioGraphRunner runner;
        private CompositeDisposable subscriptions;
        private IDisposable scenarioTimer;
        private int scenarioCount;

        private void Start()
        {
            if (autoStart)
                StartGraph();
        }

        [ContextMenu("モックグラフを開始")]
        public void StartGraph()
        {
            if (graph == null)
            {
                DisposeRunner();
                Debug.LogError("[ScenarioGraphMockRunner] ScenarioGraphが設定されていません。", this);
                return;
            }
            StartRunner(graph, null);
        }

        public bool CanDebugNode(ScenarioGraph targetGraph, NodeData node, out string reason)
        {
            if (targetGraph == null || node == null)
            {
                reason = "グラフまたはノードが設定されていません。";
                return false;
            }
            if (node.NodeType is not (ScenarioNodeType.Scenario or ScenarioNodeType.Game))
            {
                reason = "単体デバッグできるのはシナリオまたはゲームノードです。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public void DebugNode(ScenarioGraph targetGraph, NodeData node)
        {
            StartRunner(targetGraph, node.Guid);
        }

        private void StartRunner(ScenarioGraph targetGraph, string nodeGuid)
        {
            DisposeRunner();
            scenarioCount = 0;
            runner = new ScenarioGraphRunner();
            subscriptions = new CompositeDisposable();
            runner.OnNodeChanged.Subscribe(HandleNode).AddTo(subscriptions);
            runner.OnError.Subscribe(message => Debug.LogError($"[ScenarioGraphMockRunner] {message}", this))
                .AddTo(subscriptions);
            runner.OnCompleted.Subscribe(_ => Debug.Log("[ScenarioGraphMockRunner] 実行完了", this))
                .AddTo(subscriptions);

            if (string.IsNullOrEmpty(nodeGuid))
                runner.Start(targetGraph);
            else
                runner.StartAtNode(targetGraph, nodeGuid);
        }

        private void HandleNode(NodeData node)
        {
            Debug.Log($"[ScenarioGraphMockRunner] 実行対象: {node.DisplayName}", this);
            scenarioTimer?.Dispose();
            scenarioTimer = null;

            if (node.NodeType != ScenarioNodeType.Scenario)
                return;

            scenarioCount++;
            if (scenarioCount > autoCompleteScenarioCount)
                return;

            scenarioTimer = Observable.Timer(
                    TimeSpan.FromSeconds(scenarioCompletionDelaySeconds),
                    UnityTimeProvider.Update)
                .Subscribe(_ => CompleteCurrentScenario());
        }

        [ContextMenu("現在のシナリオを完了")]
        public void CompleteCurrentScenario()
        {
            scenarioTimer?.Dispose();
            scenarioTimer = null;
            runner?.CompleteScenarioNode();
        }

        [ContextMenu("現在のゲームを先頭の結果で完了")]
        public void CompleteCurrentGameWithFirstResult()
        {
            var node = runner?.GetCurrentNode();
            if (node == null || node.NodeType != ScenarioNodeType.Game || node.OutputPorts.Count == 0)
            {
                Debug.LogError("[ScenarioGraphMockRunner] 実行中のゲームノードに結果ポートがありません。", this);
                return;
            }
            runner.SubmitGameResult(node.OutputPorts[0].BranchName);
        }

        private void OnDestroy()
        {
            DisposeRunner();
        }

        private void DisposeRunner()
        {
            scenarioTimer?.Dispose();
            scenarioTimer = null;
            subscriptions?.Dispose();
            subscriptions = null;
            runner?.Dispose();
            runner = null;
        }
    }
}
