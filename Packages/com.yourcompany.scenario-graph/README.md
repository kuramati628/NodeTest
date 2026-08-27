# Scenario Graph Editor

Unity 6000.4.6向けの、シナリオCSVとゲーム進行を同一グラフで編集・実行するEditor拡張です。

## ファイル構成と責務

```text
Packages/com.yourcompany.scenario-graph/
├─ Runtime/
│  ├─ ScenarioGraphData.cs          永続ノード、Edge、グループ、コメント
│  ├─ ScenarioGraph.cs              グラフ全体を保持する単一アセット
│  ├─ GameRegistry.cs               ゲームID、表示名、シーンGUID/Path
│  ├─ ScenarioDefinition.cs         CSV参照を保持するシナリオ設定アセット
│  ├─ ScenarioExecutionContracts.cs シナリオ再生・ゲームシーン解決の契約
│  ├─ UnityScenarioGameSceneService.cs ゲームシーンの加算ロードと実装解決
│  ├─ ScenarioGraphValidator.cs     Editor/Runtime共通検証
│  └─ ScenarioGraphRunner.cs        アセットを変更しない実行状態機械
├─ Editor/
│  ├─ ScenarioGraphEditorWindow.cs  ツールバー、検索、検証一覧、保存
│  ├─ ScenarioGraphView.cs          GraphView操作とデータ同期
│  ├─ ScenarioNodeView.cs           ノード/コメント表示
│  ├─ ScenarioEdge.cs               自己接続/戻り接続の描画
│  └─ ScenarioGraphInspectors.cs    SerializedPropertyベースInspector
└─ Plugins/R3/R3.dll                R3 1.3.1 (netstandard2.1)

Packages/com.yourcompany.scenario-spreadsheet/Editor/
├─ ScenarioSpreadsheet.Editor.asmdef Editor専用アセンブリ
├─ GoogleSheetsCredential.cs        APIキー解決（環境変数優先）
├─ ScenarioSpreadsheetImportProfile.cs SpreadsheetとScenarioDefinitionの対応設定
├─ GoogleSheetsClient.cs            Google Sheets Values API通信
├─ ScenarioCsvSerializer.cs         CSV引用符処理と列数補完
├─ ScenarioDefinitionCsvImporter.cs 固定CSV更新とDefinitionへの割り当て
└─ ScenarioSpreadsheetImportProfileInspector.cs 取得ボタンと設定検証
```

```text
ScenarioGraph
 ├─ List<NodeData>
 │   ├─ ScenarioDefinition → TextAsset (Scenario)
 │   └─ GameRegistry + gameId + ScriptableObject + Branch Resolver + OutputPortData[] (Game)
 ├─ List<EdgeData> ── outputNodeGuid/outputPortGuid → inputNodeGuid
 ├─ List<GroupData>
 ├─ List<CommentData>
 └─ GraphEditorState

ScenarioGraphRunner
 ├─ IScenarioPlayer.Play ── Observable<Unit>
 ├─ IScenarioGameSceneService.LoadGame ── Observable<IScenarioGame>
 └─ IScenarioGame.StartGame ── Observable<string>

Runner.OnGameLoaded ── Observable<ScenarioGameLoadedEvent>
```

ノードとポートのGUIDは作成時だけ発行されます。コピー＆ペースト時はノードGUID、ポートGUID、内部Edge GUIDをすべて再発行します。リストはユーザー操作順を維持し、自動ソートしません。

## 作成と編集

1. `Assets > Create > Scenario > Scenario Graph` でグラフを作成します。
2. アセットをダブルクリックして専用Windowを開きます。
3. ツールバーの「開始」と「終了」で、それぞれのノードを1個ずつ作ります。
4. 「シナリオ」「ゲーム」を追加し、右側出力から左側入力へ接続します。
5. `Scenario Definition` を作成してCSVを設定し、シナリオノードから参照します。
6. ゲームノードではGameRegistry、ゲーム、既存のScriptableObjectアセットを選びます。出力ポートはアタッチデータから解決した分岐で自動生成されます。
7. 「検証」で下部のエラー一覧を確認し、「保存」でアセットを保存します。

