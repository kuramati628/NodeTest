using System.Collections.Generic;
using System.Linq;

namespace ScenarioGraphSystem
{
    /// <summary>EditorとRuntimeで共有する検証エラーです。</summary>
    public readonly struct GraphValidationError
    {
        public GraphValidationError(string message, string elementGuid = "")
        {
            Message = message;
            ElementGuid = elementGuid;
        }

        public string Message { get; }
        public string ElementGuid { get; }
        public override string ToString() => Message;
    }

    /// <summary>保存済みグラフの構造とRuntimeで判定可能な必須参照を検証します。</summary>
    public static class ScenarioGraphValidator
    {
        public static List<GraphValidationError> Validate(ScenarioGraph graph)
        {
            var errors = new List<GraphValidationError>();
            if (graph == null)
            {
                errors.Add(new GraphValidationError("グラフが設定されていません。"));
                return errors;
            }

            var startNodes = graph.Nodes.Where(node => node.NodeType == ScenarioNodeType.Start).ToList();
            if (startNodes.Count == 0)
                errors.Add(new GraphValidationError("開始ノードがありません。"));
            else if (startNodes.Count > 1)
                errors.Add(new GraphValidationError("開始ノードは1個だけ配置できます。"));

            var endNodes = graph.Nodes.Where(node => node.NodeType == ScenarioNodeType.End).ToList();
            if (endNodes.Count == 0)
                errors.Add(new GraphValidationError("終了ノードがありません。"));
            else if (endNodes.Count > 1)
                errors.Add(new GraphValidationError("終了ノードは1個だけ配置できます。"));

            foreach (var node in graph.Nodes)
            {
                if (node.NodeType == ScenarioNodeType.Start && !HasConnectedOutput(graph, node))
                    errors.Add(new GraphValidationError("開始ノードに接続先がありません。", node.Guid));

                if (node.NodeType == ScenarioNodeType.End && node.OutputPorts.Count > 0)
                    errors.Add(new GraphValidationError("終了ノードに出力ポートは設定できません。", node.Guid));
                if (node.NodeType == ScenarioNodeType.End && !graph.Edges.Any(edge => edge.InputNodeGuid == node.Guid))
                    errors.Add(new GraphValidationError("終了ノードに入力接続がありません。", node.Guid));

                if (node.NodeType == ScenarioNodeType.Scenario)
                {
                    if (node.ScenarioDefinition == null)
                        errors.Add(new GraphValidationError($"シナリオ『{node.DisplayName}』にScenarioDefinitionが設定されていません。", node.Guid));
                    else if (node.ScenarioDefinition.Csv == null)
                        errors.Add(new GraphValidationError($"ScenarioDefinition『{node.ScenarioDefinition.name}』にCSVが設定されていません。", node.Guid));
                    if (!HasConnectedOutput(graph, node))
                        errors.Add(new GraphValidationError($"シナリオ『{node.DisplayName}』の出力が未接続です。", node.Guid));
                }

                if (node.NodeType != ScenarioNodeType.Game)
                    continue;

                if (node.GameRegistry == null || string.IsNullOrEmpty(node.GameId))
                    errors.Add(new GraphValidationError($"ゲーム『{node.DisplayName}』が選択されていません。", node.Guid));
                else if (!node.GameRegistry.TryGet(node.GameId, out var registration))
                    errors.Add(new GraphValidationError($"ゲーム『{node.DisplayName}』のゲームIDを解決できません。", node.Guid));
                else if (registration.Scene == null || !registration.Scene.IsAssigned)
                    errors.Add(new GraphValidationError($"ゲーム『{registration.DisplayName}』のシーンが未設定です。", node.Guid));

                if (node.GameRegistry != null && node.GameRegistry.Games
                    .Where(game => !string.IsNullOrWhiteSpace(game.DisplayName))
                    .GroupBy(game => game.DisplayName).Any(group => group.Count() > 1))
                {
                    errors.Add(new GraphValidationError($"GameRegistry『{node.GameRegistry.name}』に重複するゲーム名があります。", node.Guid));
                }

                if (node.AttachedData == null)
                {
                    errors.Add(new GraphValidationError($"ゲーム『{node.DisplayName}』にアタッチデータが設定されていません。", node.Guid));
                }
                else if (!ScenarioBranchResolverUtility.TryGetBranchNames(node.AttachedData, node.BranchResolver,
                             out var expectedBranches, out var branchError))
                {
                    errors.Add(new GraphValidationError(branchError, node.Guid));
                }
                else
                {
                    foreach (var branch in expectedBranches.Where(branch => node.OutputPorts.All(port => port.BranchName != branch)))
                        errors.Add(new GraphValidationError($"ゲーム『{node.DisplayName}』に分岐『{branch}』の出力ポートがありません。", node.Guid));
                    foreach (var port in node.OutputPorts.Where(port => !expectedBranches.Contains(port.BranchName)))
                        errors.Add(new GraphValidationError($"ゲーム『{node.DisplayName}』にアタッチデータに存在しない出力『{port.DisplayName}』があります。", node.Guid));
                }

                foreach (var duplicate in node.OutputPorts.GroupBy(port => port.BranchName).Where(group => group.Count() > 1))
                    errors.Add(new GraphValidationError($"ゲーム『{node.DisplayName}』で分岐『{duplicate.Key}』が重複しています。", node.Guid));
                if (node.OutputPorts.Count == 0)
                    errors.Add(new GraphValidationError($"ゲーム『{node.DisplayName}』に出力ポートがありません。", node.Guid));
                foreach (var port in node.OutputPorts.Where(port => !graph.Edges.Any(edge =>
                             edge.OutputNodeGuid == node.Guid && edge.OutputPortGuid == port.Guid)))
                {
                    errors.Add(new GraphValidationError($"ゲーム『{node.DisplayName}』の出力『{port.DisplayName}』が未接続です。", node.Guid));
                }
            }

            foreach (var edge in graph.Edges)
            {
                var source = graph.FindNode(edge.OutputNodeGuid);
                if (source == null || graph.FindNode(edge.InputNodeGuid) == null)
                    errors.Add(new GraphValidationError("存在しないノードを参照するEdgeがあります。", edge.Guid));
                else if (source.OutputPorts.All(port => port.Guid != edge.OutputPortGuid))
                    errors.Add(new GraphValidationError("存在しない出力ポートを参照するEdgeがあります。", edge.Guid));
            }
            return errors;
        }

        private static bool HasConnectedOutput(ScenarioGraph graph, NodeData node)
        {
            return node.OutputPorts.Count > 0 && graph.Edges.Any(edge => edge.OutputNodeGuid == node.Guid);
        }
    }
}
