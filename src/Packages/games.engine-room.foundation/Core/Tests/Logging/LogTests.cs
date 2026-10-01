using AwesomeAssertions;
using Core.Logging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.Logging
{
    public sealed class LogTests
    {
        private static readonly LogTag Tag = new("Test");

        [Test]
        public void Info_InEditor_LogsTaggedMessage()
        {
            // Arrange
            LogAssert.Expect(LogType.Log, "[Test] hello");

            // Act
            Log.Info(Tag, "hello");

            // Assert
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Warn_Always_LogsTaggedWarning()
        {
            // Arrange
            LogAssert.Expect(LogType.Warning, "[Test] careful");

            // Act
            Log.Warn(Tag, "careful");

            // Assert
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Error_Always_LogsTaggedError()
        {
            // Arrange
            LogAssert.Expect(LogType.Error, "[Test] broken");

            // Act
            Log.Error(Tag, "broken");

            // Assert
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Verbose_WithoutDefine_LogsNothing()
        {
            // Arrange
            var message = "chatty";

            // Act
            Log.Verbose(Tag, message);

            // Assert
            LogAssert.NoUnexpectedReceived();
        }
    }
}
