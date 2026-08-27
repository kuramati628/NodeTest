using System;
using System.Reflection;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ScenarioGraphSystem.Tests
{
    public sealed class ScenarioGraphRunnerTests
    {
        private ScenarioGraph graph;
        private ScenarioDefinition definition;
        private GameRegistry registry;
        private TestGameData gameData;

        [TearDown]
        public void TearDown()
        {
            Destroy(graph);
            Destroy(definition);
            Destroy(registry);
            Destroy(gameData);
        }

        [Test]
        public void ScenarioCompletion_AdvancesToEndAndDisposesPlayback()
        {
            var player = new TestScenarioPlayer();
            graph = CreateScenarioGraph();
            using var runner = new ScenarioGraphRunner(player, new TestSceneService());
            var completedCount = 0;
            using var subscription = runner.OnCompleted.Subscribe(_ => completedCount++);

            runner.Start(graph);

            Assert.That(player.Subscribed, Is.True);
            player.Complete();
            Assert.That(completedCount, Is.EqualTo(1));
            Assert.That(player.Disposed, Is.True);
        }

        [Test]
        public void Reset_DisposesCurrentPlayback()
        {
            var player = new TestScenarioPlayer();
            graph = CreateScenarioGraph();
            using var runner = new ScenarioGraphRunner(player, new TestSceneService());

            runner.Start(graph);
            runner.Reset();

            Assert.That(player.Disposed, Is.True);
            Assert.That(runner.GetCurrentNode(), Is.Null);
        }

        [Test]
        public void SynchronousGameResult_CompletesAndDisposesSceneLease()
        {
            var game = new TestGame();
            var sceneService = new TestSceneService(game);
            graph = CreateGameGraph();
            using var runner = new ScenarioGraphRunner(new TestScenarioPlayer(), sceneService);
            var completedCount = 0;
            using var subscription = runner.OnCompleted.Subscribe(_ => completedCount++);

            runner.Start(graph);

            Assert.That(completedCount, Is.EqualTo(1));
            Assert.That(game.Started, Is.True);
            Assert.That(sceneService.LeaseDisposed, Is.True);
        }

        [Test]
        public void PlaybackError_IsForwardedAndDisposesPlayback()
        {
            var player = new TestScenarioPlayer(new InvalidOperationException("test error"));
            graph = CreateScenarioGraph();
            using var runner = new ScenarioGraphRunner(player, new TestSceneService());
            string message = null;
            using var subscription = runner.OnError.Subscribe(value => message = value);
            LogAssert.Expect(LogType.Error, "[ScenarioGraphRunner] シナリオ再生に失敗しました: test error");

            runner.Start(graph);

            Assert.That(message, Does.Contain("test error"));
            Assert.That(player.Disposed, Is.True);
        }

        private ScenarioGraph CreateScenarioGraph()
        {
            definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("csv").objectReferenceValue = new TextAsset("scenario");
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            var result = ScriptableObject.CreateInstance<ScenarioGraph>();
            var start = NodeData.Create(ScenarioNodeType.Start, Vector2.zero);
            var scenario = NodeData.Create(ScenarioNodeType.Scenario, Vector2.right);
            var end = NodeData.Create(ScenarioNodeType.End, Vector2.right * 2);
            scenario.ScenarioDefinition = definition;
            result.Nodes.Add(start);
            result.Nodes.Add(scenario);
            result.Nodes.Add(end);
            result.StartNodeGuid = start.Guid;
            result.Edges.Add(EdgeData.Create(start.Guid, start.OutputPorts[0].Guid, scenario.Guid));
            result.Edges.Add(EdgeData.Create(scenario.Guid, scenario.OutputPorts[0].Guid, end.Guid));
            return result;
        }

        private ScenarioGraph CreateGameGraph()
        {
            registry = ScriptableObject.CreateInstance<GameRegistry>();
            var registration = GameRegistration.Create();
            SetPrivateField(registration.Scene, "scenePath", "Assets/TestGame.unity");
            registry.Games.Add(registration);
            gameData = ScriptableObject.CreateInstance<TestGameData>();

            var result = ScriptableObject.CreateInstance<ScenarioGraph>();
            var start = NodeData.Create(ScenarioNodeType.Start, Vector2.zero);
            var game = NodeData.Create(ScenarioNodeType.Game, Vector2.right);
            var end = NodeData.Create(ScenarioNodeType.End, Vector2.right * 2);
            game.GameRegistry = registry;
            game.GameId = registration.GameId;
            game.AttachedData = gameData;
            game.OutputPorts.Add(OutputPortData.Create("Success", nameof(TestGameResult.Success)));
            game.OutputPorts.Add(OutputPortData.Create("Failure", nameof(TestGameResult.Failure)));
            result.Nodes.Add(start);
            result.Nodes.Add(game);
            result.Nodes.Add(end);
            result.StartNodeGuid = start.Guid;
            result.Edges.Add(EdgeData.Create(start.Guid, start.OutputPorts[0].Guid, game.Guid));
            result.Edges.Add(EdgeData.Create(game.Guid, game.OutputPorts[0].Guid, end.Guid));
            result.Edges.Add(EdgeData.Create(game.Guid, game.OutputPorts[1].Guid, end.Guid));
            return result;
        }

        private static void SetPrivateField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);

        private static void Destroy(UnityEngine.Object target)
        {
            if (target != null)
                UnityEngine.Object.DestroyImmediate(target);
        }

        private sealed class TestScenarioPlayer : IScenarioPlayer
        {
            private readonly Exception error;
            private Observer<Unit> observer;

            public TestScenarioPlayer(Exception error = null) => this.error = error;

            public bool Subscribed { get; private set; }
            public bool Disposed { get; private set; }

            public Observable<Unit> Play(ScenarioDefinition target)
            {
                return Observable.Create<Unit>(value =>
                {
                    observer = value;
                    Subscribed = true;
                    if (error != null)
                        value.OnCompleted(Result.Failure(error));
                    return new CallbackDisposable(() => Disposed = true);
                });
            }

            public void Complete()
            {
                observer.OnNext(Unit.Default);
                observer.OnCompleted();
            }
        }

        private sealed class TestSceneService : IScenarioGameSceneService
        {
            private readonly IScenarioGame game;

            public TestSceneService(IScenarioGame game = null) => this.game = game;

            public bool LeaseDisposed { get; private set; }

            public Observable<IScenarioGame> LoadGame(SceneReference sceneReference)
            {
                return Observable.Create<IScenarioGame>(observer =>
                {
                    if (game != null)
                        observer.OnNext(game);
                    return new CallbackDisposable(() => LeaseDisposed = true);
                });
            }
        }

        private sealed class TestGame : IScenarioGame
        {
            public bool Started { get; private set; }

            public Observable<string> StartGame(ScriptableObject target)
            {
                Started = true;
                return Observable.Return(nameof(TestGameResult.Success));
            }
        }

        private sealed class CallbackDisposable : IDisposable
        {
            private Action callback;

            public CallbackDisposable(Action callback) => this.callback = callback;

            public void Dispose()
            {
                var action = callback;
                callback = null;
                action?.Invoke();
            }
        }
    }

    public enum TestGameResult
    {
        Success,
        Failure
    }

    public sealed class TestGameData : SentenceData
    {
        public TestGameResult result;
    }
}
