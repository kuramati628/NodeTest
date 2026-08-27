using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ScenarioGraphSystem
{
    /// <summary>旧MockGameDataアセットのenumリストを読み取るための互換定義です。</summary>
    public enum MockSentenceResult
    {
        Success,
        Failure,
        Cancelled
    }

    /// <summary>ScenarioGraphの接続確認に使う、完了結果と待機時間だけを持つゲーム設定モックです。</summary>
    [CreateAssetMenu(fileName = "MockGameData", menuName = "Scenario/Mock/Mock Game Data")]
    public sealed class MockGameData : SentenceData
    {
        [SerializeField, HideInInspector] private List<MockSentenceResult> branches = new();
        [SerializeField] private List<string> branchNames = new()
        {
            "Success",
            "Failure",
            "Cancelled"
        };
        [SerializeField, HideInInspector] private MockSentenceResult completionResult = MockSentenceResult.Success;
        [SerializeField] private string completionBranchName = "Success";
        [SerializeField, Min(0f)] private float completionDelaySeconds = 0.25f;

        public string CompletionResult => !string.IsNullOrWhiteSpace(completionBranchName)
            ? completionBranchName
            : completionResult.ToString();
        public float CompletionDelaySeconds => completionDelaySeconds;
        public IReadOnlyList<string> Branches => GetBranchNames();

        public override IReadOnlyList<string> GetBranchNames()
        {
            var configuredBranches = branchNames != null && branchNames.Count > 0
                ? branchNames
                : (branches ?? new List<MockSentenceResult>()).Select(branch => branch.ToString()).ToList();
            return configuredBranches.Where(branch => !string.IsNullOrWhiteSpace(branch))
                .Select(branch => branch.Trim()).Distinct().ToList();
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(MockGameData))]
    internal sealed class MockGameDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var legacyBranches = serializedObject.FindProperty("branches");
            var branches = serializedObject.FindProperty("branchNames");
            var legacyCompletionResult = serializedObject.FindProperty("completionResult");
            var completionResult = serializedObject.FindProperty("completionBranchName");
            var completionDelay = serializedObject.FindProperty("completionDelaySeconds");

            if (branches.arraySize == 0 && legacyBranches.arraySize > 0)
            {
                for (var index = 0; index < legacyBranches.arraySize; index++)
                {
                    var legacyValue = (MockSentenceResult)legacyBranches.GetArrayElementAtIndex(index).enumValueIndex;
                    branches.InsertArrayElementAtIndex(branches.arraySize);
                    branches.GetArrayElementAtIndex(branches.arraySize - 1).stringValue = legacyValue.ToString();
                }
                completionResult.stringValue = ((MockSentenceResult)legacyCompletionResult.enumValueIndex).ToString();
            }

            UnityEditor.EditorGUILayout.PropertyField(branches, new UnityEngine.GUIContent("Branches"), true);
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();

            var branchNames = new List<string>();
            for (var index = 0; index < branches.arraySize; index++)
            {
                var branchName = branches.GetArrayElementAtIndex(index).stringValue?.Trim();
                if (!string.IsNullOrEmpty(branchName) && !branchNames.Contains(branchName))
                    branchNames.Add(branchName);
            }

            if (branchNames.Count == 0)
            {
                UnityEditor.EditorGUILayout.HelpBox("Branchesへ1件以上の名称を入力してください。", UnityEditor.MessageType.Warning);
            }
            else
            {
                var selectedIndex = branchNames.IndexOf(completionResult.stringValue);
                selectedIndex = UnityEditor.EditorGUILayout.Popup("Completion Result", Mathf.Max(0, selectedIndex), branchNames.ToArray());
                completionResult.stringValue = branchNames[selectedIndex];
            }

            UnityEditor.EditorGUILayout.PropertyField(completionDelay, new UnityEngine.GUIContent("Completion Delay Seconds"));
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
