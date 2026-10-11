using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Core.Platform;
using NUnit.Framework;

namespace Core.Tests.Platform
{
    public sealed class AchievementIdMapTests
    {
        private static readonly Achievement WinOnce = new("win_once");
        private static readonly Achievement BeatBoss = new("beat_boss");
        private static readonly AchievementCatalog Catalog = new(new[] { WinOnce, BeatBoss });

        [Test]
        public void ToPlatformId_NoOverride_ReturnsGameId()
        {
            // Arrange
            var map = new AchievementIdMap(Catalog, Overrides());

            // Act
            var platformId = map.ToPlatformId(WinOnce);

            // Assert
            platformId.Should().Be("win_once");
        }

        [Test]
        public void ToPlatformId_Override_ReturnsPlatformId()
        {
            // Arrange
            var map = new AchievementIdMap(Catalog, Overrides(("win_once", "ACH_WIN_ONE_GAME")));

            // Act
            var platformId = map.ToPlatformId(WinOnce);

            // Assert
            platformId.Should().Be("ACH_WIN_ONE_GAME");
        }

        [Test]
        public void ToPlatformId_AchievementNotInCatalog_Throws()
        {
            // Arrange
            var map = new AchievementIdMap(Catalog, Overrides());

            // Act
            Action act = () => map.ToPlatformId(new Achievement("win_once"));

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void TryGetAchievement_OverriddenPlatformId_ReturnsAchievement()
        {
            // Arrange
            var map = new AchievementIdMap(Catalog, Overrides(("win_once", "ACH_WIN_ONE_GAME")));

            // Act
            var isFound = map.TryGetAchievement("ACH_WIN_ONE_GAME", out var achievement);

            // Assert
            isFound.Should().BeTrue();
            achievement.Should().BeSameAs(WinOnce);
        }

        [Test]
        public void TryGetAchievement_GameIdShadowedByOverride_ReturnsFalse()
        {
            // Arrange
            var map = new AchievementIdMap(Catalog, Overrides(("win_once", "ACH_WIN_ONE_GAME")));

            // Act
            var isFound = map.TryGetAchievement("win_once", out _);

            // Assert
            isFound.Should().BeFalse();
        }

        [Test]
        public void FindProblems_OverrideForUnknownId_Reported()
        {
            // Act
            var problems = AchievementIdMap.FindProblems(Catalog, Overrides(("win_twice", "ACH_WIN_TWICE")));

            // Assert
            problems.Should().ContainSingle().Which.Should().Contain("win_twice");
        }

        [Test]
        public void FindProblems_TwoAchievementsOnOnePlatformId_Reported()
        {
            // Act
            var problems = AchievementIdMap.FindProblems(Catalog, Overrides(("beat_boss", "win_once")));

            // Assert
            problems.Should().ContainSingle().Which.Should().Contain("beat_boss").And.Contain("win_once");
        }

        [Test]
        public void FindProblems_BlankPlatformId_Reported()
        {
            // Act
            var problems = AchievementIdMap.FindProblems(Catalog, Overrides(("win_once", " ")));

            // Assert
            problems.Should().ContainSingle().Which.Should().Contain("blank");
        }

        [Test]
        public void Ctor_WithProblems_Throws()
        {
            // Act
            Action act = () => new AchievementIdMap(Catalog, Overrides(("win_twice", "ACH_WIN_TWICE")));

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*win_twice*");
        }

        private static IReadOnlyDictionary<string, string> Overrides(params (string Id, string PlatformId)[] pairs)
        {
            var overrides = new Dictionary<string, string>();

            foreach (var (id, platformId) in pairs)
            {
                overrides.Add(id, platformId);
            }

            return overrides;
        }
    }
}
