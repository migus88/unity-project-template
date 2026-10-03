using System.Collections.Generic;
using AwesomeAssertions;
using Core.Audio;
using NUnit.Framework;
using R3;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Tests.Audio
{
    public sealed class UiInteractionRelayTests
    {
        private readonly List<UiInteraction> _received = new();
        private GameObject _canvas = null!;
        private UiInteractionRelay _relay = null!;
        private DisposableBag _subscriptions;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas");
            _relay = _canvas.AddComponent<UiInteractionRelay>();
            _relay.Requested.Subscribe(_received.Add).AddTo(ref _subscriptions);
        }

        [TearDown]
        public void TearDown()
        {
            _subscriptions.Dispose();
            _subscriptions = default;
            Object.DestroyImmediate(_canvas);
            _received.Clear();
        }

        [Test]
        public void Emit_FromDeepDescendant_ReachesAncestorRelay()
        {
            // Arrange
            var panel = CreateChild(_canvas, "Panel");
            var button = CreateChild(panel, "Button");

            // Act
            UiInteractionRelay.Emit(button.transform, UiInteraction.Click);

            // Assert
            _received.Should().Equal(UiInteraction.Click);
        }

        [Test]
        public void Emit_None_SendsNothing()
        {
            // Arrange
            var button = CreateChild(_canvas, "Button");

            // Act
            UiInteractionRelay.Emit(button.transform, UiInteraction.None);

            // Assert
            _received.Should().BeEmpty();
        }

        [Test]
        public void Emit_WithoutRelay_DoesNotThrow()
        {
            // Arrange
            var orphan = new GameObject("Orphan");

            try
            {
                // Act
                var emit = () => UiInteractionRelay.Emit(orphan.transform, UiInteraction.Hover);

                // Assert
                emit.Should().NotThrow();
                _received.Should().BeEmpty();
            }
            finally
            {
                Object.DestroyImmediate(orphan);
            }
        }

        [Test]
        public void Emit_UnderInactiveParent_ReachesRelay()
        {
            // Arrange
            var panel = CreateChild(_canvas, "Panel");
            var button = CreateChild(panel, "Button");
            _canvas.SetActive(false);

            // Act
            UiInteractionRelay.Emit(button.transform, UiInteraction.Step);

            // Assert
            _received.Should().Equal(UiInteraction.Step);
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
