using System;
using System.IO;
using System.Linq;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    internal readonly struct ScenarioGeneratedPaths
    {
        public ScenarioGeneratedPaths(string csvPath, string definitionPath)
        {
            CsvPath = csvPath;
            DefinitionPath = definitionPath;
        }

        public string CsvPath { get; }
        public string DefinitionPath { get; }
    }

    /// <summary>Spreadsheet/CSV・Asset/Sheet/Labelの生成パス規則を一元管理します。</summary>
    internal static class ScenarioGeneratedPathBuilder
    {
        private static readonly char[] InvalidSegmentCharacters =
            Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
                .Distinct().ToArray();

        public static string BuildSpreadsheetRoot(string outputFolder, string spreadsheetName)
        {
            var root = NormalizeOutputFolder(outputFolder);
            return $"{root}/{SanitizeSegment(spreadsheetName, "Spreadsheet名")}";
        }

        public static ScenarioGeneratedPaths Build(
            string outputFolder,
            string spreadsheetName,
            string sheetName,
            string label)
            => Build(outputFolder, spreadsheetName, sheetName, string.Empty, label);

        public static ScenarioGeneratedPaths Build(
            string outputFolder,
            string spreadsheetName,
            string sheetName,
            string blockName,
            string label)
        {
            var spreadsheetRoot = BuildSpreadsheetRoot(outputFolder, spreadsheetName);
            var safeSheet = SanitizeSegment(sheetName, "シート名");
            if (!string.IsNullOrWhiteSpace(blockName))
            {
                var safeBlock = SanitizeSegment(blockName, "ブロック名");
                if (string.IsNullOrWhiteSpace(label))
                {
                    return new ScenarioGeneratedPaths(
                        $"{spreadsheetRoot}/CSV/{safeSheet}/{safeBlock}.csv",
                        $"{spreadsheetRoot}/Asset/{safeSheet}/{safeBlock}.asset");
                }

                var safeBlockLabel = SanitizeSegment(label, "Label名");
                return new ScenarioGeneratedPaths(
                    $"{spreadsheetRoot}/CSV/{safeSheet}/{safeBlock}/{safeBlockLabel}.csv",
                    $"{spreadsheetRoot}/Asset/{safeSheet}/{safeBlock}/{safeBlockLabel}.asset");
            }
            if (string.IsNullOrWhiteSpace(label))
            {
                return new ScenarioGeneratedPaths(
                    $"{spreadsheetRoot}/CSV/{safeSheet}.csv",
                    $"{spreadsheetRoot}/Asset/{safeSheet}.asset");
            }

            var safeLabel = SanitizeSegment(label, "Label名");
            return new ScenarioGeneratedPaths(
                $"{spreadsheetRoot}/CSV/{safeSheet}/{safeLabel}.csv",
                $"{spreadsheetRoot}/Asset/{safeSheet}/{safeLabel}.asset");
        }

        public static string NormalizeOutputFolder(string outputFolder)
        {
            var folder = (outputFolder ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');
            if (!string.Equals(folder, "Assets", StringComparison.Ordinal) &&
                !folder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("出力フォルダーはAssetsまたはAssets/以下を指定してください。");
            }
            if (folder.Split('/').Any(segment => segment == ".."))
                throw new InvalidOperationException("出力フォルダーに相対移動 '..' は使用できません。");
            return folder;
        }

        private static string SanitizeSegment(string value, string sourceName)
        {
            var characters = (value ?? string.Empty).Trim()
                .Select(character => character < ' ' || InvalidSegmentCharacters.Contains(character) ? '_' : character)
                .ToArray();
            var result = new string(characters).Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(result) || result is "." or "..")
                throw new InvalidOperationException($"{sourceName}『{value}』を安全なパス名へ変換できません。");
            return result;
        }
    }
}
