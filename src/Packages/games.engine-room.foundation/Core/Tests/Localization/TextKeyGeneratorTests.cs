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
        private const string MainFolder = "Assets/_Project/Domains/Main";
        private const string MainAsmdefPath = MainFolder + "/Code/Main.asmdef";

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

        [Test]
        public void FindOutputFolder_TableInModuleFolder_ReturnsModuleCodeFolder()
        {
            // Act
            var result = TextKeyGenerator.FindOutputFolder(MainFolder, folder => folder == $"{MainFolder}/Code");

            // Assert
            result.Should().BeCase<string>().Which.Should().Be($"{MainFolder}/Code");
        }

        [Test]
        public void FindOutputFolder_TableInModuleSubfolder_ReturnsSameSubfolderUnderCode()
        {
            // Act
            var result = TextKeyGenerator.FindOutputFolder($"{MainFolder}/Localization", folder => folder == $"{MainFolder}/Code");

            // Assert
            result.Should().BeCase<string>().Which.Should().Be($"{MainFolder}/Code/Localization");
        }

        [Test]
        public void FindOutputFolder_TableInSubDomainWithCodeFolder_ReturnsSubDomainCodeFolder()
        {
            // Act
            var result = TextKeyGenerator.FindOutputFolder($"{MainFolder}/Sub", folder => folder == $"{MainFolder}/Code" || folder == $"{MainFolder}/Sub/Code");

            // Assert
            result.Should().BeCase<string>().Which.Should().Be($"{MainFolder}/Sub/Code");
        }

        [Test]
        public void FindOutputFolder_NoCodeFolder_ReturnsError()
        {
            // Act
            var result = TextKeyGenerator.FindOutputFolder(MainFolder, _ => false);

            // Assert
            result.Should().BeCase<Error>();
        }

        [Test]
        public void BuildNamespace_ModuleCodeFolder_ReturnsRootNamespace()
        {
            // Act
            var result = TextKeyGenerator.BuildNamespace($"{MainFolder}/Code", MainAsmdefPath, "Main");

            // Assert
            result.Should().BeCase<string>().Which.Should().Be("Main");
        }

        [Test]
        public void BuildNamespace_SubfolderOfCode_AppendsSubfolder()
        {
            // Act
            var result = TextKeyGenerator.BuildNamespace($"{MainFolder}/Code/Localization", MainAsmdefPath, "Main");

            // Assert
            result.Should().BeCase<string>().Which.Should().Be("Main.Localization");
        }

        [Test]
        public void BuildNamespace_SubDomainCodeFolder_AppendsSubDomain()
        {
            // Act
            var result = TextKeyGenerator.BuildNamespace($"{MainFolder}/Sub/Code", MainAsmdefPath, "Main");

            // Assert
            result.Should().BeCase<string>().Which.Should().Be("Main.Sub");
        }

        [Test]
        public void BuildNamespace_SubfolderOfSubDomainCode_AppendsSubDomainAndSubfolder()
        {
            // Act
            var result = TextKeyGenerator.BuildNamespace($"{MainFolder}/Sub/Code/Localization", MainAsmdefPath, "Main");

            // Assert
            result.Should().BeCase<string>().Which.Should().Be("Main.Sub.Localization");
        }

        [Test]
        public void BuildNamespace_AsmdefOutsideCodeFolder_AppendsFoldersBelowAsmdef()
        {
            // Act
            var result = TextKeyGenerator.BuildNamespace($"{MainFolder}/Tests/Localization", $"{MainFolder}/Tests/Main.Tests.asmdef", "Main.Tests");

            // Assert
            result.Should().BeCase<string>().Which.Should().Be("Main.Tests.Localization");
        }

        [Test]
        public void BuildNamespace_FolderOutsideModule_ReturnsError()
        {
            // Act
            var result = TextKeyGenerator.BuildNamespace("Assets/_Project/Domains/MainOther/Code", MainAsmdefPath, "Main");

            // Assert
            result.Should().BeCase<Error>();
        }

        [TestCase("Assets/_Project/Domains/MainMenu/Localization")]
        [TestCase("Packages/games.engine-room.foundation/Domains/Loading/Localization")]
        [TestCase("Assets/Game/Domains/Level")]
        public void IsPublic_TableInDomainsFolder_ReturnsFalse(string folder)
        {
            // Act
            var isPublic = TextKeyGenerator.IsPublic(folder);

            // Assert
            isPublic.Should().BeFalse();
        }

        [TestCase("Assets/_Project/Shared/UI/Localization")]
        [TestCase("Packages/games.engine-room.foundation/Shared/UI/Localization")]
        [TestCase("Assets/_Project/DomainsExtra/Localization")]
        public void IsPublic_TableOutsideDomainsFolder_ReturnsTrue(string folder)
        {
            // Act
            var isPublic = TextKeyGenerator.IsPublic(folder);

            // Assert
            isPublic.Should().BeTrue();
        }

        [Test]
        public void GetSweepFolders_RootUnderAssets_ReturnsAssetsOnly()
        {
            // Act
            var folders = TextKeyGenerator.GetSweepFolders("Assets/_Project", _ => true);

            // Assert
            folders.Should().Equal("Assets");
        }

        [Test]
        public void GetSweepFolders_WritablePackageRoot_AddsPackageRoot()
        {
            // Act
            var folders = TextKeyGenerator.GetSweepFolders("Packages/games.engine-room.foundation", _ => true);

            // Assert
            folders.Should().Equal("Assets", "Packages/games.engine-room.foundation");
        }

        [Test]
        public void GetSweepFolders_ReadOnlyPackageRoot_ReturnsAssetsOnly()
        {
            // Act
            var folders = TextKeyGenerator.GetSweepFolders("Packages/games.engine-room.foundation", _ => false);

            // Assert
            folders.Should().Equal("Assets");
        }

        private string BuildSource()
        {
            return TextKeyGenerator.BuildSource(_table, "Sample.asset", TableGuid, "Sample", "SampleText", false).Should().BeCase<string>().Which;
        }
    }
}
