using System;
using System.IO;
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

            _loadManager.RegisterContentDirectory(Arg.Any<string>()).Returns(CreateHandle(1));
            _loadManager.GetRootAssets<DomainContent>(Arg.Any<ContentDirectoryHandle>()).Returns(new DomainContent[] { _builtContent });
            ConfigureDescriptor("Gameplay", _editorContent);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
            Object.DestroyImmediate(_descriptor);
            Object.DestroyImmediate(_editorContent);
            Object.DestroyImmediate(_builtContent);
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
        public void GetContent_RegisteredDirectoryWithOneRoot_ReturnsRootAssetOfThatDirectory()
        {
            // Arrange
            var gameplayHandle = CreateHandle(10);
            _loadManager.RegisterContentDirectory(CreateDirectory("Gameplay")).Returns(gameplayHandle);
            _loadManager.GetRootAssets<DomainContent>(gameplayHandle).Returns(new DomainContent[] { _builtContent });
            _registry.RegisterAll(_root);

            // Act
            var result = _registry.GetContent(_descriptor);

            // Assert
            result.Should().BeCase<DomainContent>().Which.Should().BeSameAs(_builtContent);
        }

        [Test]
        public void GetContent_RegisteredDirectoryWithoutRoot_ReturnsNotFound()
        {
            // Arrange
            CreateDirectory("Gameplay");
            _loadManager.GetRootAssets<DomainContent>(Arg.Any<ContentDirectoryHandle>()).Returns(Array.Empty<DomainContent>());
            _registry.RegisterAll(_root);

            // Act
            var result = _registry.GetContent(_descriptor);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public void GetContent_RegisteredDirectoryWithSeveralRoots_Throws()
        {
            // Arrange
            CreateDirectory("Gameplay");
            _loadManager.GetRootAssets<DomainContent>(Arg.Any<ContentDirectoryHandle>()).Returns(new DomainContent[] { _builtContent, _editorContent });
            _registry.RegisterAll(_root);

            // Act
            Action act = () => _registry.GetContent(_descriptor);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void GetContent_DirectoryNotRegistered_ReturnsEditorContent()
        {
            // Arrange
            CreateDirectory("MainMenu");
            _registry.RegisterAll(_root);

            // Act
            var result = _registry.GetContent(_descriptor);

            // Assert
            result.Should().BeCase<DomainContent>().Which.Should().BeSameAs(_editorContent);
            _loadManager.DidNotReceiveWithAnyArgs().GetRootAssets<DomainContent>(default);
        }

        [Test]
        public void GetContent_DirectoryNotRegisteredAndNoEditorContent_ReturnsNotFound()
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
