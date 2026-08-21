using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>ゲームごとに異なる設定アセットの共通基底型です。</summary>
    public abstract class GameData : ScriptableObject
    {
    }

    /// <summary>
    /// EditorではSceneAssetを選択し、RuntimeではGUIDとパスだけを利用するシーン参照です。
    /// SceneAssetをRuntimeアセンブリへ持ち込まないため、ビルド時にも安全に利用できます。
    /// </summary>
    [Serializable]
    public sealed class SceneReference
    {
        [SerializeField] private string sceneGuid = string.Empty;
        [SerializeField] private string scenePath = string.Empty;

        public string SceneGuid => sceneGuid;
        public string ScenePath => scenePath;
        public bool IsAssigned => !string.IsNullOrWhiteSpace(scenePath);
    }

    /// <summary>不変ゲームID、表示名、ゲームを実装するシーンを対応付ける登録情報です。</summary>
    [Serializable]
    public sealed class GameRegistration
    {
        [SerializeField] private string gameId;
        [SerializeField] private string displayName;
        [SerializeField] private SceneReference scene = new();

        public string GameId => gameId;
        public string DisplayName { get => displayName; set => displayName = value ?? string.Empty; }
        public SceneReference Scene => scene;

        /// <summary>新しい不変ゲームIDを持つ登録情報を生成します。</summary>
        public static GameRegistration Create() => new()
        {
            gameId = Guid.NewGuid().ToString("N"),
            displayName = "New Game",
            scene = new SceneReference()
        };
    }

    /// <summary>利用可能なゲームIDと、そのゲームを実装するシーンの一覧です。</summary>
    [CreateAssetMenu(fileName = "GameRegistry", menuName = "Scenario/Game Registry")]
    public sealed class GameRegistry : ScriptableObject
    {
        [SerializeField] private List<GameRegistration> games = new();
        public List<GameRegistration> Games => games;

        /// <summary>不変ゲームIDから登録情報を検索します。</summary>
        public bool TryGet(string gameId, out GameRegistration registration)
        {
            registration = games.Find(item => item.GameId == gameId);
            return registration != null;
        }
    }
}
