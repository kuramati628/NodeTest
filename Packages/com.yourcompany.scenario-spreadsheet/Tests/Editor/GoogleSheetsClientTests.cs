using System.Linq;
using NUnit.Framework;

namespace ScenarioGraphSystem.Editor.Spreadsheet.Tests
{
    public sealed class GoogleSheetsClientTests
    {
        [Test]
        public void SelectTargetSheets_ExcludesTemplateSheetsAndNonGridSheets()
        {
            var metadata = new GoogleSpreadsheetMetadata
            {
                sheets = new[]
                {
                    Sheet(10, "使い方", "GRID"),
                    Sheet(11, "ForCopy", "GRID"),
                    Sheet(12, "1", "GRID"),
                    Sheet(13, "グラフ", "OBJECT"),
                    Sheet(14, "2", "GRID")
                }
            };

            var targets = GoogleSheetsClient.SelectTargetSheets(metadata, new[] { "使い方", "forcopy" });

            CollectionAssert.AreEqual(new[] { "1", "2" }, targets.Select(sheet => sheet.title));
            CollectionAssert.AreEqual(new[] { 12, 14 }, targets.Select(sheet => sheet.sheetId));
        }

        private static GoogleSheetWrapper Sheet(int id, string title, string type) => new()
        {
            properties = new GoogleSheetProperties { sheetId = id, title = title, sheetType = type }
        };
    }
}
