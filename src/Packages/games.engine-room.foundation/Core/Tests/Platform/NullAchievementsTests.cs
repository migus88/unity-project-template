using System;
using AwesomeAssertions;
using Core.Platform;
using NUnit.Framework;

namespace Core.Tests.Platform
{
    public sealed class NullAchievementsTests
    {
        [Test]
        public void ReportProgress_UnlockOnlyAchievement_Throws()
        {
            // Arrange
            var achievements = new NullAchievements();

            // Act
            Action act = () => achievements.ReportProgress(new Achievement("win_once"), 1);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
