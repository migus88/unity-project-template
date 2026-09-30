using System.Threading;
using AwesomeAssertions;
using Core.Transitions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Core.Tests.Transitions
{
    public sealed class NullLoadingScreenTests
    {
        [Test]
        public void ShowAsync_Always_CompletesImmediately()
        {
            // Arrange
            var screen = new NullLoadingScreen();

            // Act
            var show = screen.ShowAsync(CancellationToken.None);

            // Assert
            show.Status.Should().Be(UniTaskStatus.Succeeded);
        }

        [Test]
        public void HideAsync_WithoutShow_CompletesImmediately()
        {
            // Arrange
            var screen = new NullLoadingScreen();

            // Act
            var hide = screen.HideAsync(CancellationToken.None);

            // Assert
            hide.Status.Should().Be(UniTaskStatus.Succeeded);
        }
    }
}
