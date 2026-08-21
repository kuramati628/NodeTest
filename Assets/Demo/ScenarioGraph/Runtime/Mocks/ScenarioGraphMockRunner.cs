using System;
using System.Collections;
using R3;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>
    /// シナリオ・ゲーム・Graph Runnerの接続をPlay Modeで確認する起動用モックです。
    /// 初期値では最初のシナリオだけを自動完了し、ゲーム後の分岐先ノードを保持します。
    /// </summary>
    public sealed class ScenarioGraphMockRunner : MonoBehaviour
    {
        [SerializeField] private ScenarioGraph graph;
        [SerializeField, Min(0f)] private float scenarioCompletionDelaySeconds = 0.25f;
        [SerializeField, Min(0)] private int autoCompleteScenarioCount = 1;
        [SerializeField] private bool autoStart = true;

        private ScenarioGraphRunner runner;
        private MockScenarioPlayer scenarioPlayer;
        private CompositeDisposable subscriptions;

        private void Start()
        {
            if (autoStart)
                StartGraph();
        }

        /// <summary>設定したグラフを開始し、Consoleへノード遷移とエラーを出力します。</summary>
        [ContextMenu("モックグラフを開始")]
        public void StartGraph()
        {
            DisposeRunner();
            if (graph == null)
            {
                Debug.LogError("[ScenarioGraphMockRunner] ScenarioGraphが設定されていません。", this);
                return;
            }

            scenarioPlayer = new MockScenarioPlayer(this, scenarioCompletionDelaySeconds, autoCompleteScenarioCount);
            runner = new ScenarioGraphRunner(scenarioPlayer, new UnityScenarioGameSceneService());
            subscriptions = new CompositeDisposable();
            runner.OnNodeChanged.Subscribe(node => Debug.Log($"[ScenarioGraphMockRunner] ノード遷移: {node.DisplayName}", this))
                .AddTo(subscriptions);
            runner.OnGameLoaded.Subscribe(game => Debug.Log($"[ScenarioGraphMockRunner] ゲームSceneロード完了: {game.GameId} ({game.SceneReference.ScenePath})", this))
                .AddTo(subscriptions);
            runner.OnError.Subscribe(message => Debug.LogError($"[ScenarioGraphMockRunner] {message}", this))
                .AddTo(subscriptions);
            runner.Start(graph);
        }

        /// <summary>現在のシナリオノードを手動完了させます。終端検証にも使用できます。</summary>
        [ContextMenu("現在のシナリオを完了")]
        public void CompleteCurrentScenario()
        {
            scenarioPlayer?.CompleteNow();
        }

        private void OnDestroy()
        {
            DisposeRunner();
        }

        private void DisposeRunner()
        {
            subscriptions?.Dispose();
            subscriptions = null;
            runner?.Dispose();
            runner = null;
            scenarioPlayer?.Dispose();
            scenarioPlayer = null;
        }

        /// <summary>指定回数だけシナリオ完了を自動通知する、Runner注入用のシナリオプレイヤーです。</summary>
        private sealed class MockScenarioPlayer : IScenarioPlayer, IDisposable
        {
            private readonly MonoBehaviour host;
            private readonly float delaySeconds;
            private readonly int autoCompleteCount;
            private readonly Subject<Unit> completed = new();
            private Coroutine completionCoroutine;
            private int playCount;

            public MockScenarioPlayer(MonoBehaviour host, float delaySeconds, int autoCompleteCount)
            {
                this.host = host;
                this.delaySeconds = delaySeconds;
                this.autoCompleteCount = autoCompleteCount;
            }

            public Observable<Unit> ScenarioCompleted => completed;

            public void Play(ScenarioDefinition definition)
            {
                Stop();
                playCount++;
                Debug.Log($"[MockScenarioPlayer] 再生: {definition.name}", host);
                if (playCount > autoCompleteCount)
                    return;
                completionCoroutine = host.StartCoroutine(CompleteAfterDelay());
            }

            public void Stop()
            {
                if (completionCoroutine != null)
                    host.StopCoroutine(completionCoroutine);
                completionCoroutine = null;
            }

            public void CompleteNow()
            {
                Stop();
                completed.OnNext(Unit.Default);
            }

            public void Dispose()
            {
                Stop();
                completed.Dispose();
            }

            private IEnumerator CompleteAfterDelay()
            {
                if (delaySeconds > 0f)
                    yield return new WaitForSeconds(delaySeconds);
                completionCoroutine = null;
                completed.OnNext(Unit.Default);
            }
        }
    }
}
