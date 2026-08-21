using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>ゲームごとに異なる設定アセットの共通基底型です。</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Enum)]
    public sealed class SentenceBranchEnumAttribute : Attribute
    {
    }

    /// <summary>
    /// ゲームへ渡す文章データの基底型です。派生型内のenumを分岐として公開します。
    /// enumが複数ある場合は対象のenum、フィールド、またはプロパティへSentenceBranchEnumを付けます。
    /// </summary>
    public abstract class SentenceData : ScriptableObject
    {
        public virtual Type GetBranchEnumType()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = GetType();
            var attributed = type.GetFields(flags)
                .Where(field => field.FieldType.IsEnum && field.GetCustomAttribute<SentenceBranchEnumAttribute>() != null)
                .Select(field => field.FieldType)
                .Concat(type.GetProperties(flags)
                    .Where(property => property.PropertyType.IsEnum && property.GetCustomAttribute<SentenceBranchEnumAttribute>() != null)
                    .Select(property => property.PropertyType))
                .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(nested => nested.IsEnum && nested.GetCustomAttribute<SentenceBranchEnumAttribute>() != null))
                .Distinct()
                .ToList();
            if (attributed.Count == 1)
                return attributed[0];
            if (attributed.Count > 1)
                return null;

            var candidates = type.GetFields(flags)
                .Where(field => field.FieldType.IsEnum)
                .Select(field => field.FieldType)
                .Concat(type.GetProperties(flags)
                    .Where(property => property.PropertyType.IsEnum)
                    .Select(property => property.PropertyType))
                .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Where(nested => nested.IsEnum))
                .Distinct()
                .ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        public virtual IReadOnlyList<string> GetBranchNames()
        {
            var enumType = GetBranchEnumType();
            return enumType != null && enumType.IsEnum ? Enum.GetNames(enumType) : Array.Empty<string>();
        }
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
