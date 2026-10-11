using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests
{
    public sealed class QuitHandlersTests
    {
        private static readonly TimeSpan ShortBudget = TimeSpan.FromMilliseconds(50);

        private QuitHandlers _handlers = null!;

        [SetUp]
        public void SetUp()
        {
            _handlers = new QuitHandlers();
        }

        [Test]
        public async Task RunAsync_TwoHandlers_RunsBoth()
        {
            // Arrange
            var runs = 0;
            _handlers.Add(_ => Count(ref runs));
            _handlers.Add(_ => Count(ref runs));

            // Act
            await _handlers.RunAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

            // Assert
            runs.Should().Be(2);
        }

        [Test]
        public async Task RunAsync_HandlerOutlastsBudget_CancelsItAndCompletes()
        {
            // Arrange
            var isCancelled = false;
            _handlers.Add(async ct =>
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(30), DelayType.Realtime, cancellationToken: ct);
                }
                finally
                {
                    isCancelled = ct.IsCancellationRequested;
                }
            });
            LogAssert.Expect(LogType.Warning, new Regex("ran out of time"));

            // Act
            await _handlers.RunAsync(ShortBudget, CancellationToken.None);

            // Assert
            isCancelled.Should().BeTrue();
        }

        [Test]
        public async Task RunAsync_HandlerThrows_LogsItAndRunsTheOthers()
        {
            // Arrange
            var runs = 0;
            _handlers.Add(_ => throw new InvalidOperationException("quit failed"));
            _handlers.Add(_ => Count(ref runs));
            LogAssert.Expect(LogType.Exception, new Regex("quit failed"));

            // Act
            await _handlers.RunAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

            // Assert
            runs.Should().Be(1);
        }

        [Test]
        public async Task RunAsync_HandlerRemoved_DoesNotRunIt()
        {
            // Arrange
            var runs = 0;
            var registration = _handlers.Add(_ => Count(ref runs));
            registration.Dispose();

            // Act
            await _handlers.RunAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

            // Assert
            runs.Should().Be(0);
        }

        private static UniTask Count(ref int runs)
        {
            runs++;
            return UniTask.CompletedTask;
        }
    }
}
