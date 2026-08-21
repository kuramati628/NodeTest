using System.Collections.Generic;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>ノード、接続、グループ、コメントを単一アセットに保持するシナリオグラフです。</summary>
    [CreateAssetMenu(fileName = "ScenarioGraph", menuName = "Scenario/Scenario Graph")]
    public sealed class ScenarioGraph : ScriptableObject
    {
        [SerializeField, HideInInspector] private string graphGuid;
        [SerializeField, HideInInspector] private string startNodeGuid;
        [SerializeField] private List<NodeData> nodes = new();
        [SerializeField] private List<EdgeData> edges = new();
        [SerializeField] private List<GroupData> groups = new();
        [SerializeField] private List<CommentData> comments = new();
        [SerializeField] private GraphEditorState editorState = new();

        public string GraphGuid => graphGuid;
        public string StartNodeGuid { get => startNodeGuid; set => startNodeGuid = value ?? string.Empty; }
        public List<NodeData> Nodes => nodes;
        public List<EdgeData> Edges => edges;
        public List<GroupData> Groups => groups;
        public List<CommentData> Comments => comments;
        public GraphEditorState EditorState => editorState;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(graphGuid))
                graphGuid = System.Guid.NewGuid().ToString("N");
        }

        /// <summary>GUIDからノードを検索します。</summary>
        public NodeData FindNode(string guid) => nodes.Find(node => node.Guid == guid);

        /// <summary>開始ノードGUIDを優先し、未設定時は開始型のノードを返します。</summary>
        public NodeData GetStartNode()
        {
            var node = FindNode(startNodeGuid);
            return node != null && node.NodeType == ScenarioNodeType.Start
                ? node
                : nodes.Find(candidate => candidate.NodeType == ScenarioNodeType.Start);
        }
    }
}
