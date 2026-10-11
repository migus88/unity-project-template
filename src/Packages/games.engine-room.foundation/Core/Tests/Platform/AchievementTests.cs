using System;
using AwesomeAssertions;
using Core.Platform;
using NUnit.Framework;

namespace Core.Tests.Platform
{
    public sealed class AchievementTests
    {
        [TestCase("Win_Once")]
        [TestCase("1st_win")]
        [TestCase("a1234567890123456789012345678901234567890123456789012345678901234")]
        public void Ctor_MalformedId_Throws(string id)
        {
            // Act
            Action act = () => new Achievement(id);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void Ctor_NegativeProgressTarget_Throws()
        {
            // Act
            Action act = () => new Achievement("win_once", -1);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
