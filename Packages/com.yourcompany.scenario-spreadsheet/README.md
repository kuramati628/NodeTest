# Scenario Spreadsheet Importer

`ScenarioDefinition`へGoogle Spreadsheet由来のCSVを割り当てるEditor専用拡張です。

## 依存関係

- `com.yourcompany.scenario-graph`
- `com.cysharp.unitask`
- `com.unity.nuget.newtonsoft-json`

## 利用手順

1. `Create > Scenario > Spreadsheet > Google Sheets Credential` を作成します。
2. `Create > Scenario > Spreadsheet > Import Profile` を作成します。
3. Import Profileへ対象のScenarioDefinition、Spreadsheet ID、シート、出力先を設定します。
4. Inspectorの「SpreadsheetからCSVを更新」を実行します。

CredentialとImport Profileはプロジェクト固有です。NodeTest開発プロジェクトでは`Assets/LocalConfig/`へ置き、Git管理しません。
