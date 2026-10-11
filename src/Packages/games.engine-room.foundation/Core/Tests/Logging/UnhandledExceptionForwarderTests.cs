using System;
using System.Text.RegularExpressions;
using Core.Logging;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.Logging
{
    public sealed class UnhandledExceptionForwarderTests
    {
        private UnhandledExceptionForwarder _forwarder = null!;

        [SetUp]
        public void SetUp()
        {
            _forwarder = new UnhandledExceptionForwarder();
        }

        [TearDown]
        public void TearDown()
        {
            _forwarder.Dispose();
        }

        [Test]
        public void Initialize_FaultedUniTaskForgotten_LogsExceptionOnce()
        {
            // Arrange
            _forwarder.Initialize();
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: boom"));

            // Act
            UniTask.FromException(new InvalidOperationException("boom")).Forget();

            // Assert
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OnUnobservedTaskException_Cancellation_LogsNothing()
        {
            // Act
            _forwarder.OnUnobservedTaskException(new OperationCanceledException());

            // Assert
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OnUnhandledException_ExceptionObject_LogsException()
        {
            // Arrange
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: crash"));

            // Act
            _forwarder.OnUnhandledException(this, new UnhandledExceptionEventArgs(new InvalidOperationException("crash"), false));

            // Assert
            LogAssert.NoUnexpectedReceived();
        }
    }
}
