using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ScenarioGraphSystem.Editor.Spreadsheet
{
    /// <summary>Import Profileの検証結果とSpreadsheet取得ボタンを表示します。</summary>
    [CustomEditor(typeof(ScenarioSpreadsheetImportProfile))]
    internal sealed class ScenarioSpreadsheetImportProfileInspector : UnityEditor.Editor
    {
        private bool importing;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var profile = (ScenarioSpreadsheetImportProfile)target;
            var validationMessage = ScenarioDefinitionCsvImporter.Validate(profile);
            if (!string.IsNullOrEmpty(validationMessage))
                EditorGUILayout.HelpBox(validationMessage, MessageType.Error);

            using (new EditorGUI.DisabledScope(importing || EditorApplication.isPlayingOrWillChangePlaymode ||
                                                !string.IsNullOrEmpty(validationMessage)))
            {
                if (GUILayout.Button(importing ? "取得中..." : "SpreadsheetからCSVを更新"))
                    ImportAsync(profile).Forget();
            }

            EditorGUILayout.HelpBox(
                "同じ出力パスへCSVを上書きするため、TextAssetのGUIDとScenarioDefinition参照は維持されます。",
                MessageType.Info);
        }

        private async UniTaskVoid ImportAsync(ScenarioSpreadsheetImportProfile profile)
        {
            importing = true;
            Repaint();
            try
            {
                var result = await ScenarioDefinitionCsvImporter.ImportAsync(profile);
                if (result.Succeeded)
                {
                    Debug.Log($"[Scenario Spreadsheet] {result.Message}", profile);
                    EditorUtility.DisplayDialog("Spreadsheet Import", result.Message, "OK");
                }
                else
                {
                    Debug.LogError($"[Scenario Spreadsheet] {result.Message}", profile);
                    EditorUtility.DisplayDialog("Spreadsheet Import Error", result.Message, "OK");
                }
            }
            finally
            {
                importing = false;
                Repaint();
            }
        }
    }

    /// <summary>Credentialに機密情報の扱いに関する注意を表示します。</summary>
    [CustomEditor(typeof(GoogleSheetsCredential))]
    internal sealed class GoogleSheetsCredentialInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(
                "APIキーをGitへ保存したくない場合は、Fallback API Keyを空にして環境変数を使用してください。APIキーはGoogle Cloud側でSheets APIのみに制限してください。",
                MessageType.Warning);
        }
    }
}
