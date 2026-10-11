using System.Collections.Generic;
using AwesomeAssertions;
using Core.Analytics;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Tests.Analytics
{
    public sealed class UiClickTrackingPresenterTests
    {
        private GameObject _canvas = null!;
        private UiClickRelay _relay = null!;
        private RecordingTracker _tracker = null!;
        private UiClickTrackingPresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas");
            _relay = _canvas.AddComponent<UiClickRelay>();
            _tracker = new RecordingTracker();
            _presenter = new UiClickTrackingPresenter(_relay, _tracker);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            Object.DestroyImmediate(_canvas);
        }

        [Test]
        public void Start_RelayClick_TracksClick()
        {
            // Arrange
            _presenter.Start();

            // Act
            _relay.Request("quit");

            // Assert
            _tracker.Tracked.Should().Equal(new UiClick("", "quit"));
        }

        private sealed class RecordingTracker : IUiClickTracker
        {
            public List<UiClick> Tracked { get; } = new();

            public void Track(UiClick click)
            {
                Tracked.Add(click);
            }
        }
    }
}
