# NodeTest / Scenario Graph Development Project

Scenario Graphパッケージを開発・検証するためのUnity 6000.4.6プロジェクトです。

```text
Packages/
├─ com.yourcompany.scenario-graph/       コアRuntime・GraphView Editor・R3 DLL
└─ com.yourcompany.scenario-spreadsheet/ Spreadsheet CSV更新用Editor拡張

Assets/Demo/ScenarioGraph/                Play Mode検証用のScene・Graph・モック・CSV
Assets/LocalConfig/                       ローカル専用のSpreadsheet認証・Import設定
```

## 開発手順

1. `Assets/Demo/ScenarioGraph/Scenes/SampleScene.unity` を開きます。
2. `Packages/com.yourcompany.scenario-graph/` 内でコア機能を編集します。
3. `Packages/com.yourcompany.scenario-spreadsheet/` 内でSpreadsheet拡張を編集します。
4. Play Modeでモックグラフを確認します。
5. `Assets/LocalConfig/` の認証情報はコミットしません。

他プロジェクトへ配布する場合は、このリポジトリにタグを作成し、各パッケージをGit URLの`path`指定で参照します。
