using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>
    /// シナリオ再生に渡す設定アセットです。
    /// CSVの解釈は行わず、既存のシナリオ再生システムへ渡す参照だけを保持します。
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioDefinition", menuName = "Scenario/Scenario Definition")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        [SerializeField] private TextAsset csv;

        public TextAsset Csv => csv;
    }
}
