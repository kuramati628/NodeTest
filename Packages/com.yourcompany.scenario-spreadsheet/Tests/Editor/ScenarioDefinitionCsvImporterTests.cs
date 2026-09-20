using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ScenarioGraphSystem.Editor.Spreadsheet.Tests
{
    public sealed class ScenarioDefinitionCsvImporterTests
    {
        private ScenarioSpreadsheetImportProfile profile;

        [SetUp]
        public void SetUp() => profile = ScriptableObject.CreateInstance<ScenarioSpreadsheetImportProfile>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(profile);

        [Test]
        public void BuildOutputs_ResolvesJumpsAcrossActSheetsAndEndsTerminalSections()
        {
            var spreadsheet = Book(
                Act(1, "1-1", "1-2", "2-1"),
                Act(2, "2-1", "2-2", "3-1"),
                Act(3, "3-1", "3-2", "4-1"),
                new GoogleSheetData
                {
                    sheetId = 4,
                    title = "Act4",
                    values = new[]
                    {
                        Row("", "Label", "4-1"), Row("", "Text", "Doctor", "opening"),
                        Row("", "GoToGame"), Row("", "DefineLabel", "", "4-2", "4-3"),
                        Row("", "Label", "4-2"), Row("", "End"),
                        Row("", "Label", "4-3"), Row("", "End")
                    }
                });

            var outputs = ScenarioDefinitionCsvImporter.BuildOutputs(profile, spreadsheet);
            var byLabel = outputs.ToDictionary(output => output.Label);

            Assert.That(outputs.Count, Is.EqualTo(9));
            foreach (var pair in new[] { ("1-2", "2-1"), ("2-2", "3-1"), ("3-2", "4-1") })
                Assert.That(byLabel[pair.Item1].TransitionTargetKey, Is.EqualTo(byLabel[pair.Item2].StableKey));
            Assert.That(byLabel["1-1"].ManualGameTransition, Is.True);
            Assert.That(byLabel["4-2"].TransitionTargetKey, Is.Empty);
            Assert.That(byLabel["4-3"].TransitionTargetKey, Is.Empty);
            Assert.That(byLabel["1-1"].CsvPath, Does.EndWith("/CSV/Act1/1-1.csv"));
        }

        [Test]
        public void BuildOutputs_RejectsMissingOrDuplicateLabelsBeforeWriting()
        {
            var missing = Assert.Throws<InvalidOperationException>(() =>
                ScenarioDefinitionCsvImporter.BuildOutputs(profile, Book(Act(1, "1-1", "1-2", "missing"))));
            StringAssert.Contains("missing", missing.Message);

            var duplicate = Assert.Throws<InvalidOperationException>(() =>
                ScenarioDefinitionCsvImporter.BuildOutputs(profile, Book(
                    Act(1, "1-1", "1-2", "1-1"), Act(2, "1-1", "2-2", "1-2"))));
            StringAssert.Contains("重複", duplicate.Message);
        }

        private static GoogleSpreadsheetData Book(params GoogleSheetData[] sheets) => new()
        {
            spreadsheetId = "book",
            title = "Stage1",
            sheets = sheets
        };

        private static GoogleSheetData Act(int id, string first, string result, string jump) => new()
        {
            sheetId = id,
            title = $"Act{id}",
            values = new[]
            {
                Row("", "Label", first), Row("", "Text", "Doctor", "opening"),
                Row("", "GoToGame"), Row("", "DefineLabel", "", result),
                Row("", "Label", result), Row("", "Text", "Doctor", "result"),
                Row("", "jump", jump)
            }
        };

        private static string[] Row(params string[] cells) => cells;
    }
}