アタッチ先が`SentenceData`なら従来どおり分岐を取得します。既存ScriptableObjectがenumを1種類だけ持つ場合も自動検出され、enumメンバー名が出力ポート名になります。
複数enumなどで自動解決できない型は、ノードの`Branch Resolver`へ専用Resolverアセットを設定してください。

右クリックからもノード、グループ、コメントを作成できます。`Ctrl/Cmd+C` と `Ctrl/Cmd+V`、Delete、ズーム、パンはGraphView標準操作です。検索欄はノード名、CSV名、ゲーム名、コメント本文、グループ名を対象にします。グループを削除しても内部ノードは削除されません。

## GameRegistryとゲーム実装の例

`Assets > Create > Scenario > Game Registry` でRegistryを作成し、Inspectorの `+` からゲームを追加します。ゲームごとにSceneAssetを指定すると、Runtime用のシーンGUIDとPathが自動保存されます。対象シーンはBuild Settingsで有効にし、シーン内には `IScenarioGame` を実装するMonoBehaviourを1個だけ配置します。

```csharp
using R3;
using ScenarioGraphSystem;
using UnityEngine;

public enum SampleResult
{
    Success,
    Failure
}

[CreateAssetMenu(menuName = "Scenario/Sample Game Data")]
public sealed class SampleSentenceData : SentenceData
{
    [SentenceBranchEnum]
    public SampleResult result;
    public int targetScore = 10;
}

public sealed class SampleGame : MonoBehaviour, IScenarioGame
{
    public Observable<string> StartGame(ScriptableObject definition)
    {
        var settings = (SampleSentenceData)definition;
        // 実際の実装では購読解除時にゲーム処理も停止してください。
        return Observable.Return(SampleResult.Success.ToString());
    }
}
```

既存のScriptableObjectを変更せず分岐を取り出したい場合は、次のようなResolverを作成してノードへ設定します。

```csharp
[CreateAssetMenu(menuName = "Scenario/Imported Data Resolver")]
public sealed class ImportedDataResolver : ScenarioBranchResolver
{
    public override bool CanResolve(ScriptableObject data) => data is ImportedGameData;

    public override IReadOnlyList<string> GetBranchNames(ScriptableObject data)
    {
        return ((ImportedGameData)data).branches;
    }
}
```

標準の `UnityScenarioGameSceneService` は購読時に登録シーンをAdditiveで読み込み、ロードしたシーンのルート以下から `IScenarioGame` を検索します。購読解除、ノード遷移、Reset、エラー時にはゲームシーンをアンロードします。

## CSVシナリオ連携の例

`Assets > Create > Scenario > Scenario Definition` でアセットを作り、CSVを設定します。エディタとRunnerはCSV本文を解析せず、ScenarioDefinitionを既存のシナリオシステムへ渡して終了だけをR3で受け取ります。

```csharp
using R3;
using ScenarioGraphSystem;
using UnityEngine;

public sealed class SampleScenarioPlayer : IScenarioPlayer
{
    public Observable<Unit> Play(ScenarioDefinition definition)
    {
        // 既存シナリオシステムの「1回の再生」をObservableへ変換して返します。
        // Observable.Createを使う場合、返すIDisposableで既存システムを停止します。
        return existingScenarioSystem.PlayAsObservable(definition.Csv);
    }
}
```

起動側では依存を注入してRunnerを生成します。

```csharp
var gameSceneService = new UnityScenarioGameSceneService();
var runner = new ScenarioGraphRunner(scenarioPlayer, gameSceneService);
runner.OnNodeChanged.Subscribe(node => Debug.Log($"Node: {node.DisplayName}"));
runner.OnError.Subscribe(message => Debug.LogError(message));
runner.Start(graphAsset);
// 終了時: runner.Dispose();
```

