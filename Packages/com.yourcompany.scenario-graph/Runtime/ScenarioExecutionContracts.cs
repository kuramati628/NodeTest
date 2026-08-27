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
        /// <summary>
        /// 指定シナリオの1回の再生を表すObservableを返します。
        /// 購読時に再生を開始し、完了時にUnitを1回発行します。購読解除時は再生を停止します。
        /// </summary>
        Observable<Unit> Play(ScenarioDefinition definition);
    }

    /// <summary>ゲームシーン内に1つだけ配置するゲーム実装の契約です。</summary>
    public interface IScenarioGame
    {
        /// <summary>
        /// 指定データによる1回のゲーム実行を表すObservableを返します。
        /// 購読時に開始し、終了時に分岐名を1回発行します。購読解除時は実行を停止します。
        /// </summary>
        Observable<string> StartGame(ScriptableObject definition);
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
        /// <summary>
        /// SceneのロードとIScenarioGame解決を表すObservableを返します。
        /// 購読解除時は進行中のロードを無効化し、この購読でロードしたSceneをアンロードします。
        /// </summary>
        Observable<IScenarioGame> LoadGame(SceneReference sceneReference);
    }
}
