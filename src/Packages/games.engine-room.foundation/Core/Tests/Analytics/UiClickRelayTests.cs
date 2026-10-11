using System.Collections.Generic;
using AwesomeAssertions;
using Core.Analytics;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Tests.Analytics
{
    public sealed class UiClickRelayTests
    {
        private readonly List<UiClick> _received = new();
        private GameObject _canvas = null!;
        private DisposableBag _subscriptions;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas");
            var relay = _canvas.AddComponent<UiClickRelay>();
            var serialized = new SerializedObject(relay);
            serialized.FindProperty("<Screen>k__BackingField").stringValue = "start";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            relay.Clicked.Subscribe(_received.Add).AddTo(ref _subscriptions);
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
        public void Emit_ChildOfRelay_PublishesClickWithRelayScreen()
        {
            // Arrange
            var button = CreateChild(_canvas, "Button");

            // Act
            UiClickRelay.Emit(button.transform, "new_run");

            // Assert
            _received.Should().Equal(new UiClick("start", "new_run"));
        }

        [Test]
        public void Emit_EmptyElement_PublishesNothing()
        {
            // Arrange
            var button = CreateChild(_canvas, "Button");

            // Act
            UiClickRelay.Emit(button.transform, "");

            // Assert
            _received.Should().BeEmpty();
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
