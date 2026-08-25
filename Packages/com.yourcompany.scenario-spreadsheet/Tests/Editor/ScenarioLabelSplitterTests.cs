using System;
using System.Linq;
using NUnit.Framework;

namespace ScenarioGraphSystem.Editor.Spreadsheet.Tests
{
    public sealed class ScenarioLabelSplitterTests
    {
        [Test]
        public void HasDefinition_ReturnsFalseForLegacyCsv()
        {
            var source = Sheet(Row("Text", "Player", "従来形式"), Row("End"));

            Assert.That(ScenarioLabelSplitter.HasDefinition(source), Is.False);
        }

        [Test]
        public void SplitBlocks_CreatesNumberedBlocksAndRemovesGoToGame()
        {
            var source = Sheet(
                Row("Text", "Doctor", "before 1"),
                Row("GoToGame"),
                Row("DefineLabel", "Success"),
                Row("Label", "Success"),
                Row("Text", "Doctor", "before 2 belongs to previous label"),
                Row("GoToGame"),
                Row("DefineLabel", "HugeSuccess"),
                Row("Label", "HugeSuccess"),
                Row("End"),
                Row("ShakingScreen"));

            var blocks = ScenarioLabelSplitter.SplitBlocks(source);

            Assert.That(blocks.Count, Is.EqualTo(2));
            Assert.That(blocks[0].BlockNumber, Is.EqualTo(1));
            Assert.That(blocks[1].BlockNumber, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "Text" }, blocks[0].PrefixRows.Select(row => row[0]));
            Assert.That(blocks[1].PrefixRows, Is.Empty);
            Assert.That(blocks.SelectMany(block => block.PrefixRows)
                .Any(row => row[0] == "GoToGame"), Is.False);
            CollectionAssert.AreEqual(
                new[] { "Text" },
                blocks[0].Sections[0].Rows.Select(row => row[0]));
            Assert.That(blocks[0].Sections[0].EndsWithGoToGame, Is.True);
            CollectionAssert.AreEqual(
                new[] { "End", "ShakingScreen" },
                blocks[1].Sections[0].Rows.Select(row => row[0]));
            Assert.That(blocks[1].Sections[0].EndsWithGoToGame, Is.False);
        }

        [Test]
        public void SplitBlocks_RejectsNextDefinitionWithoutPreviousGoToGame()
        {
            var source = Sheet(
                Row("GoToGame"),
                Row("DefineLabel", "Success"),
                Row("Label", "Success"),
                Row("Text", "Doctor", "not transitioned"),
                Row("DefineLabel", "Next"),
                Row("Label", "Next"),
                Row("End"));

            var exception = Assert.Throws<InvalidOperationException>(() => ScenarioLabelSplitter.SplitBlocks(source));
            StringAssert.Contains("GoToGame", exception.Message);
        }

        [Test]
        public void SplitBlocks_RejectsPrefixWithoutGoToGame()
        {
            var source = Sheet(
                Row("Text", "Doctor", "before"),
                Row("DefineLabel", "Success"),
                Row("Label", "Success"),
                Row("End"));

            var exception = Assert.Throws<InvalidOperationException>(() => ScenarioLabelSplitter.SplitBlocks(source));
            StringAssert.Contains("GoToGame", exception.Message);
        }

        [Test]
        public void Split_RemovesControlRowsAndPreservesDeclaredOrder()
        {
            var source = Sheet(
                Row("DefineLabel", "Success", "HugeSuccess", "Label_End"),
                Row("Label", "Success"),
                Row("TextGameResult", "TextCharaA"),
                Row("Text", "Doctor", "今回は許してやる"),
                Row("jump", "Label_End"),
                Row("Label", "HugeSuccess"),
                Row("TextGameResult", "TextCharaA"),
                Row("Text", "Player", "誠意はあるようだな"),
                Row("jump", "Label_End"),
                Row("Label", "Label_End"),
                Row("ShowBlackBelt", "FALSE"),
                Row("End"));

            var sections = ScenarioLabelSplitter.Split(source);

            CollectionAssert.AreEqual(
                new[] { "Success", "HugeSuccess", "Label_End" },
                sections.Select(section => section.Label));
            Assert.That(sections[0].JumpTarget, Is.EqualTo("Label_End"));
            Assert.That(sections[1].JumpTarget, Is.EqualTo("Label_End"));
            Assert.That(sections[2].JumpTarget, Is.Empty);
            CollectionAssert.AreEqual(
                new[] { "TextGameResult", "Text" },
                sections[0].Rows.Select(row => row[0]));
            CollectionAssert.AreEqual(
                new[] { "ShowBlackBelt", "End" },
                sections[2].Rows.Select(row => row[0]));
        }

        [Test]
        public void Split_RejectsUndefinedJumpTarget()
        {
            var source = Sheet(
                Row("DefineLabel", "Success"),
                Row("Label", "Success"),
                Row("Text", "Player", "text"),
                Row("jump", "Missing"));

            var exception = Assert.Throws<InvalidOperationException>(() => ScenarioLabelSplitter.Split(source));
            StringAssert.Contains("DefineLabelで宣言されていません", exception.Message);
        }

        [Test]
        public void Split_RejectsExecutableRowAfterJump()
        {
            var source = Sheet(
                Row("DefineLabel", "Success"),
                Row("Label", "Success"),
                Row("jump", "Success"),
                Row("Text", "Player", "到達しない行"));

            var exception = Assert.Throws<InvalidOperationException>(() => ScenarioLabelSplitter.Split(source));
            StringAssert.Contains("jumpの後", exception.Message);
        }

        [Test]
        public void Split_RejectsMissingDeclaredLabel()
        {
            var source = Sheet(
                Row("DefineLabel", "Success", "Label_End"),
                Row("Label", "Success"),
                Row("End"));

            var exception = Assert.Throws<InvalidOperationException>(() => ScenarioLabelSplitter.Split(source));
            StringAssert.Contains("Label_End", exception.Message);
        }

        private static GoogleSheetData Sheet(params string[][] rows) => new() { values = rows };
        private static string[] Row(params string[] cells) => cells;
    }
}
