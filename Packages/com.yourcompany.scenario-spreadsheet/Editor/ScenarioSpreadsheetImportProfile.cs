using ScenarioGraphSystem;
using UnityEngine;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    /// <summary>1つのScenarioDefinitionとGoogle Spreadsheetの取得条件を対応付けるEditor専用設定です。</summary>
    [CreateAssetMenu(fileName = "ScenarioSpreadsheetImportProfile", menuName = "Scenario/Spreadsheet/Import Profile")]
    public sealed class ScenarioSpreadsheetImportProfile : ScriptableObject
    {
        [SerializeField] private ScenarioDefinition targetDefinition;
        [SerializeField] private GoogleSheetsCredential credential;
        [SerializeField] private string spreadsheetId = string.Empty;
        [SerializeField] private int sheetGid = -1;
        [SerializeField] private string fallbackSheetName = "シート1";
        [SerializeField] private string cellRange = "A1:Z1000";
        [SerializeField] private string outputFolder = "Assets/ScenarioData/Generated";
        [SerializeField] private string outputFileName = "Scenario.csv";

        public ScenarioDefinition TargetDefinition => targetDefinition;
        public GoogleSheetsCredential Credential => credential;
        public string SpreadsheetId => spreadsheetId?.Trim() ?? string.Empty;
        public int SheetGid => sheetGid;
        public string FallbackSheetName => fallbackSheetName?.Trim() ?? string.Empty;
        public string CellRange => cellRange?.Trim() ?? string.Empty;
        public string OutputFolder => outputFolder?.Trim() ?? string.Empty;
        public string OutputFileName => outputFileName?.Trim() ?? string.Empty;
    }
}
