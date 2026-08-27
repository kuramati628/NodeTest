using System;
using System.Collections.Generic;
using System.Linq;
using R3;
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

        /// <summary>購読中だけ指定シーンをロードし、解決したゲーム実装を1回発行します。</summary>
        public Observable<IScenarioGame> LoadGame(SceneReference sceneReference)
        {
            return Observable.Create<IScenarioGame>(observer =>
            {
                var version = ++operationVersion;
                if (sceneReference == null || !sceneReference.IsAssigned)
                {
                    NotifyError(observer, "ゲームシーンが未設定です。");
                    return new SceneLease(this, version);
                }

                if (SceneUtility.GetBuildIndexByScenePath(sceneReference.ScenePath) < 0)
                {
                    NotifyError(observer, $"ゲームシーン『{sceneReference.ScenePath}』がBuild Settingsに登録されていません。");
                    return new SceneLease(this, version);
                }

                BeginTransitionLoad(sceneReference.ScenePath, version, observer);
                return new SceneLease(this, version);
            });
        }

        private void Cancel(int version)
        {
            if (version != operationVersion)
                return;
            operationVersion++;
            UnloadLoadedScene();
        }

        private void BeginTransitionLoad(string scenePath, int version, Observer<IScenarioGame> observer)
        {
            if (!currentScene.IsValid() || !currentScene.isLoaded)
            {
                currentScene = default;
                BeginLoad(scenePath, version, observer);
                return;
            }

            var scene = currentScene;
            currentScene = default;
            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation == null)
            {
                BeginLoad(scenePath, version, observer);
                return;
            }

            operation.completed += _ =>
            {
                if (version == operationVersion)
                    BeginLoad(scenePath, version, observer);
            };
        }

        private void BeginLoad(string scenePath, int version, Observer<IScenarioGame> observer)
        {
            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                NotifyError(observer, $"ゲームシーンの読み込み開始に失敗しました: {exception.Message}");
                return;
            }

            if (operation == null)
            {
                NotifyError(observer, $"ゲームシーン『{scenePath}』を読み込めませんでした。");
                return;
            }

            operation.completed += _ => CompleteLoad(scenePath, version, observer);
        }

        private void CompleteLoad(string scenePath, int version, Observer<IScenarioGame> observer)
        {
            var loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                if (version == operationVersion)
                    NotifyError(observer, $"ゲームシーン『{scenePath}』の読み込みに失敗しました。");
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
                NotifyError(observer, games.Count == 0
                    ? $"ゲームシーン『{scenePath}』にIScenarioGame実装がありません。"
                    : $"ゲームシーン『{scenePath}』にIScenarioGame実装が複数あります。");
                Cancel(version);
                return;
            }

            observer.OnNext(games[0]);
        }

        private static void NotifyError(Observer<IScenarioGame> observer, string message)
        {
            observer.OnCompleted(Result.Failure(new InvalidOperationException(message)));
        }

        private static List<IScenarioGame> FindGames(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .OfType<IScenarioGame>()
                .ToList();
        }

        private void UnloadLoadedScene()
        {
            if (!currentScene.IsValid() || !currentScene.isLoaded)
            {
                currentScene = default;
                return;
            }

            var scene = currentScene;
            currentScene = default;
            SceneManager.UnloadSceneAsync(scene);
        }

        private sealed class SceneLease : IDisposable
        {
            private UnityScenarioGameSceneService owner;
            private readonly int version;

            public SceneLease(UnityScenarioGameSceneService owner, int version)
            {
                this.owner = owner;
                this.version = version;
            }

            public void Dispose()
            {
                var target = owner;
                owner = null;
                target?.Cancel(version);
            }
        }
    }
}
