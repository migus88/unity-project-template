using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Core.Content;
using Core.Domains;
using Core.Results;
using Core.Tests.Domains;
using NSubstitute;
using NUnit.Framework;
using TestUtils;
using Unity.Loading;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Core.Tests.Content
{
    public sealed class ContentDirectoryRegistryTests
    {
        private string _root = null!;
        private IContentLoadManager _loadManager = null!;
        private ContentDirectoryRegistry _registry = null!;
        private FirstTestDomainDescriptor _descriptor = null!;
        private TestDomainContent _editorContent = null!;
        private TestDomainContent _builtContent = null!;
        private TestDomainContent _otherContent = null!;
        private readonly List<ContentCatalog> _catalogs = new();

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "CoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _loadManager = Substitute.For<IContentLoadManager>();
            _registry = new ContentDirectoryRegistry(_loadManager);
            _descriptor = ScriptableObject.CreateInstance<FirstTestDomainDescriptor>();
            _editorContent = ScriptableObject.CreateInstance<TestDomainContent>();
            _builtContent = ScriptableObject.CreateInstance<TestDomainContent>();
            _otherContent = ScriptableObject.CreateInstance<TestDomainContent>();

            _loadManager.RegisterContentDirectory(Arg.Any<string>()).Returns(CreateHandle(1));
            _loadManager.GetRootAssets<ContentCatalog>(Arg.Any<ContentDirectoryHandle>()).Returns(new[] { CreateCatalog() });
            ConfigureDescriptor("Gameplay", _editorContent);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
            Object.DestroyImmediate(_descriptor);
            Object.DestroyImmediate(_editorContent);
            Object.DestroyImmediate(_builtContent);
            Object.DestroyImmediate(_otherContent);

            foreach (var catalog in _catalogs)
            {
                Object.DestroyImmediate(catalog);
            }

            _catalogs.Clear();
        }

        [Test]
        public void RegisterAll_RootWithSubfolders_RegistersEachSubfolderByFullPath()
        {
            // Arrange
            var gameplayPath = CreateDirectory("Gameplay");
            var mainMenuPath = CreateDirectory("MainMenu");

            // Act
            _registry.RegisterAll(_root);

            // Assert
            _loadManager.Received(1).RegisterContentDirectory(gameplayPath);
            _loadManager.Received(1).RegisterContentDirectory(mainMenuPath);
            _loadManager.ReceivedWithAnyArgs(2).RegisterContentDirectory(default!);
        }

        [Test]
        public void RegisterAll_RootWithFiles_IgnoresFiles()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_root, "BuildManifestHash.txt"), "hash");

            // Act
            _registry.RegisterAll(_root);

            // Assert
            _loadManager.DidNotReceiveWithAnyArgs().RegisterContentDirectory(default!);
        }

        [Test]
        public void RegisterAll_MissingRoot_RegistersNothingAndWarns()
        {
            // Arrange
            var missingRoot = Path.Combine(_root, "Missing");
            LogAssert.Expect(LogType.Warning, new Regex("No content directories found"));

            // Act
            _registry.RegisterAll(missingRoot);

            // Assert
            _loadManager.DidNotReceiveWithAnyArgs().RegisterContentDirectory(default!);
        }

        [Test]
        public void RegisterAll_RelativeRoot_Throws()
        {
            // Arrange
            var relativeRoot = "Content";

            // Act
            Action act = () => _registry.RegisterAll(relativeRoot);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void RegisterAll_CalledTwice_Throws()
        {
            // Arrange
            CreateDirectory("Gameplay");
            _registry.RegisterAll(_root);

            // Act
            Action act = () => _registry.RegisterAll(_root);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void RegisterAll_InvalidHandle_SkipsDirectoryAndLogsError()
        {
            // Arrange
            CreateDirectory("Gameplay");
            _loadManager.RegisterContentDirectory(Arg.Any<string>()).Returns(default(ContentDirectoryHandle));
            LogAssert.Expect(LogType.Error, new Regex("Failed to register content directory 'Gameplay'"));

            // Act
            _registry.RegisterAll(_root);

            // Assert
            _registry.Get("Gameplay").Should().BeCase<NotFound>();
        }

        [Test]
        public void Get_RegisteredName_ReturnsHandleOfThatDirectory()
        {
            // Arrange
            var gameplayHandle = CreateHandle(10);
            var mainMenuHandle = CreateHandle(20);
            _loadManager.RegisterContentDirectory(CreateDirectory("Gameplay")).Returns(gameplayHandle);
            _loadManager.RegisterContentDirectory(CreateDirectory("MainMenu")).Returns(mainMenuHandle);
            _registry.RegisterAll(_root);

            // Act
            var result = _registry.Get("MainMenu");

            // Assert
            result.Should().BeCase<ContentDirectoryHandle>().Which.Should().Be(mainMenuHandle);
        }

        [Test]
        public void Get_UnknownName_ReturnsNotFound()
        {
            // Arrange
            CreateDirectory("Gameplay");
            _registry.RegisterAll(_root);

            // Act
            var result = _registry.Get("MainMenu");

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public void GetContent_NameInSharedCatalog_ReturnsEntryWithThatName()
        {
            // Arrange
            CreateDirectory("Shared");
            _loadManager.GetRootAssets<ContentCatalog>(Arg.Any<ContentDirectoryHandle>())
                .Returns(new[] { CreateCatalog(("MainMenu", _otherContent), ("Gameplay", _builtContent)) });
            _registry.RegisterAll(_root);

            // Act
            var result = _registry.GetContent(_descriptor);

            // Assert
            result.Should().BeCase<DomainContent>().Which.Should().BeSameAs(_builtContent);
        }

        [Test]
        public void GetContent_NameInAnotherDirectoryCatalog_ReturnsThatEntry()
        {
            // Arrange
            var gameplayHandle = CreateHandle(10);
            var sharedHandle = CreateHandle(20);
            _loadManager.RegisterContentDirectory(CreateDirectory("Gameplay")).Returns(gameplayHandle);
            _loadManager.RegisterContentDirectory(CreateDirectory("Shared")).Returns(sharedHandle);
            _loadManager.GetRootAssets<ContentCatalog>(gameplayHandle).Returns(new[] { CreateCatalog(("Gameplay", _builtContent)) });
            _loadManager.GetRootAssets<ContentCatalog>(sharedHandle).Returns(new[] { CreateCatalog(("MainMenu", _otherContent)) });
            _registry.RegisterAll(_root);

            // Act
            var result = _registry.GetContent(_descriptor);

            // Assert
            result.Should().BeCase<DomainContent>().Which.Should().BeSameAs(_builtContent);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void RegisterAll_DirectoryWithoutSingleCatalog_LogsErrorAndSkipsIt(int catalogCount)
        {
            // Arrange
            CreateDirectory("Shared");
            var catalogs = new ContentCatalog[catalogCount];

            for (var i = 0; i < catalogCount; i++)
            {
                catalogs[i] = CreateCatalog(("Gameplay", _builtContent));
            }

            _loadManager.GetRootAssets<ContentCatalog>(Arg.Any<ContentDirectoryHandle>()).Returns(catalogs);
            ConfigureDescriptor("Gameplay", null);
            LogAssert.Expect(LogType.Error, new Regex($"Content directory 'Shared' has {catalogCount} content catalogs"));

            // Act
            _registry.RegisterAll(_root);

            // Assert
            _registry.GetContent(_descriptor).Should().BeCase<NotFound>();
        }

        [Test]
        public void RegisterAll_NameInTwoDirectories_Throws()
        {
            // Arrange
            CreateDirectory("Gameplay");
            CreateDirectory("Shared");
            _loadManager.GetRootAssets<ContentCatalog>(Arg.Any<ContentDirectoryHandle>()).Returns(new[] { CreateCatalog(("Gameplay", _builtContent)) });

            // Act
            Action act = () => _registry.RegisterAll(_root);

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*'Gameplay' and 'Shared'*");
        }

        [Test]
        public void GetContent_NameNotInAnyCatalog_ReturnsEditorContent()
        {
            // Arrange
            CreateDirectory("Shared");
            _loadManager.GetRootAssets<ContentCatalog>(Arg.Any<ContentDirectoryHandle>()).Returns(new[] { CreateCatalog(("MainMenu", _otherContent)) });
            _registry.RegisterAll(_root);

            // Act
            var result = _registry.GetContent(_descriptor);

            // Assert
            result.Should().BeCase<DomainContent>().Which.Should().BeSameAs(_editorContent);
        }

        [Test]
        public void GetContent_NameNotInAnyCatalogAndNoEditorContent_ReturnsNotFound()
        {
            // Arrange
            ConfigureDescriptor("Gameplay", null);

            // Act
            var result = _registry.GetContent(_descriptor);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        private string CreateDirectory(string name)
        {
            return Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        }

        private ContentCatalog CreateCatalog(params (string Name, DomainContent Content)[] entries)
        {
            var catalog = ContentCatalog.Create(entries.Select(entry => new ContentCatalogEntry(entry.Name, entry.Content)).ToList());
            _catalogs.Add(catalog);
            return catalog;
        }

        private void ConfigureDescriptor(string contentDirectoryName, DomainContent? editorContent)
        {
            var serializedDescriptor = new SerializedObject(_descriptor);
            serializedDescriptor.FindProperty("<ContentDirectoryName>k__BackingField").stringValue = contentDirectoryName;
            serializedDescriptor.FindProperty("<EditorContent>k__BackingField").objectReferenceValue = editorContent;
            serializedDescriptor.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ContentDirectoryHandle CreateHandle(ulong value)
        {
            object handle = default(ContentDirectoryHandle);
            typeof(ContentDirectoryHandle).GetField("m_Handle", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(handle, value);
            return (ContentDirectoryHandle)handle;
        }
    }
}
