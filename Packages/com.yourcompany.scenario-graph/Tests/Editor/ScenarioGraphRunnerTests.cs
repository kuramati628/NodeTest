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
        public void Start_AnnouncesScenarioAndWaitsForExternalCompletion()
        {
            graph = CreateScenarioGraph(out var scenario, out _);
            using var runner = new ScenarioGraphRunner();
            NodeData announced = null;
            var completedCount = 0;
            using var nodeSubscription = runner.OnNodeChanged.Subscribe(node => announced = node);
            using var completedSubscription = runner.OnCompleted.Subscribe(_ => completedCount++);

            runner.Start(graph);

            Assert.That(announced, Is.SameAs(scenario));
            Assert.That(runner.GetCurrentNode(), Is.SameAs(scenario));
            Assert.That(runner.IsRunning, Is.True);
            Assert.That(completedCount, Is.Zero);
        }

        [Test]
        public void CompleteScenarioNode_AdvancesToEndAndCompletesOnce()
        {
            graph = CreateScenarioGraph(out _, out var end);
            using var runner = new ScenarioGraphRunner();
            var completedCount = 0;
            using var subscription = runner.OnCompleted.Subscribe(_ => completedCount++);

            runner.Start(graph);
            runner.CompleteScenarioNode();
            runner.CompleteScenarioNode();

            Assert.That(completedCount, Is.EqualTo(1));
            Assert.That(runner.GetCurrentNode(), Is.SameAs(end));
            Assert.That(runner.IsRunning, Is.False);
        }

        [Test]
        public void Reset_ClearsCurrentExecution()
        {
            graph = CreateScenarioGraph(out _, out _);
            using var runner = new ScenarioGraphRunner();

            runner.Start(graph);
            runner.Reset();

            Assert.That(runner.GetCurrentNode(), Is.Null);
            Assert.That(runner.IsRunning, Is.False);
        }

        [Test]
        public void SubmitGameResult_UsesMatchingBranchAndCompletes()
        {
            graph = CreateGameGraph(out var game, out var end);
            using var runner = new ScenarioGraphRunner();
            NodeData announced = null;
            var completedCount = 0;
            using var nodeSubscription = runner.OnNodeChanged.Subscribe(node => announced = node);
            using var completedSubscription = runner.OnCompleted.Subscribe(_ => completedCount++);

            runner.Start(graph);
            Assert.That(announced, Is.SameAs(game));

            runner.SubmitGameResult(nameof(TestGameResult.Success));

            Assert.That(completedCount, Is.EqualTo(1));
            Assert.That(runner.GetCurrentNode(), Is.SameAs(end));
        }

        [Test]
        public void SubmitGameResult_WithUnknownBranch_PublishesError()
        {
            graph = CreateGameGraph(out _, out _);
            using var runner = new ScenarioGraphRunner();
            string message = null;
            using var subscription = runner.OnError.Subscribe(value => message = value);
            LogAssert.Expect(LogType.Error,
                "[ScenarioGraphRunner] ゲームからアタッチデータにない分岐『Missing』が返されました。");

            runner.Start(graph);
            runner.SubmitGameResult("Missing");

            Assert.That(message, Does.Contain("Missing"));
            Assert.That(runner.IsRunning, Is.False);
        }

        [Test]
        public void StartAtNode_AnnouncesOnlyTargetAndCompletesWithoutFollowingEdge()
        {
            graph = CreateScenarioGraph(out var scenario, out _);
            using var runner = new ScenarioGraphRunner();
            NodeData announced = null;
            var completedCount = 0;
            using var nodeSubscription = runner.OnNodeChanged.Subscribe(node => announced = node);
            using var completedSubscription = runner.OnCompleted.Subscribe(_ => completedCount++);

            runner.StartAtNode(graph, scenario.Guid);
            runner.CompleteScenarioNode();

            Assert.That(announced, Is.SameAs(scenario));
            Assert.That(completedCount, Is.EqualTo(1));
        }

        private ScenarioGraph CreateScenarioGraph(out NodeData scenario, out NodeData end)
        {
            definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("csv").objectReferenceValue = new TextAsset("scenario");
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            var result = ScriptableObject.CreateInstance<ScenarioGraph>();
            var start = NodeData.Create(ScenarioNodeType.Start, Vector2.zero);
            scenario = NodeData.Create(ScenarioNodeType.Scenario, Vector2.right);
            end = NodeData.Create(ScenarioNodeType.End, Vector2.right * 2);
            scenario.ScenarioDefinition = definition;
            result.Nodes.Add(start);
            result.Nodes.Add(scenario);
            result.Nodes.Add(end);
            result.StartNodeGuid = start.Guid;
            result.Edges.Add(EdgeData.Create(start.Guid, start.OutputPorts[0].Guid, scenario.Guid));
            result.Edges.Add(EdgeData.Create(scenario.Guid, scenario.OutputPorts[0].Guid, end.Guid));
            return result;
        }

        private ScenarioGraph CreateGameGraph(out NodeData game, out NodeData end)
        {
            registry = ScriptableObject.CreateInstance<GameRegistry>();
            var registration = GameRegistration.Create();
            SetPrivateField(registration.Scene, "scenePath", "Assets/TestGame.unity");
            registry.Games.Add(registration);
            gameData = ScriptableObject.CreateInstance<TestGameData>();

            var result = ScriptableObject.CreateInstance<ScenarioGraph>();
            var start = NodeData.Create(ScenarioNodeType.Start, Vector2.zero);
            game = NodeData.Create(ScenarioNodeType.Game, Vector2.right);
            end = NodeData.Create(ScenarioNodeType.End, Vector2.right * 2);
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
