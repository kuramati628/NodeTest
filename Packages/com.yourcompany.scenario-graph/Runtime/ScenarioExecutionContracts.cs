namespace ScenarioGraphSystem
{
    /// <summary>
    /// Graph Editorから選択ノードを単体デバッグする、プロジェクト側Play Modeホストの契約です。
    /// Sceneの準備、シナリオ再生、ゲーム実行はホスト側で行います。
    /// </summary>
    public interface IScenarioGraphDebugHost
    {
        bool CanDebugNode(ScenarioGraph graph, NodeData node, out string reason);
        void DebugNode(ScenarioGraph graph, NodeData node);
    }
}
