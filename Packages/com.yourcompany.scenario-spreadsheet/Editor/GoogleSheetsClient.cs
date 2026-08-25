using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    /// <summary>Google Sheets Values APIからシートのセル値を取得するEditor用クライアントです。</summary>
    internal sealed class GoogleSheetsClient
    {
        private const string ApiBaseUrl = "https://sheets.googleapis.com/v4/spreadsheets";

        /// <summary>Spreadsheet名と、除外対象以外のすべてのGRIDシートを取得します。</summary>
        public async UniTask<GoogleSpreadsheetData> FetchSpreadsheetAsync(
            string apiKey,
            string spreadsheetId,
            string cellRange,
            IEnumerable<string> excludedSheetNames,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Google Sheets APIキーが設定されていません。");
            if (string.IsNullOrWhiteSpace(spreadsheetId))
                throw new InvalidOperationException("Spreadsheet IDが設定されていません。");

            var fields = Uri.EscapeDataString("properties.title,sheets.properties(sheetId,title,sheetType)");
            var metadataUrl = $"{ApiBaseUrl}/{Uri.EscapeDataString(spreadsheetId)}?fields={fields}&key={Uri.EscapeDataString(apiKey)}";
            var metadataJson = await GetJsonAsync(metadataUrl, cancellationToken);
            var metadata = JsonConvert.DeserializeObject<GoogleSpreadsheetMetadata>(metadataJson)
                           ?? throw new InvalidOperationException("Spreadsheetのメタデータを解析できませんでした。");
            var spreadsheetTitle = metadata.properties?.title?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(spreadsheetTitle))
                throw new InvalidOperationException("Spreadsheet名を取得できませんでした。");

            var targets = SelectTargetSheets(metadata, excludedSheetNames);
            if (targets.Length == 0)
                throw new InvalidOperationException("インポート対象のシートがありません。");

            var sheets = new List<GoogleSheetData>(targets.Length);
            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var a1Range = BuildA1Range(target.title, cellRange);
                var url = $"{ApiBaseUrl}/{Uri.EscapeDataString(spreadsheetId)}/values/{Uri.EscapeDataString(a1Range)}?key={Uri.EscapeDataString(apiKey)}";
                var json = await GetJsonAsync(url, cancellationToken);
                var values = JsonConvert.DeserializeObject<GoogleSheetData>(json)
                             ?? throw new InvalidOperationException($"シート『{target.title}』の応答を解析できませんでした。");
                values.sheetId = target.sheetId;
                values.title = target.title.Trim();
                sheets.Add(values);
            }

            return new GoogleSpreadsheetData
            {
                spreadsheetId = spreadsheetId,
                title = spreadsheetTitle,
                sheets = sheets.ToArray()
            };
        }

        internal static GoogleSheetProperties[] SelectTargetSheets(
            GoogleSpreadsheetMetadata metadata,
            IEnumerable<string> excludedSheetNames)
        {
            var excluded = new HashSet<string>(
                (excludedSheetNames ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim()),
                StringComparer.OrdinalIgnoreCase);
            return (metadata?.sheets ?? Array.Empty<GoogleSheetWrapper>())
                .Select(sheet => sheet?.properties)
                .Where(properties => properties != null &&
                                     (string.IsNullOrEmpty(properties.sheetType) || properties.sheetType == "GRID") &&
                                     !string.IsNullOrWhiteSpace(properties.title) &&
                                     !excluded.Contains(properties.title.Trim()))
                .ToArray();
        }

        private static async UniTask<string> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"Google Sheets APIの取得に失敗しました（HTTP {request.responseCode}）: {request.error}");
            }

            if (string.IsNullOrWhiteSpace(request.downloadHandler.text))
                throw new InvalidOperationException("Google Sheets APIから空の応答が返されました。");
            return request.downloadHandler.text;
        }

        private static string BuildA1Range(string sheetName, string cellRange)
        {
            var escapedSheetName = sheetName.Replace("'", "''");
            return string.IsNullOrWhiteSpace(cellRange)
                ? $"'{escapedSheetName}'"
                : $"'{escapedSheetName}'!{cellRange.Trim()}";
        }
    }

    /// <summary>Google Sheets Values APIのセル値応答です。</summary>
    [Serializable]
    internal sealed class GoogleSheetData
    {
        public int sheetId;
        public string title;
        public string[][] values;
    }

    [Serializable]
    internal sealed class GoogleSpreadsheetData
    {
        public string spreadsheetId;
        public string title;
        public GoogleSheetData[] sheets;
    }

    [Serializable]
    internal sealed class GoogleSpreadsheetMetadata
    {
        public GoogleSpreadsheetProperties properties;
        public GoogleSheetWrapper[] sheets;
    }

    [Serializable]
    internal sealed class GoogleSpreadsheetProperties
    {
        public string title;
    }

    [Serializable]
    internal sealed class GoogleSheetWrapper
    {
        public GoogleSheetProperties properties;
    }

    [Serializable]
    internal sealed class GoogleSheetProperties
    {
        public int sheetId;
        public string title;
        public string sheetType;
    }
}
