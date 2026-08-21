using System;
using System.Collections;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>
    /// ゲームSceneへ配置するIScenarioGame実装モックです。
    /// 渡されたMockGameDataの設定どおりに一度だけ終了通知を返します。
    /// </summary>
    public sealed class MockScenarioGame : MonoBehaviour, IScenarioGame
    {
        [SerializeField] private string fallbackResult = "Cancelled";
        private Coroutine completionCoroutine;
        private bool completed;

        /// <summary>ゲーム設定に従い、指定時間後に終了結果を1回だけ返します。</summary>
        public void StartGame(SentenceData definition, Action<string> onCompleted)
        {
            if (completionCoroutine != null)
                StopCoroutine(completionCoroutine);

            completed = false;
            var mockData = definition as MockGameData;
            if (mockData == null)
            {
                Debug.LogError("[MockScenarioGame] MockGameData以外が渡されました。", this);
                CompleteOnce(fallbackResult, onCompleted);
                return;
            }

            completionCoroutine = StartCoroutine(CompleteAfterDelay(mockData, onCompleted));
        }

        private IEnumerator CompleteAfterDelay(MockGameData data, Action<string> onCompleted)
        {
            if (data.CompletionDelaySeconds > 0f)
                yield return new WaitForSeconds(data.CompletionDelaySeconds);
            CompleteOnce(data.CompletionResult, onCompleted);
            completionCoroutine = null;
        }

        private void CompleteOnce(string result, Action<string> onCompleted)
        {
            if (completed)
                return;
            completed = true;
            Debug.Log($"[MockScenarioGame] 完了結果: {result}", this);
            onCompleted?.Invoke(result);
        }

        private void OnDisable()
        {
            if (completionCoroutine != null)
                StopCoroutine(completionCoroutine);
            completionCoroutine = null;
        }
    }
}
