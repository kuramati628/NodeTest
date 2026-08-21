using System;
using R3;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>ゲームSceneのロードとIScenarioGame解決が完了したことを表すR3イベント値です。</summary>
    public readonly struct ScenarioGameLoadedEvent
    {
        public ScenarioGameLoadedEvent(string gameId, SceneReference sceneReference, IScenarioGame game)
        {
            GameId = gameId;
            SceneReference = sceneReference;
            Game = game;
        }

        public string GameId { get; }
        public SceneReference SceneReference { get; }
        public IScenarioGame Game { get; }
    }

    /// <summary>シナリオ設定を再生し、完了をRunnerへ通知するシステムの契約です。</summary>
    public interface IScenarioPlayer
    {
        Observable<Unit> ScenarioCompleted { get; }
        void Play(ScenarioDefinition definition);
        void Stop();
    }

    /// <summary>ゲームシーン内に1つだけ配置するゲーム実装の契約です。</summary>
    public interface IScenarioGame
    {
        void StartGame(ScriptableObject definition, Action<string> onCompleted);
    }

    /// <summary>Graph Editorから選択ノードを単体デバッグするPlay Mode側ホストです。</summary>
    public interface IScenarioGraphDebugHost
    {
        bool CanDebugNode(ScenarioGraph graph, NodeData node, out string reason);
        void DebugNode(ScenarioGraph graph, NodeData node);
    }

    /// <summary>登録シーンを読み込み、そのシーン内のゲーム実装をRunnerへ渡す契約です。</summary>
    public interface IScenarioGameSceneService
    {
        void LoadGame(SceneReference sceneReference, Action<IScenarioGame> onLoaded, Action<string> onError);
        void UnloadCurrentGame();
    }
}
