using System;
using R3;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>
    /// プロジェクト側のGameSceneEntryPointから利用できるゲーム実装モックです。
    /// Scenario Graphパッケージ固有の実行インターフェースには依存しません。
    /// </summary>
    public sealed class MockScenarioGame : MonoBehaviour
    {
        [SerializeField] private string fallbackResult = "Cancelled";
        /// <summary>ゲーム設定に従い、指定時間後に終了結果を1回だけ発行します。</summary>
        public Observable<string> StartGame(ScriptableObject definition)
        {
            var mockData = definition as MockGameData;
            if (mockData == null)
            {
                Debug.LogError("[MockScenarioGame] MockGameData以外が渡されました。", this);
                return Observable.Return(fallbackResult)
                    .Do(result => Debug.Log($"[MockScenarioGame] 完了結果: {result}", this));
            }

            return Observable.Timer(
                    TimeSpan.FromSeconds(mockData.CompletionDelaySeconds),
                    UnityTimeProvider.Update)
                .Select(_ => mockData.CompletionResult)
                .Do(result => Debug.Log($"[MockScenarioGame] 完了結果: {result}", this));
        }
    }
}
