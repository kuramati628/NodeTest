using System;
using NUnit.Framework;

namespace ScenarioGraphSystem.Editor.Spreadsheet.Tests
{
    public sealed class ScenarioGeneratedPathBuilderTests
    {
        [Test]
        public void Build_WithoutLabel_SeparatesCsvAndAssetFolders()
        {
            var paths = ScenarioGeneratedPathBuilder.Build(
                "Assets/ScenarioData/Generated", "テスト用", "1", string.Empty);

            Assert.That(paths.CsvPath, Is.EqualTo("Assets/ScenarioData/Generated/テスト用/CSV/1.csv"));
            Assert.That(paths.DefinitionPath, Is.EqualTo("Assets/ScenarioData/Generated/テスト用/Asset/1.asset"));
        }

        [Test]
        public void Build_WithLabel_CreatesMatchingSheetSubfolders()
        {
            var paths = ScenarioGeneratedPathBuilder.Build(
                "Assets/ScenarioData/Generated", "テスト用", "2", "HugeSuccess");

            Assert.That(paths.CsvPath, Is.EqualTo("Assets/ScenarioData/Generated/テスト用/CSV/2/HugeSuccess.csv"));
            Assert.That(paths.DefinitionPath, Is.EqualTo("Assets/ScenarioData/Generated/テスト用/Asset/2/HugeSuccess.asset"));
        }

        [Test]
        public void Build_WithBlock_CreatesNumberedMainAndLabelPaths()
        {
            var main = ScenarioGeneratedPathBuilder.Build(
                "Assets/ScenarioData/Generated", "テスト用", "1", "1-2", string.Empty);
            var label = ScenarioGeneratedPathBuilder.Build(
                "Assets/ScenarioData/Generated", "テスト用", "1", "1-2", "Success");

            Assert.That(main.CsvPath, Is.EqualTo("Assets/ScenarioData/Generated/テスト用/CSV/1/1-2.csv"));
            Assert.That(main.DefinitionPath, Is.EqualTo("Assets/ScenarioData/Generated/テスト用/Asset/1/1-2.asset"));
            Assert.That(label.CsvPath, Is.EqualTo("Assets/ScenarioData/Generated/テスト用/CSV/1/1-2/Success.csv"));
            Assert.That(label.DefinitionPath, Is.EqualTo("Assets/ScenarioData/Generated/テスト用/Asset/1/1-2/Success.asset"));
        }

        [Test]
        public void Build_ReplacesPathSeparatorCharacters()
        {
            var paths = ScenarioGeneratedPathBuilder.Build("Assets/Generated", "Test/Book", "Scene:1", "A/B");

            Assert.That(paths.CsvPath, Is.EqualTo("Assets/Generated/Test_Book/CSV/Scene_1/A_B.csv"));
        }

        [Test]
        public void NormalizeOutputFolder_RejectsParentTraversal()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ScenarioGeneratedPathBuilder.NormalizeOutputFolder("Assets/Generated/../Other"));
        }
    }
}
