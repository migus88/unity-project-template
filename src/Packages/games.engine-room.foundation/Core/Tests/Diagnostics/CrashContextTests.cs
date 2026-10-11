using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using Core.Diagnostics;
using Core.Results;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Diagnostics
{
    public sealed class CrashContextTests
    {
        private string _root = null!;
        private string _logPath = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "CoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _logPath = Path.Combine(_root, "Player.log");
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
        public void SetSessionFile_Relative_ExposesFullPathAndName()
        {
            // Arrange
            var context = new CrashContext(_root, _logPath);

            // Act
            context.SetSessionFile(Path.Combine("Recordings", "run.rec"));

            // Assert
            context.SessionFilePath.Should().Be(Path.Combine(_root, "Recordings", "run.rec"));
            context.SessionFileName.Should().Be("run.rec");
        }

        [Test]
        public void ClearSessionFile_AfterSet_ClearsPath()
        {
            // Arrange
            var context = new CrashContext(_root, _logPath);
            context.SetSessionFile("run.rec");

            // Act
            context.ClearSessionFile();

            // Assert
            context.SessionFilePath.Should().BeNull();
            context.SessionFileName.Should().BeNull();
        }

        [Test]
        public void SetSessionFile_Rooted_Throws()
        {
            // Arrange
            var context = new CrashContext(_root, _logPath);

            // Act
            Action act = () => context.SetSessionFile(Path.Combine(_root, "run.rec"));

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void ReadLogTail_MissingFile_NotFound()
        {
            // Arrange
            var context = new CrashContext(_root, _logPath);

            // Act
            var result = context.ReadLogTail(1024);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public void ReadLogTail_NoLogPath_NotFound()
        {
            // Arrange
            var context = new CrashContext(_root, string.Empty);

            // Act
            var result = context.ReadLogTail(1024);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public void ReadLogTail_SmallerThanMax_ReturnsWholeFile()
        {
            // Arrange
            File.WriteAllText(_logPath, "first\nsecond\n");
            var context = new CrashContext(_root, _logPath);

            // Act
            var result = context.ReadLogTail(1024);

            // Assert
            Encoding.UTF8.GetString(result.Should().BeCase<byte[]>().Which).Should().Be("first\nsecond\n");
        }

        [Test]
        public void ReadLogTail_LargerThanMax_StartsAfterFirstNewlineOfTail()
        {
            // Arrange
            File.WriteAllText(_logPath, "aaaa\nbbbb\ncccc\n");
            var context = new CrashContext(_root, _logPath);

            // Act
            var result = context.ReadLogTail(7);

            // Assert
            Encoding.UTF8.GetString(result.Should().BeCase<byte[]>().Which).Should().Be("cccc\n");
        }

        [Test]
        public void ReadLogTail_FileOpenForWriting_StillReads()
        {
            // Arrange
            using var writer = new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            var line = Encoding.UTF8.GetBytes("open line\n");
            writer.Write(line, 0, line.Length);
            writer.Flush();
            var context = new CrashContext(_root, _logPath);

            // Act
            var result = context.ReadLogTail(1024);

            // Assert
            Encoding.UTF8.GetString(result.Should().BeCase<byte[]>().Which).Should().Be("open line\n");
        }
    }
}
