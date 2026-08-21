using System;
using System.Linq;
using R3;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>アセットへ状態を書き込まず、シナリオグラフの実行状態と遷移を管理します。</summary>
    public sealed class ScenarioGraphRunner : IDisposable
    {
        private readonly IScenarioPlayer scenarioPlayer;
        private readonly IScenarioGameSceneService gameSceneService;
        private readonly Subject<NodeData> nodeChanged = new();
        private readonly Subject<string> error = new();
        private readonly Subject<ScenarioGameLoadedEvent> gameLoaded = new();
        private CompositeDisposable transitionSubscriptions = new();
        private ScenarioGraph graph;
        private NodeData currentNode;
        private bool running;
        private int transitionVersion;

        public ScenarioGraphRunner(IScenarioPlayer scenarioPlayer, IScenarioGameSceneService gameSceneService)
        {
            this.scenarioPlayer = scenarioPlayer;
            this.gameSceneService = gameSceneService;
        }

        public Observable<NodeData> OnNodeChanged => nodeChanged;
        public Observable<string> OnError => error;
        /// <summary>ゲームSceneのロードとゲーム実装解決が完了した直後に発行されます。</summary>
        public Observable<ScenarioGameLoadedEvent> OnGameLoaded => gameLoaded;

        /// <summary>グラフを開始ノードから実行します。検証エラーがある場合は開始しません。</summary>
        public void Start(ScenarioGraph targetGraph)
        {
            Reset();
            graph = targetGraph;
            var validationErrors = ScenarioGraphValidator.Validate(graph);
            if (validationErrors.Count > 0)
            {
                Fail(validationErrors[0].Message);
                return;
            }

            running = true;
            EnterNode(GetStartNode());
        }

        /// <summary>現在設定されているグラフの開始ノードを返します。</summary>
        public NodeData GetStartNode() => graph != null ? graph.GetStartNode() : null;

        /// <summary>現在実行中のノードを返します。</summary>
        public NodeData GetCurrentNode() => currentNode;

        /// <summary>ゲーム終了結果に対応する出力Edgeへ遷移します。</summary>
        public void SubmitGameResult(GameResult result)
        {
            if (!running || currentNode == null || currentNode.NodeType != ScenarioNodeType.Game)
                return;

            var port = currentNode.OutputPorts.FirstOrDefault(candidate => candidate.GameResult == result);
            if (port == null)
            {
                Fail($"GameResult.{result}に対応する出力ポートがありません。");
                return;
            }

            var edge = graph.Edges.FirstOrDefault(candidate =>
                candidate.OutputNodeGuid == currentNode.Guid && candidate.OutputPortGuid == port.Guid);
            if (edge == null)
            {
                Fail($"GameResult.{result}に対応するEdgeがありません。");
                return;
            }

            EnterNode(graph.FindNode(edge.InputNodeGuid));
        }

        /// <summary>実行状態を破棄し、購読・シナリオ再生・ゲームシーンを確実に解除します。</summary>
        public void Reset()
        {
            transitionVersion++;
            running = false;
            scenarioPlayer?.Stop();
            gameSceneService?.UnloadCurrentGame();
            transitionSubscriptions.Dispose();
            transitionSubscriptions = new CompositeDisposable();
            graph = null;
            currentNode = null;
        }

        private void EnterNode(NodeData node)
        {
            transitionVersion++;
            transitionSubscriptions.Clear();
            scenarioPlayer?.Stop();
            gameSceneService?.UnloadCurrentGame();
            if (node == null)
            {
                Fail("遷移先ノードを解決できません。");
                return;
            }

            currentNode = node;
            nodeChanged.OnNext(node);
            // 購読側が通知中にResetや手動遷移を行った場合、古いノードの処理を開始しません。
            if (!running || currentNode != node)
                return;

            switch (node.NodeType)
            {
                case ScenarioNodeType.Start:
                    AdvanceSingleOutput(node, "開始ノードが未接続です。");
                    break;
                case ScenarioNodeType.Scenario:
                    StartScenario(node);
                    break;
                case ScenarioNodeType.Game:
                    StartGame(node);
                    break;
            }
        }

        private void StartScenario(NodeData node)
        {
            if (node.ScenarioDefinition == null || node.ScenarioDefinition.Csv == null || scenarioPlayer == null)
            {
                if (node.ScenarioDefinition == null)
                    Fail("ScenarioDefinitionが未設定です。");
                else if (node.ScenarioDefinition.Csv == null)
                    Fail("ScenarioDefinitionにCSVが設定されていません。");
                else
                    Fail("IScenarioPlayerが設定されていません。");
                return;
            }

            var version = transitionVersion;
            scenarioPlayer.ScenarioCompleted.Take(1).Subscribe(_ =>
            {
                if (running && version == transitionVersion)
                    AdvanceSingleOutput(node, "シナリオノードの出力が未接続です。");
            }, exception => Fail($"シナリオ完了通知でエラーが発生しました: {exception.Message}"), _ => { })
            .AddTo(transitionSubscriptions);

            try
            {
                scenarioPlayer.Play(node.ScenarioDefinition);
            }
            catch (Exception exception)
            {
                Fail($"シナリオ開始に失敗しました: {exception.Message}");
            }
        }

        private void StartGame(NodeData node)
        {
            if (node.GameRegistry == null || !node.GameRegistry.TryGet(node.GameId, out var registration))
            {
                Fail("未解決のゲームIDです。");
                return;
            }
            if (node.GameData == null)
            {
                Fail("GameDataが未設定です。");
                return;
            }
            if (registration.Scene == null || !registration.Scene.IsAssigned)
            {
                Fail($"ゲーム『{registration.DisplayName}』のシーンが未設定です。");
                return;
            }
            if (gameSceneService == null)
            {
                Fail("IScenarioGameSceneServiceが設定されていません。");
                return;
            }

            var version = transitionVersion;
            gameSceneService.LoadGame(registration.Scene, game =>
            {
                if (!running || version != transitionVersion)
                    return;

                try
                {
                    gameLoaded.OnNext(new ScenarioGameLoadedEvent(node.GameId, registration.Scene, game));
                }
                catch (Exception exception)
                {
                    Fail($"ゲームSceneロード通知でエラーが発生しました: {exception.Message}");
                    return;
                }
                if (!running || version != transitionVersion)
                    return;

                var completed = false;
                try
                {
                    game.StartGame(node.GameData, result =>
                    {
                        if (completed || !running || version != transitionVersion)
                            return;
                        completed = true;
                        SubmitGameResult(result);
                    });
                }
                catch (Exception exception)
                {
                    Fail($"ゲーム開始に失敗しました: {exception.Message}");
                }
            }, message =>
            {
                if (running && version == transitionVersion)
                    Fail(message);
            });
        }

        private void AdvanceSingleOutput(NodeData node, string message)
        {
            var edge = graph.Edges.FirstOrDefault(candidate => candidate.OutputNodeGuid == node.Guid);
            if (edge == null)
            {
                Fail(message);
                return;
            }
            EnterNode(graph.FindNode(edge.InputNodeGuid));
        }

        private void Fail(string message)
        {
            running = false;
            transitionVersion++;
            scenarioPlayer?.Stop();
            gameSceneService?.UnloadCurrentGame();
            transitionSubscriptions.Clear();
            Debug.LogError($"[ScenarioGraphRunner] {message}");
            error.OnNext(message);
        }

        public void Dispose()
        {
            Reset();
            transitionSubscriptions.Dispose();
            nodeChanged.Dispose();
            gameLoaded.Dispose();
            error.Dispose();
        }
    }
}
