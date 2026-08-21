using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScenarioGraphSystem
{
    /// <summary>
    /// GameRegistryに登録されたシーンを加算ロードし、そのシーン内のIScenarioGameを解決します。
    /// 1つのゲームシーンにはIScenarioGame実装を持つMonoBehaviourを1個だけ配置してください。
    /// </summary>
    public sealed class UnityScenarioGameSceneService : IScenarioGameSceneService
    {
        private Scene currentScene;
        private int operationVersion;

        /// <summary>現在のゲームシーンを破棄してから、指定シーンを加算ロードします。</summary>
        public void LoadGame(SceneReference sceneReference, Action<IScenarioGame> onLoaded, Action<string> onError)
        {
            if (sceneReference == null || !sceneReference.IsAssigned)
            {
                onError?.Invoke("ゲームシーンが未設定です。");
                return;
            }

            if (SceneUtility.GetBuildIndexByScenePath(sceneReference.ScenePath) < 0)
            {
                onError?.Invoke($"ゲームシーン『{sceneReference.ScenePath}』がBuild Settingsに登録されていません。");
                return;
            }

            var version = ++operationVersion;
            UnloadLoadedScene(() =>
            {
                if (version != operationVersion)
                    return;
                BeginLoad(sceneReference.ScenePath, version, onLoaded, onError);
            });
        }

        /// <summary>ロード中処理を無効化し、現在のゲームシーンをアンロードします。</summary>
        public void UnloadCurrentGame()
        {
            operationVersion++;
            UnloadLoadedScene(null);
        }

        private void BeginLoad(string scenePath, int version, Action<IScenarioGame> onLoaded, Action<string> onError)
        {
            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                onError?.Invoke($"ゲームシーンの読み込み開始に失敗しました: {exception.Message}");
                return;
            }

            if (operation == null)
            {
                onError?.Invoke($"ゲームシーン『{scenePath}』を読み込めませんでした。");
                return;
            }

            operation.completed += _ => CompleteLoad(scenePath, version, onLoaded, onError);
        }

        private void CompleteLoad(string scenePath, int version, Action<IScenarioGame> onLoaded, Action<string> onError)
        {
            var loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                if (version == operationVersion)
                    onError?.Invoke($"ゲームシーン『{scenePath}』の読み込みに失敗しました。");
                return;
            }

            if (version != operationVersion)
            {
                SceneManager.UnloadSceneAsync(loadedScene);
                return;
            }

            currentScene = loadedScene;
            var games = FindGames(loadedScene);
            if (games.Count != 1)
            {
                onError?.Invoke(games.Count == 0
                    ? $"ゲームシーン『{scenePath}』にIScenarioGame実装がありません。"
                    : $"ゲームシーン『{scenePath}』にIScenarioGame実装が複数あります。");
                UnloadCurrentGame();
                return;
            }

            onLoaded?.Invoke(games[0]);
        }

        private static List<IScenarioGame> FindGames(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .OfType<IScenarioGame>()
                .ToList();
        }

        private void UnloadLoadedScene(Action onCompleted)
        {
            if (!currentScene.IsValid() || !currentScene.isLoaded)
            {
                currentScene = default;
                onCompleted?.Invoke();
                return;
            }

            var scene = currentScene;
            currentScene = default;
            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation == null)
                onCompleted?.Invoke();
            else
                operation.completed += _ => onCompleted?.Invoke();
        }
    }
}
