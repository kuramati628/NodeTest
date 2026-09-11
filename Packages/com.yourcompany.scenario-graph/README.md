# Scenario Graph Editor

Unity 6000.4.6向けの、シナリオCSVとゲーム進行を同一グラフで編集するEditor拡張とRuntime状態機械です。

## 責務

Scenario Graphパッケージは次の処理だけを担当します。

- グラフ構造の検証
- Startノードの解決
- 現在ノードの保持
- 次に実行するScenario/Gameノードの通知
- Scenarioノード正常完了時の単一出力Edge解決
- ゲーム結果名に対応する出力PortとEdgeの解決
- Endノード到達時の完了通知

次の処理は利用プロジェクト側のCoordinatorやScene EntryPointで実装してください。

- Unity Sceneのロード、アンロード
- Navigathenaなどの画面遷移API呼び出し
- シナリオ再生
- ゲームコンポーネントの検索と実行
- ゲームへの入力データ送信と結果購読
- Result Sceneへの遷移

```text
ScenarioGraphRunner
  ├─ OnNodeChanged(NodeData) ── 利用側がScenario/Gameを実行
  ├─ CompleteScenarioNode() ─── Scenario正常完了を返す
  ├─ SubmitGameResult(name) ─── Game結果名を返す
  ├─ OnCompleted ────────────── End到達通知
  └─ OnError ───────────────── 検証・分岐エラー
```

## ファイル構成

```text
Packages/com.yourcompany.scenario-graph/
├─ Runtime/
│  ├─ ScenarioGraphData.cs          永続ノード、Edge、グループ、コメント
│  ├─ ScenarioGraph.cs              グラフ全体を保持する単一アセット
│  ├─ GameRegistry.cs               ゲームID、表示名、シーンGUID/Path
│  ├─ ScenarioDefinition.cs         CSV参照を保持するシナリオ設定アセット
│  ├─ ScenarioExecutionContracts.cs プロジェクト側デバッグホストの契約
│  ├─ ScenarioGraphValidator.cs     Editor/Runtime共通検証
│  └─ ScenarioGraphRunner.cs        外部から進行させる実行状態機械
├─ Editor/                          GraphView Editor
├─ Tests/Editor/                    RunnerのEdit Modeテスト
└─ Samples~/ScenarioGraphDemo/      外部ホスト方式の接続デモ
```

ノードとポートのGUIDは作成時だけ発行されます。既存の`NodeData`、`GameRegistry`、`SceneReference`、出力Port、Edgeのシリアライズ構造は維持されます。

## 作成と編集

1. `Assets > Create > Scenario > Scenario Graph` でグラフを作成します。
2. アセットをダブルクリックして専用Windowを開きます。
3. 「開始」と「終了」を1個ずつ作ります。
4. 「シナリオ」「ゲーム」を追加してEdgeで接続します。
5. Scenarioノードへ`ScenarioDefinition`を設定します。
6. Gameノードへ`GameRegistry`、ゲームID、Attached Dataを設定します。
7. 「検証」でエラーを確認し、「保存」でアセットを保存します。

Attached Dataが`SentenceData`ならenumから分岐名を取得します。既存ScriptableObjectにenumが1種類だけある場合も自動検出します。複数enumなどで自動解決できない場合は`ScenarioBranchResolver`を設定してください。

## Runnerの使用例

Runnerはコンストラクタ引数を必要としません。`OnNodeChanged`で受け取ったノードを利用側で実行し、完了結果だけをRunnerへ返します。

```csharp
using R3;
using ScenarioGraphSystem;

var runner = new ScenarioGraphRunner();

runner.OnNodeChanged.Subscribe(node =>
{
    switch (node.NodeType)
    {
        case ScenarioNodeType.Scenario:
            // node.ScenarioDefinitionを会話Sceneへ渡す。
            break;

        case ScenarioNodeType.Game:
            // node.GameIdから遷移先を解決し、node.AttachedDataをゲームSceneへ渡す。
            break;
    }
});

runner.OnCompleted.Subscribe(_ =>
{
    // 利用側がResult Sceneへ遷移する。
});

runner.Start(graphAsset);
```

Scenarioの正常終了時は次を呼びます。

```csharp
runner.CompleteScenarioNode();
```

ゲーム終了時は出力Portの`BranchName`と一致する結果名を渡します。

```csharp
runner.SubmitGameResult("Perfect");
```

無効な分岐名、未接続Port、存在しないEdgeは`OnError`へ通知され、実行を停止します。実行中でない場合や現在ノードと異なる種類の完了通知は、多重完了対策として無視されます。

## GameRegistry

`Assets > Create > Scenario > Game Registry` でRegistryを作成します。ゲームごとの不変ID、表示名、Scene GUID/Pathを保持します。

RunnerはGame Registryを利用してグラフの妥当性を検証しますが、登録Sceneをロードしません。利用側のCoordinatorが`node.GameRegistry.TryGet(node.GameId, out registration)`で登録情報を取得し、任意のScene管理システムへ渡してください。

## ノード単体デバッグ

Graph Editorの単体デバッグはSceneを自動的に切り替えません。現在開いているSceneでPlay Modeを開始し、Scene内の`IScenarioGraphDebugHost`へ選択ノードを渡します。

```csharp
public sealed class ProjectDebugHost : MonoBehaviour, IScenarioGraphDebugHost
{
    public bool CanDebugNode(ScenarioGraph graph, NodeData node, out string reason)
    {
        reason = string.Empty;
        return true;
    }

    public void DebugNode(ScenarioGraph graph, NodeData node)
    {
        // プロジェクト固有のSceneData生成、画面遷移、再生を行う。
    }
}
```

パッケージ同梱の`ScenarioGraphMockRunner`ではScene遷移を行わず、Context MenuからScenario完了またはGame結果送信を試せます。

## R3

このパッケージはR3 DLLを同梱しません。R3コアとR3.Unity 1.3.1を利用プロジェクト側へ導入してください。

```json
"com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.3.1"
```

R3のソースとライセンスは <https://github.com/Cysharp/R3> を参照してください。
