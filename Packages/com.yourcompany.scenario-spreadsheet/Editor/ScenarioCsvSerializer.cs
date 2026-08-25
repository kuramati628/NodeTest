using System;
using System.Text;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    /// <summary>Google Sheetsの可変長セル配列を、引用符を考慮したCSV文字列へ変換します。</summary>
    internal static class ScenarioCsvSerializer
    {
        public static string Serialize(GoogleSheetData sheetData)
            => Serialize(sheetData?.values);

        public static string Serialize(string[][] rows)
        {
            if (rows == null || rows.Length == 0)
                return string.Empty;

            var columnCount = 0;
            foreach (var row in rows)
                columnCount = Math.Max(columnCount, row?.Length ?? 0);

            var builder = new StringBuilder();
            for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                var row = rows[rowIndex] ?? Array.Empty<string>();
                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    if (columnIndex > 0)
                        builder.Append(',');
                    AppendEscaped(builder, columnIndex < row.Length ? row[columnIndex] : string.Empty);
                }
                builder.Append('\n');
            }
            return builder.ToString();
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                builder.Append(value);
                return;
            }

            builder.Append('"');
            builder.Append(value.Replace("\"", "\"\""));
            builder.Append('"');
        }
    }
}
