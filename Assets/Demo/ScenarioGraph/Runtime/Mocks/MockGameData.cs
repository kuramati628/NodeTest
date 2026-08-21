using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>ScenarioGraphの接続確認に使う、完了結果と待機時間だけを持つゲーム設定モックです。</summary>
    [CreateAssetMenu(fileName = "MockGameData", menuName = "Scenario/Mock/Mock Game Data")]
    public sealed class MockGameData : GameData
    {
        [SerializeField] private GameResult completionResult = GameResult.Success;
        [SerializeField, Min(0f)] private float completionDelaySeconds = 0.25f;

        public GameResult CompletionResult => completionResult;
        public float CompletionDelaySeconds => completionDelaySeconds;
    }
}
