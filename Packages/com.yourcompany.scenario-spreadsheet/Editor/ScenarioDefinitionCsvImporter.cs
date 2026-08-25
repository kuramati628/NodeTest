using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    /// <summary>Spreadsheet全体の取得からCSV・ScenarioDefinition・Graphの同期までを実行します。</summary>
    public static class ScenarioDefinitionCsvImporter
    {
        /// <summary>
        /// Import Profileの設定に従ってCSVを更新します。
        /// 取得や変換に失敗した場合は、既存CSVとScenarioDefinitionの参照を変更しません。
        /// </summary>
        public static async UniTask<ScenarioSpreadsheetImportResult> ImportAsync(
            ScenarioSpreadsheetImportProfile profile,
            CancellationToken cancellationToken = default)
        {
            var validationMessage = Validate(profile);
            if (!string.IsNullOrEmpty(validationMessage))
                return ScenarioSpreadsheetImportResult.Failure(validationMessage);

            try
            {
                var apiKey = profile.Credential.ResolveApiKey();
                var client = new GoogleSheetsClient();
                var spreadsheet = await client.FetchSpreadsheetAsync(
                    apiKey,
                    profile.SpreadsheetId,
                    profile.CellRange,
                    profile.ExcludedSheetNames,
                    cancellationToken);
                var outputs = BuildOutputs(profile, spreadsheet);
                if (outputs.Count == 0)
                    return ScenarioSpreadsheetImportResult.Failure("対象シートに出力可能なセルがありません。");

                foreach (var output in outputs)
                    WriteCsvAsset(output.CsvPath, output.Csv);

                foreach (var output in outputs)
                {
                    var csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(output.CsvPath);
                    if (csvAsset == null)
                        return ScenarioSpreadsheetImportResult.Failure($"生成したCSVをTextAssetとして読み込めませんでした: {output.CsvPath}");
                    output.Definition = LoadOrCreateDefinition(output.DefinitionPath);
                    AssignCsv(output.Definition, csvAsset);
                }

                if (profile.TargetGraph != null)
                    ScenarioLabelGraphSynchronizer.Synchronize(profile, outputs);

                return ScenarioSpreadsheetImportResult.Success(outputs.Select(output => output.CsvPath).ToArray());
            }
            catch (OperationCanceledException)
            {
                return ScenarioSpreadsheetImportResult.Failure("Spreadsheetの取得をキャンセルしました。");
            }
            catch (Exception exception)
            {
                return ScenarioSpreadsheetImportResult.Failure($"Spreadsheetのインポートに失敗しました: {exception.Message}");
            }
        }

        /// <summary>Import Profileに不足または危険なパス指定がないか検証します。</summary>
        public static string Validate(ScenarioSpreadsheetImportProfile profile)
        {
            if (profile == null)
                return "Import Profileが設定されていません。";
            if (profile.Credential == null)
                return "GoogleSheetsCredentialが設定されていません。";
            if (string.IsNullOrWhiteSpace(profile.Credential.ResolveApiKey()))
                return $"APIキーがありません。環境変数『{profile.Credential.EnvironmentVariableName}』またはCredentialを設定してください。";
            if (string.IsNullOrWhiteSpace(profile.SpreadsheetId))
                return "Spreadsheet IDが設定されていません。";
            if (profile.CellRange.Contains("!"))
                return "Cell Rangeにはシート名を含めず、A1:Z1000の形式で指定してください。";

            try
            {
                ScenarioGeneratedPathBuilder.NormalizeOutputFolder(profile.OutputFolder);
                if (profile.TargetGraph != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(profile)))
                    return "Graph同期を行うImport Profileは保存済みアセットである必要があります。";
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
            return string.Empty;
        }

        private static List<GeneratedScenarioSection> BuildOutputs(
            ScenarioSpreadsheetImportProfile profile,
            GoogleSpreadsheetData spreadsheet)
        {
            var outputs = new List<GeneratedScenarioSection>();
            var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheet in spreadsheet.sheets ?? Array.Empty<GoogleSheetData>())
            {
                if (!HasOutputRows(sheet?.values))
                    continue;

                if (!ScenarioLabelSplitter.HasDefinition(sheet))
                {
                    AddOutput(
                        profile, spreadsheet, sheet, 0, string.Empty, string.Empty, string.Empty,
                        false, ScenarioCsvSerializer.Serialize(sheet.values), outputs, usedPaths);
                    continue;
                }

                var blocks = ScenarioLabelSplitter.SplitBlocks(sheet);
                foreach (var block in blocks)
                {
                    var blockName = $"{sheet.title}-{block.BlockNumber}";
                    if (block.PrefixRows.Count > 0)
                    {
                        AddOutput(
                            profile, spreadsheet, sheet, block.BlockNumber, blockName, string.Empty, string.Empty,
                            false, ScenarioCsvSerializer.Serialize(block.PrefixRows.ToArray()), outputs, usedPaths);
                    }

                    for (var index = 0; index < block.Sections.Count; index++)
                    {
                        var section = block.Sections[index];
                        var csv = ScenarioCsvSerializer.Serialize(section.Rows.ToArray());
                        if (string.IsNullOrEmpty(csv))
                            throw new InvalidOperationException($"シート『{sheet.title}』のLabel『{section.Label}』に出力可能なシナリオ命令がありません。");

                        var targetLabel = !string.IsNullOrEmpty(section.JumpTarget)
                            ? section.JumpTarget
                            : index + 1 < block.Sections.Count ? block.Sections[index + 1].Label : string.Empty;
                        var targetKey = string.IsNullOrEmpty(targetLabel)
                            ? string.Empty
                            : BuildStableKey(spreadsheet.spreadsheetId, sheet.sheetId, block.BlockNumber, targetLabel);
                        AddOutput(
                            profile, spreadsheet, sheet, block.BlockNumber, blockName, section.Label, targetKey,
                            section.EndsWithGoToGame, csv, outputs, usedPaths);
                    }
                }
            }
            return outputs;
        }

        private static void AddOutput(
            ScenarioSpreadsheetImportProfile profile,
            GoogleSpreadsheetData spreadsheet,
            GoogleSheetData sheet,
            int blockNumber,
            string blockName,
            string label,
            string transitionTargetKey,
            bool manualGameTransition,
            string csv,
            ICollection<GeneratedScenarioSection> outputs,
            ISet<string> usedPaths)
        {
            var paths = ScenarioGeneratedPathBuilder.Build(
                profile.OutputFolder,
                spreadsheet.title,
                sheet.title,
                blockName,
                label);
            if (!usedPaths.Add(paths.CsvPath) || !usedPaths.Add(paths.DefinitionPath))
                throw new InvalidOperationException($"生成パスが重複します。シート名またはLabel名を確認してください: {sheet.title}/{label}");

            outputs.Add(new GeneratedScenarioSection(
                BuildStableKey(spreadsheet.spreadsheetId, sheet.sheetId, blockNumber, label),
                sheet.sheetId,
                sheet.title,
                blockNumber,
                blockName,
                label,
                transitionTargetKey,
                manualGameTransition,
                paths.CsvPath,
                paths.DefinitionPath,
                csv));
        }

        private static string BuildStableKey(string spreadsheetId, int sheetId, int blockNumber, string label)
            => $"{spreadsheetId}:{sheetId}:{blockNumber}:{(string.IsNullOrEmpty(label) ? "@main" : label)}";

        private static bool HasOutputRows(IEnumerable<string[]> rows)
            => rows != null && rows.Any(row => row != null && row.Any(cell => !string.IsNullOrWhiteSpace(cell)));

        private static void WriteCsvAsset(string assetPath, string csv)
        {
            var relativePath = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.Combine(Application.dataPath, relativePath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("CSV出力フォルダーを解決できませんでした。");

            Directory.CreateDirectory(directory);
            // 同一パスへ上書きすることで.metaとTextAssetのGUIDを維持します。
            File.WriteAllText(absolutePath, csv, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void AssignCsv(ScenarioDefinition definition, TextAsset csvAsset)
        {
            Undo.RecordObject(definition, "ScenarioDefinitionのCSVを更新");
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.Update();
            var csvProperty = serializedDefinition.FindProperty("csv");
            if (csvProperty == null)
                throw new InvalidOperationException("ScenarioDefinitionのCSVプロパティを解決できません。");

            csvProperty.objectReferenceValue = csvAsset;
            serializedDefinition.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        private static ScenarioDefinition LoadOrCreateDefinition(string assetPath)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(assetPath);
            if (definition != null)
                return definition;
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                throw new InvalidOperationException($"ScenarioDefinitionの生成先に別種のアセットがあります: {assetPath}");

            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            EnsureAssetFolder(directory);
            definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
            definition.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(definition, assetPath);
            return definition;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
                return;
            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        internal sealed class GeneratedScenarioSection
        {
            public GeneratedScenarioSection(
                string stableKey,
                int sheetId,
                string sheetName,
                int blockNumber,
                string blockName,
                string label,
                string transitionTargetKey,
                bool manualGameTransition,
                string csvPath,
                string definitionPath,
                string csv)
            {
                StableKey = stableKey;
                SheetId = sheetId;
                SheetName = sheetName;
                BlockNumber = blockNumber;
                BlockName = blockName;
                Label = label;
                TransitionTargetKey = transitionTargetKey;
                ManualGameTransition = manualGameTransition;
                CsvPath = csvPath;
                DefinitionPath = definitionPath;
                Csv = csv;
            }

            public string StableKey { get; }
            public int SheetId { get; }
            public string SheetName { get; }
            public int BlockNumber { get; }
            public string BlockName { get; }
            public string Label { get; }
            public string DisplayName => string.IsNullOrEmpty(Label)
                ? string.IsNullOrEmpty(BlockName) ? SheetName : BlockName
                : string.IsNullOrEmpty(BlockName) ? Label : $"{BlockName}/{Label}";
            public string TransitionTargetKey { get; }
            public bool ManualGameTransition { get; }
            public string CsvPath { get; }
            public string DefinitionPath { get; }
            public string Csv { get; }
            public ScenarioDefinition Definition { get; set; }
        }
    }

    /// <summary>Spreadsheetインポートの成否と出力先を返す結果です。</summary>
    public readonly struct ScenarioSpreadsheetImportResult
    {
        private ScenarioSpreadsheetImportResult(bool succeeded, string message, IReadOnlyList<string> assetPaths)
        {
            Succeeded = succeeded;
            Message = message;
            AssetPaths = assetPaths ?? Array.Empty<string>();
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public IReadOnlyList<string> AssetPaths { get; }
        public string AssetPath => AssetPaths.FirstOrDefault() ?? string.Empty;

        public static ScenarioSpreadsheetImportResult Success(IReadOnlyList<string> assetPaths)
            => new(true, $"CSVを{assetPaths.Count}件更新しました: {string.Join(", ", assetPaths)}", assetPaths);

        public static ScenarioSpreadsheetImportResult Failure(string message)
            => new(false, message, Array.Empty<string>());
    }
}
