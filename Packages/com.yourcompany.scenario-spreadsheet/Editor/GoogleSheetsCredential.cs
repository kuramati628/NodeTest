using System;
using UnityEngine;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    /// <summary>
    /// Google Sheets APIへ接続するためのAPIキー設定です。
    /// 環境変数が設定されている場合は、アセットに保存した値より環境変数を優先します。
    /// </summary>
    [CreateAssetMenu(fileName = "GoogleSheetsCredential", menuName = "Scenario/Spreadsheet/Google Sheets Credential")]
    public sealed class GoogleSheetsCredential : ScriptableObject
    {
        [SerializeField] private string environmentVariableName = "GOOGLE_SHEETS_API_KEY";
        [SerializeField] private string fallbackApiKey = string.Empty;

        public string EnvironmentVariableName => environmentVariableName;

        /// <summary>環境変数、アセット内フォールバックの順にAPIキーを解決します。</summary>
        public string ResolveApiKey()
        {
            if (!string.IsNullOrWhiteSpace(environmentVariableName))
            {
                var environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);
                if (!string.IsNullOrWhiteSpace(environmentValue))
                    return environmentValue.Trim();
            }

            return fallbackApiKey?.Trim() ?? string.Empty;
        }
    }
}
