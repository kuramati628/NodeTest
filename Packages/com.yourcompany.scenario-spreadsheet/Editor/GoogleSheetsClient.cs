using System;
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

        /// <summary>GIDまたはシート名を解決し、指定範囲のセル値を取得します。</summary>
        public async UniTask<GoogleSheetData> FetchAsync(
            string apiKey,
            string spreadsheetId,
            int sheetGid,
            string fallbackSheetName,
            string cellRange,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Google Sheets APIキーが設定されていません。");
            if (string.IsNullOrWhiteSpace(spreadsheetId))
                throw new InvalidOperationException("Spreadsheet IDが設定されていません。");

            var sheetName = sheetGid >= 0
                ? await ResolveSheetNameAsync(apiKey, spreadsheetId, sheetGid, cancellationToken)
                : fallbackSheetName;
            if (string.IsNullOrWhiteSpace(sheetName))
                throw new InvalidOperationException("取得対象のシート名を解決できません。");

            var a1Range = BuildA1Range(sheetName, cellRange);
            var url = $"{ApiBaseUrl}/{Uri.EscapeDataString(spreadsheetId)}/values/{Uri.EscapeDataString(a1Range)}?key={Uri.EscapeDataString(apiKey)}";
            var json = await GetJsonAsync(url, cancellationToken);
            return JsonConvert.DeserializeObject<GoogleSheetData>(json)
                   ?? throw new InvalidOperationException("Google Sheets APIの応答を解析できませんでした。");
        }

        private static async UniTask<string> ResolveSheetNameAsync(
            string apiKey,
            string spreadsheetId,
            int sheetGid,
            CancellationToken cancellationToken)
        {
            var url = $"{ApiBaseUrl}/{Uri.EscapeDataString(spreadsheetId)}?fields=sheets.properties&key={Uri.EscapeDataString(apiKey)}";
            var json = await GetJsonAsync(url, cancellationToken);
            var metadata = JsonConvert.DeserializeObject<GoogleSpreadsheetMetadata>(json);
            var sheet = metadata?.sheets?.FirstOrDefault(item => item?.properties?.sheetId == sheetGid);
            return sheet?.properties?.title
                   ?? throw new InvalidOperationException($"sheet gid={sheetGid} に対応するシートが見つかりませんでした。");
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
        public string[][] values;
    }

    [Serializable]
    internal sealed class GoogleSpreadsheetMetadata
    {
        public GoogleSheetWrapper[] sheets;
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
    }
}
