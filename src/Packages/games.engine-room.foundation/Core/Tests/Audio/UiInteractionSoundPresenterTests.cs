using System;
using Core.Audio;
using NSubstitute;
using NUnit.Framework;
using TestUtils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Tests.Audio
{
    public sealed class UiInteractionSoundPresenterTests
    {
        private GameObject _canvas = null!;
        private UiInteractionRelay _relay = null!;
        private IUiInteractionSounds _sounds = null!;
        private FakeClock _clock = null!;
        private UiInteractionSoundPresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas");
            _relay = _canvas.AddComponent<UiInteractionRelay>();
            _sounds = Substitute.For<IUiInteractionSounds>();
            _clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            _presenter = new UiInteractionSoundPresenter(_relay, _sounds, _clock);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            Object.DestroyImmediate(_canvas);
        }

        [Test]
        public void Request_AfterStart_ForwardsInteraction()
        {
            // Arrange
            _presenter.Start();

            // Act
            _relay.Request(UiInteraction.Click);

            // Assert
            _sounds.Received(1).Play(UiInteraction.Click);
        }

        [Test]
        public void Request_TwoTicksWithinInterval_PlaysOnce()
        {
            // Arrange
            _presenter.Start();

            // Act
            _relay.Request(UiInteraction.Tick);
            _clock.Advance(TimeSpan.FromMilliseconds(74));
            _relay.Request(UiInteraction.Tick);

            // Assert
            _sounds.Received(1).Play(UiInteraction.Tick);
        }

        [Test]
        public void Request_TicksIntervalApart_PlaysBoth()
        {
            // Arrange
            _presenter.Start();

            // Act
            _relay.Request(UiInteraction.Tick);
            _clock.Advance(UiInteractionSoundPresenter.MinTickInterval);
            _relay.Request(UiInteraction.Tick);

            // Assert
            _sounds.Received(2).Play(UiInteraction.Tick);
        }

        [TestCase(UiInteraction.Hover)]
        [TestCase(UiInteraction.Click)]
        [TestCase(UiInteraction.Back)]
        [TestCase(UiInteraction.Step)]
        public void Request_NonTickTwiceAtOnce_PlaysBoth(UiInteraction interaction)
        {
            // Arrange
            _presenter.Start();

            // Act
            _relay.Request(interaction);
            _relay.Request(interaction);

            // Assert
            _sounds.Received(2).Play(interaction);
        }

        [Test]
        public void Request_AfterDispose_PlaysNothing()
        {
            // Arrange
            _presenter.Start();
            _presenter.Dispose();

            // Act
            _relay.Request(UiInteraction.Click);

            // Assert
            _sounds.DidNotReceiveWithAnyArgs().Play(default);
        }
    }
}
