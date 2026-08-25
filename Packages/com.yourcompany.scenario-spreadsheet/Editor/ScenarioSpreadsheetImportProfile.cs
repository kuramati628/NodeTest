using System.Collections.Generic;
using ScenarioGraphSystem;
using UnityEngine;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    /// <summary>1つのScenarioGraphとGoogle Spreadsheet全体の取得条件を対応付けるEditor専用設定です。</summary>
    [CreateAssetMenu(fileName = "ScenarioSpreadsheetImportProfile", menuName = "Scenario/Spreadsheet/Import Profile")]
    public sealed class ScenarioSpreadsheetImportProfile : ScriptableObject
    {
        [SerializeField] private ScenarioGraph targetGraph;
        [SerializeField] private GoogleSheetsCredential credential;
        [SerializeField] private string spreadsheetId = string.Empty;
        [SerializeField] private string cellRange = "A1:Z1000";
        [SerializeField] private string outputFolder = "Assets/ScenarioData/Generated";
        [SerializeField] private List<string> excludedSheetNames = new() { "使い方", "ForCopy" };

        public ScenarioGraph TargetGraph => targetGraph;
        public GoogleSheetsCredential Credential => credential;
        public string SpreadsheetId => spreadsheetId?.Trim() ?? string.Empty;
        public string CellRange => cellRange?.Trim() ?? string.Empty;
        public string OutputFolder => outputFolder?.Trim() ?? string.Empty;
        public IReadOnlyList<string> ExcludedSheetNames => excludedSheetNames;
    }
}
