# Scenario Spreadsheet Importer

`ScenarioDefinition`へGoogle Spreadsheet由来のCSVを割り当てるEditor専用拡張です。

## 依存関係

- `com.yourcompany.scenario-graph`
- `com.cysharp.unitask`
- `com.unity.nuget.newtonsoft-json`

## 利用手順

1. `Create > Scenario > Spreadsheet > Google Sheets Credential` を作成します。
2. `Create > Scenario > Spreadsheet > Import Profile` を作成します。
3. Import ProfileへSpreadsheet ID、出力先、必要なら同期先ScenarioGraphを設定します。
4. Inspectorの「SpreadsheetからCSVを更新」を実行します。

CredentialとImport Profileはプロジェクト固有です。NodeTest開発プロジェクトでは`Assets/LocalConfig/`へ置き、Git管理しません。

## LabelとGraph同期

- シート先頭の`Label`と、`DefineLabel`内の各`Label`は別々のCSV・ScenarioDefinition・Graphノードになります。
- `jump`先は、同じSpreadsheetのインポート対象シート全体で一意なLabel名から解決します。未定義・重複したLabelはインポートを中止します。
- `jump`、`DefineLabel`、`Label`、`GoToGame`行は生成CSVに含めません。末尾が`End`の区間はGraphの終了ノードへ接続します。
- `GoToGame`で終わる区間からGameノードへの接続、およびGameノードから次のScenarioへの接続は手動で設定します。
