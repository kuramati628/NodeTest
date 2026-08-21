using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    /// <summary>Spreadsheet取得からCSV更新、ScenarioDefinitionへの割り当てまでを実行します。</summary>
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
                var sheetData = await client.FetchAsync(
                    apiKey,
                    profile.SpreadsheetId,
                    profile.SheetGid,
                    profile.FallbackSheetName,
                    profile.CellRange,
                    cancellationToken);
                var csv = ScenarioCsvSerializer.Serialize(sheetData);
                if (string.IsNullOrEmpty(csv))
                    return ScenarioSpreadsheetImportResult.Failure("Spreadsheetに出力可能なセルがありません。");

                var assetPath = BuildAssetPath(profile);
                WriteCsvAsset(assetPath, csv);
                var csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (csvAsset == null)
                    return ScenarioSpreadsheetImportResult.Failure($"更新したCSVをTextAssetとして読み込めませんでした: {assetPath}");

                AssignCsv(profile.TargetDefinition, csvAsset);
                return ScenarioSpreadsheetImportResult.Success(assetPath);
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
            if (profile.TargetDefinition == null)
                return "更新対象のScenarioDefinitionが設定されていません。";
            if (profile.Credential == null)
                return "GoogleSheetsCredentialが設定されていません。";
            if (string.IsNullOrWhiteSpace(profile.Credential.ResolveApiKey()))
                return $"APIキーがありません。環境変数『{profile.Credential.EnvironmentVariableName}』またはCredentialを設定してください。";
            if (string.IsNullOrWhiteSpace(profile.SpreadsheetId))
                return "Spreadsheet IDが設定されていません。";
            if (profile.SheetGid < 0 && string.IsNullOrWhiteSpace(profile.FallbackSheetName))
                return "Sheet GIDまたはフォールバックシート名を設定してください。";
            if (profile.CellRange.Contains("!"))
                return "Cell Rangeにはシート名を含めず、A1:Z1000の形式で指定してください。";

            try
            {
                BuildAssetPath(profile);
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
            return string.Empty;
        }

        private static string BuildAssetPath(ScenarioSpreadsheetImportProfile profile)
        {
            var folder = profile.OutputFolder.Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(folder, "Assets", StringComparison.Ordinal) &&
                !folder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("出力フォルダーはAssetsまたはAssets/以下を指定してください。");
            }
            if (folder.Split('/').Contains(".."))
                throw new InvalidOperationException("出力フォルダーに相対移動 '..' は使用できません。");

            var fileName = profile.OutputFileName;
            if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidOperationException("出力ファイル名が不正です。");
            }
            if (!string.Equals(Path.GetExtension(fileName), ".csv", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("出力ファイル名の拡張子は.csvにしてください。");

            return $"{folder}/{fileName}";
        }

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
    }

    /// <summary>Spreadsheetインポートの成否と出力先を返す結果です。</summary>
    public readonly struct ScenarioSpreadsheetImportResult
    {
        private ScenarioSpreadsheetImportResult(bool succeeded, string message, string assetPath)
        {
            Succeeded = succeeded;
            Message = message;
            AssetPath = assetPath;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public string AssetPath { get; }

        public static ScenarioSpreadsheetImportResult Success(string assetPath)
            => new(true, $"CSVを更新しました: {assetPath}", assetPath);

        public static ScenarioSpreadsheetImportResult Failure(string message)
            => new(false, message, string.Empty);
    }
}
