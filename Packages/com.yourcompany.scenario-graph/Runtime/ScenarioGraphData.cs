using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>シナリオグラフで利用できるノード種別です。</summary>
    public enum ScenarioNodeType
    {
        Start,
        Scenario,
        Game
    }

    /// <summary>すべてのゲームが返す共通の終了結果です。必要に応じて値を末尾へ追加してください。</summary>
    public enum GameResult
    {
        Success,
        Failure,
        Cancelled
    }

    /// <summary>ノードの将来拡張用メタデータです。</summary>
    [Serializable]
    public sealed class NodeMetadata
    {
        [SerializeField, TextArea] private string description = string.Empty;
        [SerializeField] private List<string> tags = new();
        [SerializeField, TextArea] private string customData = string.Empty;

        public string Description { get => description; set => description = value ?? string.Empty; }
        public List<string> Tags => tags;
        public string CustomData { get => customData; set => customData = value ?? string.Empty; }
    }

    /// <summary>ゲームノードの出力ポートと、そのポートに対応する結果を保存します。</summary>
    [Serializable]
    public sealed class OutputPortData
    {
        [SerializeField] private string guid;
        [SerializeField] private string displayName;
        [SerializeField] private GameResult gameResult;

        public string Guid => guid;
        public string DisplayName { get => displayName; set => displayName = value ?? string.Empty; }
        public GameResult GameResult { get => gameResult; set => gameResult = value; }

        /// <summary>新しい不変GUIDを持つ出力ポートを生成します。</summary>
        public static OutputPortData Create(string name, GameResult result = GameResult.Success)
        {
            return new OutputPortData
            {
                guid = System.Guid.NewGuid().ToString("N"),
                displayName = name ?? string.Empty,
                gameResult = result
            };
        }
    }

    /// <summary>GraphViewとは独立して永続化されるノードデータです。</summary>
    [Serializable]
    public sealed class NodeData
    {
        [SerializeField] private string guid;
        [SerializeField] private string displayName;
        [SerializeField] private ScenarioNodeType nodeType;
        [SerializeField] private Rect position = new(0, 0, 260, 180);
        [SerializeField] private bool collapsed;
        [SerializeField] private NodeMetadata metadata = new();
        [SerializeField] private ScenarioDefinition scenarioDefinition;
        [SerializeField] private GameRegistry gameRegistry;
        [SerializeField] private string gameId;
        [SerializeField] private GameData gameData;
        [SerializeField] private List<OutputPortData> outputPorts = new();

        public string Guid => guid;
        public string DisplayName { get => displayName; set => displayName = value ?? string.Empty; }
        public ScenarioNodeType NodeType => nodeType;
        public Rect Position { get => position; set => position = value; }
        public bool Collapsed { get => collapsed; set => collapsed = value; }
        public NodeMetadata Metadata => metadata;
        public ScenarioDefinition ScenarioDefinition { get => scenarioDefinition; set => scenarioDefinition = value; }
        public GameRegistry GameRegistry { get => gameRegistry; set => gameRegistry = value; }
        public string GameId { get => gameId; set => gameId = value ?? string.Empty; }
        public GameData GameData { get => gameData; set => gameData = value; }
        public List<OutputPortData> OutputPorts => outputPorts;

        /// <summary>種別に応じた初期ポートを持つ新規ノードを生成します。</summary>
        public static NodeData Create(ScenarioNodeType type, Vector2 position)
        {
            var node = new NodeData
            {
                guid = System.Guid.NewGuid().ToString("N"),
                nodeType = type,
                displayName = type switch
                {
                    ScenarioNodeType.Start => "開始",
                    ScenarioNodeType.Scenario => "シナリオ",
                    ScenarioNodeType.Game => "ゲーム",
                    _ => "ノード"
                },
                position = new Rect(position, new Vector2(260, type == ScenarioNodeType.Game ? 230 : 180))
            };

            if (type != ScenarioNodeType.Game)
                node.outputPorts.Add(OutputPortData.Create("次へ"));
            return node;
        }

        /// <summary>コピー＆ペースト用に参照設定を維持しつつ、全GUIDを再発行して複製します。</summary>
        public NodeData CloneWithNewGuids(Vector2 offset)
        {
            var clone = Create(nodeType, position.position + offset);
            clone.displayName = displayName + " Copy";
            clone.position = new Rect(position.position + offset, position.size);
            clone.collapsed = collapsed;
            clone.metadata.Description = metadata.Description;
            clone.metadata.Tags.AddRange(metadata.Tags);
            clone.metadata.CustomData = metadata.CustomData;
            clone.scenarioDefinition = scenarioDefinition;
            clone.gameRegistry = gameRegistry;
            clone.gameId = gameId;
            clone.gameData = gameData;
            clone.outputPorts.Clear();
            foreach (var port in outputPorts)
                clone.outputPorts.Add(OutputPortData.Create(port.DisplayName, port.GameResult));
            return clone;
        }
    }

    /// <summary>ポート間の接続を表す永続Edgeデータです。</summary>
    [Serializable]
    public sealed class EdgeData
    {
        [SerializeField] private string guid;
        [SerializeField] private string outputNodeGuid;
        [SerializeField] private string outputPortGuid;
        [SerializeField] private string inputNodeGuid;

        public string Guid => guid;
        public string OutputNodeGuid => outputNodeGuid;
        public string OutputPortGuid => outputPortGuid;
        public string InputNodeGuid => inputNodeGuid;

        /// <summary>新しい接続データを生成します。</summary>
        public static EdgeData Create(string outputNodeGuid, string outputPortGuid, string inputNodeGuid)
        {
            return new EdgeData
            {
                guid = System.Guid.NewGuid().ToString("N"),
                outputNodeGuid = outputNodeGuid,
                outputPortGuid = outputPortGuid,
                inputNodeGuid = inputNodeGuid
            };
        }
    }

    /// <summary>グループの表示情報と所属ノードGUIDを保存します。</summary>
    [Serializable]
    public sealed class GroupData
    {
        [SerializeField] private string guid;
        [SerializeField] private string title;
        [SerializeField] private Rect position;
        [SerializeField] private List<string> nodeGuids = new();

        public string Guid => guid;
        public string Title { get => title; set => title = value ?? string.Empty; }
        public Rect Position { get => position; set => position = value; }
        public List<string> NodeGuids => nodeGuids;

        public static GroupData Create(Vector2 position) => new()
        {
            guid = System.Guid.NewGuid().ToString("N"), title = "グループ", position = new Rect(position, new Vector2(500, 300))
        };
    }

    /// <summary>グラフ上へ独立配置できるコメントデータです。</summary>
    [Serializable]
    public sealed class CommentData
    {
        [SerializeField] private string guid;
        [SerializeField, TextArea] private string text;
        [SerializeField] private Rect position;

        public string Guid => guid;
        public string Text { get => text; set => text = value ?? string.Empty; }
        public Rect Position { get => position; set => position = value; }

        public static CommentData Create(Vector2 position) => new()
        {
            guid = System.Guid.NewGuid().ToString("N"), text = "コメント", position = new Rect(position, new Vector2(280, 140))
        };
    }

    /// <summary>エディタの表示状態です。実行中状態は含みません。</summary>
    [Serializable]
    public sealed class GraphEditorState
    {
        [SerializeField] private Vector3 viewPosition;
        [SerializeField] private Vector3 viewScale = Vector3.one;
        [SerializeField] private bool minimapVisible = true;

        public Vector3 ViewPosition { get => viewPosition; set => viewPosition = value; }
        public Vector3 ViewScale { get => viewScale; set => viewScale = value; }
        public bool MinimapVisible { get => minimapVisible; set => minimapVisible = value; }
    }
}
