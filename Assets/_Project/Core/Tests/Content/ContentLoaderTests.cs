using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Content;
using Core.Results;
using NUnit.Framework;
using TestUtils;
using Unity.Loading;
using UnityEngine;

namespace Core.Tests.Content
{
    public sealed class ContentLoaderTests
    {
        private ContentLoader _loader = null!;

        [SetUp]
        public void SetUp()
        {
            _loader = new ContentLoader();
        }

        [Test]
        public async Task LoadAsync_InvalidLoadable_ReturnsNotFound()
        {
            // Arrange
            var loadable = new Loadable<GameObject>(default);

            // Act
            var result = await _loader.LoadAsync(loadable, CancellationToken.None);

            // Assert
            result.Should().BeCase<NotFound>();
        }

        [Test]
        public async Task LoadAsync_CancelledToken_Throws()
        {
            // Arrange
            var loadable = new Loadable<GameObject>(default);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await _loader.LoadAsync(loadable, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
    }
}
