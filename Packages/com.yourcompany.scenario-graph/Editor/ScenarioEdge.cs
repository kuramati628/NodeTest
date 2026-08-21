using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace ScenarioGraphSystem.Editor
{
    /// <summary>自己接続と右から左へ戻る接続を外周へ逃がして描画するEdgeです。</summary>
    internal sealed class ScenarioEdge : Edge
    {
        protected override EdgeControl CreateEdgeControl() => new ScenarioEdgeControl(this)
        {
            capRadius = 4f,
            interceptWidth = 8f
        };
    }

    /// <summary>接続方向に応じてベジェ制御点を調整し、ループの視認性を確保します。</summary>
    internal sealed class ScenarioEdgeControl : EdgeControl
    {
        private readonly ScenarioEdge owner;

        public ScenarioEdgeControl(ScenarioEdge owner) => this.owner = owner;

        protected override void ComputeControlPoints()
        {
            base.ComputeControlPoints();
            var points = controlPoints;
            if (points == null || points.Length != 4 || owner.output == null || owner.input == null)
                return;

            var sameNode = owner.output.node == owner.input.node;
            if (sameNode)
            {
                // 自己接続はノード右上の外周を大きく回り、ノード本体との重なりを避けます。
                points[1] = points[0] + new Vector2(140f, -110f);
                points[2] = points[3] + new Vector2(140f, -110f);
            }
            else if (points[0].x >= points[3].x)
            {
                // 戻りEdgeは両端から十分外へ出してから接続先へ戻します。
                var escape = Mathf.Max(120f, Mathf.Abs(points[0].x - points[3].x) * 0.35f);
                points[1] = points[0] + Vector2.right * escape;
                points[2] = points[3] + Vector2.left * escape;
            }
        }
    }
}
