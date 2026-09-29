using AwesomeAssertions;
using Core.Editor.Localization;
using Core.Localization;
using Core.Results;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Localization
{
    public sealed class TextKeyGeneratorTests
    {
        private const string TableGuid = "0123456789abcdef0123456789abcdef";
        private const string OtherTableGuid = "fedcba9876543210fedcba9876543210";
        private const string OutputPath = "Assets/_Project/Domains/Sample/Code/SampleText.g.cs";
        private const string MovedOutputPath = "Assets/_Project/Domains/Other/Code/SampleText.g.cs";

        private LocalizationTable _table = null!;

        [SetUp]
        public void SetUp()
        {
            _table = TestLocalizationTables.Create("Sample", ("play", "Play", "Graj"));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_table);
        }

        [Test]
        public void BuildSource_Table_HasNoCommentsAndCarriesTableGuid()
        {
            // Act
            var source = BuildSource();

            // Assert
            source.Should().NotContain("//").And.NotContain("/*");
            source.Should().Contain($"internal const string TableGuid = \"{TableGuid}\";");
        }

        [Test]
        public void BuildSource_KeyNamedLikeTableGuidConstant_ReturnsError()
        {
            // Arrange
            var table = TestLocalizationTables.Create("Sample", ("table_guid", "Guid", "Guid"));

            try
            {
                // Act
                var result = TextKeyGenerator.BuildSource(table, "Sample.asset", TableGuid, "Sample", "SampleText", false);

                // Assert
                result.Should().BeCase<Error>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void IsStaleOutput_CurrentOutputOfTable_ReturnsFalse()
        {
            // Act
            var isStale = TextKeyGenerator.IsStaleOutput(OutputPath, BuildSource(), TableGuid, OutputPath, _ => true);

            // Assert
            isStale.Should().BeFalse();
        }

        [Test]
        public void IsStaleOutput_SameTableAtOtherPath_ReturnsTrue()
        {
            // Act
            var isStale = TextKeyGenerator.IsStaleOutput(OutputPath, BuildSource(), TableGuid, MovedOutputPath, _ => true);

            // Assert
            isStale.Should().BeTrue();
        }

        [Test]
        public void IsStaleOutput_OtherLiveTable_ReturnsFalse()
        {
            // Act
            var isStale = TextKeyGenerator.IsStaleOutput(OutputPath, BuildSource(), OtherTableGuid, MovedOutputPath, guid => guid == TableGuid);

            // Assert
            isStale.Should().BeFalse();
        }

        [Test]
        public void IsStaleOutput_TableDeleted_ReturnsTrue()
        {
            // Act
            var isStale = TextKeyGenerator.IsStaleOutput(OutputPath, BuildSource(), null, null, _ => false);

            // Assert
            isStale.Should().BeTrue();
        }

        [Test]
        public void IsStaleOutput_FileWithoutTableGuid_ReturnsFalse()
        {
            // Arrange
            const string source = "namespace Sample\n{\n    internal static class Other\n    {\n    }\n}\n";

            // Act
            var isStale = TextKeyGenerator.IsStaleOutput(OutputPath, source, null, null, _ => false);

            // Assert
            isStale.Should().BeFalse();
        }

        private string BuildSource()
        {
            return TextKeyGenerator.BuildSource(_table, "Sample.asset", TableGuid, "Sample", "SampleText", false).Should().BeCase<string>().Which;
        }
    }
}
