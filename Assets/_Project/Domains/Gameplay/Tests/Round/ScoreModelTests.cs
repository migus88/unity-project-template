using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Gameplay.Round;
using NUnit.Framework;
using R3;

namespace Gameplay.Tests.Round
{
    public sealed class ScoreModelTests
    {
        private ScoreModel _model = null!;

        [SetUp]
        public void SetUp()
        {
            _model = new ScoreModel();
        }

        [TearDown]
        public void TearDown()
        {
            _model.Dispose();
        }

        [Test]
        public void Score_New_IsZero()
        {
            // Act
            var score = _model.Score.CurrentValue;

            // Assert
            score.Should().Be(0);
        }

        [Test]
        public void Add_SeveralTimes_AccumulatesPoints()
        {
            // Act
            _model.Add(10);
            _model.Add(5);

            // Assert
            _model.Score.CurrentValue.Should().Be(15);
        }

        [Test]
        public void Add_WhileObserved_EmitsEveryNewScore()
        {
            // Arrange
            var emitted = new List<int>();
            using var subscription = _model.Score.Subscribe(emitted.Add);

            // Act
            _model.Add(10);
            _model.Add(10);

            // Assert
            emitted.Should().Equal(0, 10, 20);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void Add_NonPositivePoints_Throws(int points)
        {
            // Act
            var act = () => _model.Add(points);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
            _model.Score.CurrentValue.Should().Be(0);
        }
    }
}
