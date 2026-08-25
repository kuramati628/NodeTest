using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    /// <summary>Label分割で生成したScenarioDefinitionとScenarioGraphのノード・Edgeを同期します。</summary>
    internal static class ScenarioLabelGraphSynchronizer
    {
        private const string OwnerTagPrefix = "scenario-spreadsheet:";

        public static void Synchronize(
            ScenarioSpreadsheetImportProfile profile,
            IReadOnlyList<ScenarioDefinitionCsvImporter.GeneratedScenarioSection> outputs)
        {
            var graph = profile.TargetGraph;
            if (graph == null)
                return;

            var profilePath = AssetDatabase.GetAssetPath(profile);
            var profileGuid = AssetDatabase.AssetPathToGUID(profilePath);
            if (string.IsNullOrEmpty(profileGuid))
                throw new InvalidOperationException("Graph同期を行うImport Profileは保存済みアセットである必要があります。");

            var ownerPrefix = $"{OwnerTagPrefix}{profileGuid}:";
            var nodesByStableKey = new Dictionary<string, NodeData>(StringComparer.Ordinal);
            var firstPosition = FindGeneratedNodeOrigin(graph, ownerPrefix);

            Undo.RecordObject(graph, "Label分割シナリオを同期");
            for (var index = 0; index < outputs.Count; index++)
            {
                var output = outputs[index];
                var ownerTag = ownerPrefix + output.StableKey;
                var node = graph.Nodes.FirstOrDefault(candidate =>
                               candidate.NodeType == ScenarioNodeType.Scenario &&
                               candidate.Metadata.Tags.Contains(ownerTag))
                           ?? graph.Nodes.FirstOrDefault(candidate =>
                               candidate.NodeType == ScenarioNodeType.Scenario &&
                               candidate.ScenarioDefinition == output.Definition);
                if (node == null)
                {
                    node = NodeData.Create(
                        ScenarioNodeType.Scenario,
                        firstPosition + new Vector2(0, index * 220));
                    graph.Nodes.Add(node);
                }

                node.DisplayName = output.DisplayName;
                node.ScenarioDefinition = output.Definition;
                RemoveOwnerTags(node, ownerPrefix);
                if (!node.Metadata.Tags.Contains(ownerTag))
                    node.Metadata.Tags.Add(ownerTag);
                nodesByStableKey.Add(output.StableKey, node);
            }

            var activeNodeGuids = nodesByStableKey.Values.Select(node => node.Guid).ToHashSet();
            var staleNodes = graph.Nodes.Where(node =>
                    node.NodeType == ScenarioNodeType.Scenario &&
                    node.Metadata.Tags.Any(tag => tag.StartsWith(ownerPrefix, StringComparison.Ordinal)) &&
                    !activeNodeGuids.Contains(node.Guid))
                .ToArray();
            foreach (var stale in staleNodes)
            {
                graph.Edges.RemoveAll(edge => edge.OutputNodeGuid == stale.Guid || edge.InputNodeGuid == stale.Guid);
                graph.Nodes.Remove(stale);
                foreach (var group in graph.Groups)
                    group.NodeGuids.Remove(stale.Guid);
            }

            // Label区間の遷移だけをImporterが所有します。Labelなしシートの手動Edgeは維持します。
            var managedTransitionNodeGuids = outputs
                .Where(output => !string.IsNullOrEmpty(output.Label) && !output.ManualGameTransition)
                .Select(output => nodesByStableKey[output.StableKey].Guid)
                .ToHashSet();
            graph.Edges.RemoveAll(edge => managedTransitionNodeGuids.Contains(edge.OutputNodeGuid));
            var endNode = graph.Nodes.FirstOrDefault(node => node.NodeType == ScenarioNodeType.End);
            if (endNode == null && outputs.Any(output =>
                    !string.IsNullOrEmpty(output.Label) && !output.ManualGameTransition &&
                    string.IsNullOrEmpty(output.TransitionTargetKey)))
            {
                endNode = NodeData.Create(
                    ScenarioNodeType.End,
                    firstPosition + new Vector2(440, (outputs.Count - 1) * 220));
                graph.Nodes.Add(endNode);
            }
            for (var index = 0; index < outputs.Count; index++)
            {
                var output = outputs[index];
                if (string.IsNullOrEmpty(output.Label) || output.ManualGameTransition)
                    continue;
                var source = nodesByStableKey[output.StableKey];
                var target = !string.IsNullOrEmpty(output.TransitionTargetKey)
                    ? nodesByStableKey[output.TransitionTargetKey]
                    : endNode;

                if (target != null)
                    Connect(graph, source, source.OutputPorts[0], target);
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssetIfDirty(graph);
        }

        private static Vector2 FindGeneratedNodeOrigin(ScenarioGraph graph, string ownerPrefix)
        {
            var existing = graph.Nodes.Where(node =>
                    node.Metadata.Tags.Any(tag => tag.StartsWith(ownerPrefix, StringComparison.Ordinal)))
                .ToArray();
            if (existing.Length > 0)
                return new Vector2(existing.Min(node => node.Position.x), existing.Min(node => node.Position.y));
            if (graph.Nodes.Count == 0)
                return new Vector2(400, 100);
            var right = graph.Nodes.Max(node => node.Position.xMax);
            var top = graph.Nodes.Min(node => node.Position.yMin);
            return new Vector2(right + 180, top);
        }

        private static void Connect(ScenarioGraph graph, NodeData source, OutputPortData port, NodeData target)
        {
            if (graph.Edges.Any(edge => edge.OutputNodeGuid == source.Guid &&
                                        edge.OutputPortGuid == port.Guid &&
                                        edge.InputNodeGuid == target.Guid))
            {
                return;
            }
            graph.Edges.Add(EdgeData.Create(source.Guid, port.Guid, target.Guid));
        }

        private static void RemoveOwnerTags(NodeData node, string ownerPrefix)
            => node.Metadata.Tags.RemoveAll(tag => tag.StartsWith(ownerPrefix, StringComparison.Ordinal));
    }
}
