using System;
using AwesomeAssertions;
using Core.Platform;
using NUnit.Framework;

namespace Core.Tests.Platform
{
    public sealed class AchievementCatalogTests
    {
        [Test]
        public void Ctor_DuplicateIds_Throws()
        {
            // Arrange
            var achievements = new[] { new Achievement("win_once"), new Achievement("win_once") };

            // Act
            Action act = () => new AchievementCatalog(achievements);

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*win_once*");
        }

        [Test]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            // Arrange
            var catalog = new AchievementCatalog(new[] { new Achievement("win_once") });

            // Act
            var isFound = catalog.TryGet("win_twice", out _);

            // Assert
            isFound.Should().BeFalse();
        }
    }
}
