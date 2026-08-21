using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ScenarioGraphSystem.Editor
{
    /// <summary>AssetDatabaseとBuild Settingsを利用するEditor専用検証を共通検証へ追加します。</summary>
    internal static class ScenarioGraphEditorValidator
    {
        public static List<GraphValidationError> Validate(ScenarioGraph graph)
        {
            var errors = ScenarioGraphValidator.Validate(graph);
            if (graph == null)
                return errors;

            foreach (var node in graph.Nodes.Where(node => node.NodeType == ScenarioNodeType.Game))
            {
                if (node.GameRegistry == null || !node.GameRegistry.TryGet(node.GameId, out var registration) ||
                    registration.Scene == null || !registration.Scene.IsAssigned)
                {
                    continue;
                }

                var reference = registration.Scene;
                if (string.IsNullOrWhiteSpace(reference.SceneGuid))
                {
                    errors.Add(new GraphValidationError(
                        $"ゲーム『{registration.DisplayName}』のシーンGUIDがありません。GameRegistryでシーンを選び直してください。",
                        node.Guid));
                    continue;
                }

                var resolvedPath = AssetDatabase.GUIDToAssetPath(reference.SceneGuid);
                if (string.IsNullOrEmpty(resolvedPath))
                {
                    errors.Add(new GraphValidationError(
                        $"ゲーム『{registration.DisplayName}』のシーンGUIDを解決できません。",
                        node.Guid));
                    continue;
                }

                if (!string.Equals(reference.ScenePath, resolvedPath, StringComparison.Ordinal))
                {
                    errors.Add(new GraphValidationError(
                        $"ゲーム『{registration.DisplayName}』のシーンパスが古い状態です。GameRegistryを開いて保存してください。",
                        node.Guid));
                }

                if (!EditorBuildSettings.scenes.Any(scene => scene.enabled &&
                        string.Equals(scene.path, resolvedPath, StringComparison.Ordinal)))
                {
                    errors.Add(new GraphValidationError(
                        $"ゲーム『{registration.DisplayName}』のシーンがBuild Settingsで有効になっていません。",
                        node.Guid));
                }
            }

            return errors;
        }
    }
}