Runnerはノード遷移、Reset、エラー、完了、Disposeのたびに現在のシナリオ・Scene・ゲーム購読を解除します。各実装が複数回値を発行しても、最初の1回だけを受理します。同期的に値を発行するObservableでも、購読は遷移時に確実に破棄されます。

ゲームSceneは`LoadSceneMode.Additive`で読み込まれるため、Runnerを配置したシナリオSceneは維持されます。次のゲームへ遷移するとき、Reset、Dispose、エラー時には現在のゲームSceneだけをアンロードします。ロードと`IScenarioGame`解決が完了すると、Runnerの`OnGameLoaded`（R3）が`StartGame`直前に1回発行されます。

```csharp
runner.OnGameLoaded.Subscribe(loaded =>
{
    Debug.Log($"Game loaded: {loaded.GameId}");
});
```

## Google SpreadsheetからのCSV更新

1. `Assets > Create > Scenario > Spreadsheet > Google Sheets Credential` でCredentialを作成します。
2. APIキーは環境変数 `GOOGLE_SHEETS_API_KEY` に設定します。アセット内のFallback API Keyはローカル確認用です。
3. `Assets > Create > Scenario > Spreadsheet > Import Profile` でImport Profileを作成します。
4. Target Definition、Credential、Spreadsheet ID、Sheet GIDまたはシート名、セル範囲を設定します。
5. Inspectorの「SpreadsheetからCSVを更新」を押します。

CSVはImport Profileで指定した同一パスへ上書きされます。このためTextAssetのGUIDは維持され、ScenarioDefinitionやグラフの参照は切れません。CSV取得・AssetDatabase操作はEditor専用であり、プレイヤー実行時はScenarioDefinitionに保存済みのTextAssetだけを使用します。

セル中のカンマ、改行、ダブルクォートはCSV形式に従ってエスケープされます。Google Sheets APIが省略する行末の空セルは、取得結果の最大列数まで補完されます。

## モックによるPlay Mode接続確認

シナリオノードとゲームノードには単体デバッグボタンがあります。ボタンはPlay Mode外でのみ実行できます。
ゲームノードは登録済みのゲームSceneを読み込んでPlay Modeを開始し、設定済みのアタッチデータをそのSceneの`IScenarioGame`へ渡します。
シナリオノードは現在開いているSceneでPlay Modeを開始し、`IScenarioGraphDebugHost`へ対象ノードを渡します。
デモの`ScenarioGraphMockRunner`はこのインターフェースを実装済みです。

プロジェクトには全体接続確認用のモックを同梱しています。

- `Assets/Demo/ScenarioGraph/MockGameData.asset` はゲームノードに設定済みです。Inspectorの`Branches`リストへ任意の分岐名を入力でき、`Completion Result`にはそのリストの値が選択肢として表示されます。
- `Assets/Demo/ScenarioGraph/Scenes/game1.unity` と `game2.unity` には `MockScenarioGame` が配置済みです。どちらもBuild Settingsへ登録済みです。
- `Assets/Demo/ScenarioGraph/Scenes/SampleScene.unity` の `Scenario Graph Mock Runner` は、Play Mode開始時に `ScenarioGraph.asset` を実行します。

Play Modeでは「開始 → BeforeGameシナリオ → game1 → Success/Failureシナリオ」までConsoleへ出力されます。初期設定は分岐先シナリオで停止し、`Scenario Graph Mock Runner`のコンテキストメニュー「現在のシナリオを完了」を実行すると終了ノードまで確認できます。

## R3

コアAPIには、公式NuGet `R3` 1.3.1の `lib/netstandard2.1/R3.dll` を同梱しています。デモの時間・フレーム処理にはUPMの `com.cysharp.r3`（R3.Unity）を使用します。R3.UnityはコアDLLを内包しないため、このプロジェクトでは両者が補完関係です。ソースとライセンスは <https://github.com/Cysharp/R3> を参照してください。
