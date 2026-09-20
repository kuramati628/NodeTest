using System;
using System.Collections.Generic;
using System.Linq;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    internal sealed class ScenarioLabelSection
    {
        public ScenarioLabelSection(string label)
        {
            Label = label;
        }

        public string Label { get; }
        public List<string[]> Rows { get; } = new();
        public string JumpTarget { get; set; } = string.Empty;
        public bool EndsWithGoToGame { get; set; }
        public bool EndsWithEnd { get; set; }
    }

    internal sealed class ScenarioLabelBlock
    {
        public ScenarioLabelBlock(int blockNumber, IReadOnlyList<string[]> prefixRows)
        {
            BlockNumber = blockNumber;
            PrefixRows = prefixRows;
        }

        public int BlockNumber { get; }
        public IReadOnlyList<string[]> PrefixRows { get; }
        public List<ScenarioLabelSection> Sections { get; } = new();
    }

    /// <summary>
    /// 先頭LabelとDefineLabel内のLabelを区間へ分割し、制御行をCSVから除きます。
    /// </summary>
    internal static class ScenarioLabelSplitter
    {
        private const string DefineLabelCommand = "DefineLabel";
        private const string LabelCommand = "Label";
        private const string JumpCommand = "jump";
        private const string GoToGameCommand = "GoToGame";
        private const string EndCommand = "End";
        private const int CommandColumnIndex = 1;
        private const int ValueColumnIndex = 2;

        public static bool HasDefinition(GoogleSheetData sheetData)
            => (sheetData?.values ?? Array.Empty<string[]>()).Any(row =>
                string.Equals(GetCell(row, CommandColumnIndex), DefineLabelCommand, StringComparison.Ordinal));

        public static bool HasLabel(GoogleSheetData sheetData)
            => (sheetData?.values ?? Array.Empty<string[]>()).Any(row =>
                string.Equals(GetCell(row, CommandColumnIndex), LabelCommand, StringComparison.Ordinal));

        /// <summary>先頭LabelとDefineLabelごとの分岐区間をブロックへ変換します。</summary>
        public static IReadOnlyList<ScenarioLabelBlock> SplitBlocks(GoogleSheetData sheetData)
        {
            var rows = sheetData?.values ?? Array.Empty<string[]>();
            var blocks = new List<ScenarioLabelBlock>();
            var initialPrefix = new List<string[]>();
            ScenarioLabelBlock currentBlock = null;
            IReadOnlyList<string> declaredLabels = Array.Empty<string>();
            HashSet<string> declaredSet = null;
            Dictionary<string, ScenarioLabelSection> sectionsByLabel = null;
            ScenarioLabelSection currentSection = null;
            var jumpRowIndex = -1;
            var waitingForNextDefinition = false;

            for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                var row = rows[rowIndex] ?? Array.Empty<string>();
                if (!IsMeaningfulRow(row))
                    continue;
                var command = GetCell(row, CommandColumnIndex);

                if (string.Equals(command, DefineLabelCommand, StringComparison.Ordinal))
                {
                    IReadOnlyList<string[]> prefix;
                    if (currentBlock != null)
                    {
                        if (!waitingForNextDefinition)
                            throw new InvalidOperationException($"次のDefineLabel直前にGoToGameがありません（{FormatRow(rowIndex)}）。");
                        if (currentBlock.BlockNumber != 0)
                            ValidateDeclaredSections(declaredLabels, sectionsByLabel);
                        prefix = Array.Empty<string[]>();
                    }
                    else
                    {
                        prefix = BuildPrefix(initialPrefix, rowIndex);
                        initialPrefix.Clear();
                    }
                    declaredLabels = ReadDeclaredLabels(row, rowIndex);
                    declaredSet = new HashSet<string>(declaredLabels, StringComparer.Ordinal);
                    sectionsByLabel = new Dictionary<string, ScenarioLabelSection>(StringComparer.Ordinal);
                    currentBlock = new ScenarioLabelBlock(blocks.Count(block => block.BlockNumber > 0) + 1, prefix);
                    blocks.Add(currentBlock);
                    currentSection = null;
                    jumpRowIndex = -1;
                    waitingForNextDefinition = false;
                    continue;
                }

                if (currentBlock == null)
                {
                    if (string.Equals(command, LabelCommand, StringComparison.Ordinal))
                    {
                        if (initialPrefix.Count > 0)
                            throw new InvalidOperationException($"先頭Labelより前に命令があります（{FormatRow(rowIndex)}）。");
                        currentBlock = new ScenarioLabelBlock(0, Array.Empty<string[]>());
                        blocks.Add(currentBlock);
                        currentSection = new ScenarioLabelSection(RequireSingleValue(row, rowIndex, LabelCommand));
                        currentBlock.Sections.Add(currentSection);
                        continue;
                    }
                    if (string.Equals(command, JumpCommand, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"DefineLabelブロック外に{command}があります（{FormatRow(rowIndex)}）。");
                    }
                    initialPrefix.Add((string[])row.Clone());
                    continue;
                }

                if (waitingForNextDefinition)
                    throw new InvalidOperationException($"GoToGameと次のDefineLabelの間に命令があります（{FormatRow(rowIndex)}）。");

                if (string.Equals(command, LabelCommand, StringComparison.Ordinal))
                {
                    if (currentBlock.BlockNumber == 0)
                        throw new InvalidOperationException($"先頭Label区間に複数のLabelがあります（{FormatRow(rowIndex)}）。");
                    var label = RequireSingleValue(row, rowIndex, LabelCommand);
                    if (!declaredSet.Contains(label))
                        throw new InvalidOperationException($"宣言されていないLabel『{label}』があります（{FormatRow(rowIndex)}）。");
                    if (sectionsByLabel.ContainsKey(label))
                        throw new InvalidOperationException($"Label『{label}』が重複しています（{FormatRow(rowIndex)}）。");

                    currentSection = new ScenarioLabelSection(label);
                    sectionsByLabel.Add(label, currentSection);
                    currentBlock.Sections.Add(currentSection);
                    jumpRowIndex = -1;
                    continue;
                }

                if (currentSection == null)
                    throw new InvalidOperationException($"Labelより前にシナリオ命令があります（{FormatRow(rowIndex)}）。");
                if (jumpRowIndex >= 0)
                    throw new InvalidOperationException(
                        $"jumpの後に実行可能な行があります（{FormatRow(jumpRowIndex)} → {FormatRow(rowIndex)}）。jumpはLabel区間の末尾に置いてください。");
                if (string.Equals(command, GoToGameCommand, StringComparison.Ordinal))
                {
                    if (currentBlock.BlockNumber != 0)
                        ValidateDeclaredSections(declaredLabels, sectionsByLabel);
                    currentSection.EndsWithGoToGame = true;
                    waitingForNextDefinition = true;
                    currentSection = null;
                    jumpRowIndex = -1;
                    continue;
                }

                if (string.Equals(command, JumpCommand, StringComparison.Ordinal))
                {
                    var target = RequireSingleValue(row, rowIndex, JumpCommand);
                    currentSection.JumpTarget = target;
                    jumpRowIndex = rowIndex;
                    continue;
                }

                currentSection.Rows.Add((string[])row.Clone());
                currentSection.EndsWithEnd = string.Equals(command, EndCommand, StringComparison.Ordinal);
            }

            if (currentBlock != null)
            {
                if (currentBlock.BlockNumber != 0)
                    ValidateDeclaredSections(declaredLabels, sectionsByLabel);
            }
            if (blocks.Count == 0)
                throw new InvalidOperationException("DefineLabel行が見つかりません。");
            if (waitingForNextDefinition)
                throw new InvalidOperationException("最後のGoToGameに対応するDefineLabelがありません。");
            return blocks;
        }

        public static IReadOnlyList<ScenarioLabelSection> Split(GoogleSheetData sheetData)
        {
            var rows = sheetData?.values ?? Array.Empty<string[]>();
            var declarationIndex = FindDeclarationIndex(rows);
            if (declarationIndex < 0)
                throw new InvalidOperationException("Label分割が有効ですが、DefineLabel行が見つかりません。");

            if (rows.Take(declarationIndex).Any(IsMeaningfulRow))
                throw new InvalidOperationException("DefineLabelより前にシナリオ命令があります。DefineLabelは分岐ブロックの先頭に置いてください。");

            var declaredLabels = ReadDeclaredLabels(rows[declarationIndex], declarationIndex);
            var declaredSet = new HashSet<string>(declaredLabels, StringComparer.Ordinal);
            var sectionsByLabel = new Dictionary<string, ScenarioLabelSection>(StringComparer.Ordinal);
            ScenarioLabelSection current = null;
            var jumpRowIndex = -1;

            for (var rowIndex = declarationIndex + 1; rowIndex < rows.Length; rowIndex++)
            {
                var row = rows[rowIndex] ?? Array.Empty<string>();
                if (!IsMeaningfulRow(row))
                    continue;

                var command = GetCell(row, CommandColumnIndex);
                if (string.Equals(command, DefineLabelCommand, StringComparison.Ordinal))
                    throw new InvalidOperationException($"DefineLabelが複数あります（{FormatRow(rowIndex)}）。");

                if (string.Equals(command, LabelCommand, StringComparison.Ordinal))
                {
                    var label = RequireSingleValue(row, rowIndex, LabelCommand);
                    if (!declaredSet.Contains(label))
                        throw new InvalidOperationException($"宣言されていないLabel『{label}』があります（{FormatRow(rowIndex)}）。");
                    if (sectionsByLabel.ContainsKey(label))
                        throw new InvalidOperationException($"Label『{label}』が重複しています（{FormatRow(rowIndex)}）。");

                    current = new ScenarioLabelSection(label);
                    sectionsByLabel.Add(label, current);
                    jumpRowIndex = -1;
                    continue;
                }

                if (current == null)
                    throw new InvalidOperationException($"Labelより前にシナリオ命令があります（{FormatRow(rowIndex)}）。");
                if (jumpRowIndex >= 0)
                    throw new InvalidOperationException(
                        $"jumpの後に実行可能な行があります（{FormatRow(jumpRowIndex)} → {FormatRow(rowIndex)}）。jumpはLabel区間の末尾に置いてください。");

                if (string.Equals(command, JumpCommand, StringComparison.Ordinal))
                {
                    var target = RequireSingleValue(row, rowIndex, JumpCommand);
                    current.JumpTarget = target;
                    jumpRowIndex = rowIndex;
                    continue;
                }

                current.Rows.Add((string[])row.Clone());
            }

            var missingLabels = declaredLabels.Where(label => !sectionsByLabel.ContainsKey(label)).ToArray();
            if (missingLabels.Length > 0)
                throw new InvalidOperationException($"宣言に対応するLabel行がありません: {string.Join(", ", missingLabels)}");

            return declaredLabels.Select(label => sectionsByLabel[label]).ToArray();
        }

        private static IReadOnlyList<string[]> BuildPrefix(IReadOnlyList<string[]> pendingRows, int defineRowIndex)
        {
            if (pendingRows.Count == 0)
                return Array.Empty<string[]>();

            var goToGameIndexes = pendingRows.Select((row, index) => (row, index))
                .Where(item => string.Equals(GetCell(item.row, CommandColumnIndex), GoToGameCommand, StringComparison.Ordinal))
                .Select(item => item.index)
                .ToArray();
            if (goToGameIndexes.Length != 1 || goToGameIndexes[0] != pendingRows.Count - 1)
            {
                throw new InvalidOperationException(
                    $"DefineLabel直前には1つのGoToGameが必要です（{FormatRow(defineRowIndex)}）。");
            }
            return pendingRows.Take(pendingRows.Count - 1)
                .Select(row => (string[])row.Clone())
                .ToArray();
        }

        private static void ValidateDeclaredSections(
            IReadOnlyList<string> declaredLabels,
            IReadOnlyDictionary<string, ScenarioLabelSection> sectionsByLabel)
        {
            var missingLabels = declaredLabels
                .Where(label => sectionsByLabel == null || !sectionsByLabel.ContainsKey(label))
                .ToArray();
            if (missingLabels.Length > 0)
                throw new InvalidOperationException($"宣言に対応するLabel行がありません: {string.Join(", ", missingLabels)}");
        }

        private static int FindDeclarationIndex(IReadOnlyList<string[]> rows)
        {
            var result = -1;
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (!string.Equals(GetCell(rows[rowIndex], CommandColumnIndex), DefineLabelCommand, StringComparison.Ordinal))
                    continue;
                if (result >= 0)
                    throw new InvalidOperationException($"DefineLabelが複数あります（{FormatRow(rowIndex)}）。");
                result = rowIndex;
            }
            return result;
        }

        private static IReadOnlyList<string> ReadDeclaredLabels(string[] row, int rowIndex)
        {
            var labels = (row ?? Array.Empty<string>()).Skip(ValueColumnIndex)
                .Select(value => value?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrEmpty(value))
                .ToArray();
            if (labels.Length == 0)
                throw new InvalidOperationException($"DefineLabelにラベルがありません（{FormatRow(rowIndex)}）。");

            var duplicate = labels.GroupBy(label => label, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrEmpty(duplicate))
                throw new InvalidOperationException($"DefineLabel内で『{duplicate}』が重複しています（{FormatRow(rowIndex)}）。");
            return labels;
        }

        private static string RequireSingleValue(string[] row, int rowIndex, string command)
        {
            var value = GetCell(row, ValueColumnIndex);
            if (string.IsNullOrEmpty(value))
                throw new InvalidOperationException($"{command}に対象名がありません（{FormatRow(rowIndex)}）。");
            return value;
        }

        private static bool IsMeaningfulRow(string[] row)
            => row != null && row.Any(value => !string.IsNullOrWhiteSpace(value));

        private static string GetCell(string[] row, int index)
            => row != null && index < row.Length ? row[index]?.Trim() ?? string.Empty : string.Empty;

        private static string FormatRow(int zeroBasedRowIndex) => $"{zeroBasedRowIndex + 1}行目";
    }
}
