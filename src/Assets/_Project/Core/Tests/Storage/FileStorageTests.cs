using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Results;
using Core.Storage;
using NUnit.Framework;
using TestUtils;
using Success = OneOf.Types.Success;

namespace Core.Tests.Storage
{
    public sealed class FileStorageTests
    {
        private string _root = null!;
        private FileStorage _storage = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "CoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _storage = new FileStorage(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void Constructor_RelativeRoot_Throws()
        {
            // Arrange
            var relativeRoot = "Saves";

            // Act
            Action act = () => _ = new FileStorage(relativeRoot);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public async Task ReadAsync_MissingFile_ReturnsNotFound()
        {
            // Arrange
            var path = "missing.json";

            // Act
            var result = await _storage.ReadAsync(path, CancellationToken.None);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public async Task ReadAsync_MissingDirectory_ReturnsNotFound()
        {
            // Arrange
            var path = Path.Combine("Saves", "slot_0.json");

            // Act
            var result = await _storage.ReadAsync(path, CancellationToken.None);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public async Task ReadAsync_PathIsDirectory_ReturnsError()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_root, "folder"));

            // Act
            var result = await _storage.ReadAsync("folder", CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>();
        }

        [Test]
        public async Task ReadAsync_AbsolutePath_Throws()
        {
            // Arrange
            var absolutePath = Path.Combine(_root, "file.json");

            // Act
            Func<Task> act = async () => await _storage.ReadAsync(absolutePath, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Test]
        public async Task WriteAsync_NewFile_CanBeReadBack()
        {
            // Arrange
            var path = "settings.json";

            // Act
            var writeResult = await _storage.WriteAsync(path, "{\"a\":1}", CancellationToken.None);
            var readResult = await _storage.ReadAsync(path, CancellationToken.None);

            // Assert
            writeResult.Should().BeCase<Success>();
            readResult.Should().BeCase<string>().Which.Should().Be("{\"a\":1}");
        }

        [Test]
        public async Task WriteAsync_ExistingFile_ReplacesContent()
        {
            // Arrange
            var path = "settings.json";
            await _storage.WriteAsync(path, "old", CancellationToken.None);

            // Act
            var writeResult = await _storage.WriteAsync(path, "new", CancellationToken.None);

            // Assert
            writeResult.Should().BeCase<Success>();
            File.ReadAllText(Path.Combine(_root, path)).Should().Be("new");
        }

        [Test]
        public async Task WriteAsync_NestedPath_CreatesDirectories()
        {
            // Arrange
            var path = Path.Combine("Saves", "Deep", "slot_1.json");

            // Act
            var writeResult = await _storage.WriteAsync(path, "data", CancellationToken.None);

            // Assert
            writeResult.Should().BeCase<Success>();
            File.Exists(Path.Combine(_root, path)).Should().BeTrue();
        }

        [Test]
        public async Task WriteAsync_Succeeded_LeavesNoTempFile()
        {
            // Arrange
            var path = "slot_0.json";
            await _storage.WriteAsync(path, "first", CancellationToken.None);

            // Act
            await _storage.WriteAsync(path, "second", CancellationToken.None);

            // Assert
            Directory.GetFiles(_root).Should().ContainSingle().Which.Should().EndWith("slot_0.json");
        }

        [Test]
        public async Task WriteAsync_TargetIsDirectory_ReturnsError()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_root, "slot_0.json"));

            // Act
            var result = await _storage.WriteAsync("slot_0.json", "data", CancellationToken.None);

            // Assert
            result.Should().BeCase<Error>();
        }

        [Test]
        public async Task WriteAsync_TargetIsDirectory_LeavesNoTempFile()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_root, "slot_0.json"));

            // Act
            await _storage.WriteAsync("slot_0.json", "data", CancellationToken.None);

            // Assert
            Directory.GetFiles(_root).Should().BeEmpty();
        }

        [Test]
        public async Task WriteAsync_CancelledToken_ThrowsOperationCanceled()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await _storage.WriteAsync("slot_0.json", "data", cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task Exists_AfterWrite_ReturnsTrue()
        {
            // Arrange
            await _storage.WriteAsync("slot_0.json", "data", CancellationToken.None);

            // Act
            var exists = _storage.Exists("slot_0.json");

            // Assert
            exists.Should().BeTrue();
        }

        [Test]
        public void Exists_MissingFile_ReturnsFalse()
        {
            // Arrange
            var path = "missing.json";

            // Act
            var exists = _storage.Exists(path);

            // Assert
            exists.Should().BeFalse();
        }

        [Test]
        public async Task Delete_ExistingFile_RemovesIt()
        {
            // Arrange
            await _storage.WriteAsync("slot_0.json", "data", CancellationToken.None);

            // Act
            var result = _storage.Delete("slot_0.json");

            // Assert
            result.Should().BeCase<Success>();
            _storage.Exists("slot_0.json").Should().BeFalse();
        }

        [Test]
        public void Delete_MissingFile_Succeeds()
        {
            // Arrange
            var path = Path.Combine("Saves", "missing.json");

            // Act
            var result = _storage.Delete(path);

            // Assert
            result.Should().BeCase<Success>();
        }
    }
}
