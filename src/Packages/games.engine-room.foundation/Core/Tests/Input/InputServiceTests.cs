using AwesomeAssertions;
using Core.Input;
using NUnit.Framework;
using UnityEngine;

namespace Core.Tests.Input
{
    public sealed class InputServiceTests
    {
        private GameInput _actions = null!;
        private InputService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _actions = new GameInput();
            _service = new InputService(_actions);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            Object.DestroyImmediate(_actions.asset);
        }

        [Test]
        public void Actions_NothingPushed_ReturnsGivenInstanceWithAllMapsDisabled()
        {
            // Act
            var actions = _service.Actions;

            // Assert
            actions.Should().BeSameAs(_actions);
            _actions.Player.enabled.Should().BeFalse();
            _actions.UI.enabled.Should().BeFalse();
        }

        [Test]
        public void Actions_EditorOrDevelopmentBuild_DebugMapStaysEnabledOutsideTheStack()
        {
            // Act
            var handle = _service.Push(InputMaps.None);
            handle.Dispose();

            // Assert
            _actions.Debug.enabled.Should().BeTrue();
        }

        [Test]
        public void Push_Player_EnablesOnlyPlayerMap()
        {
            // Act
            _service.Push(InputMaps.Player);

            // Assert
            _actions.Player.enabled.Should().BeTrue();
            _actions.UI.enabled.Should().BeFalse();
        }

        [Test]
        public void Push_PlayerAndUi_EnablesBothMaps()
        {
            // Act
            _service.Push(InputMaps.Player | InputMaps.Ui);

            // Assert
            _actions.Player.enabled.Should().BeTrue();
            _actions.UI.enabled.Should().BeTrue();
        }

        [Test]
        public void Push_OnTopOfAnotherSet_EnablesExactlyTheNewSet()
        {
            // Arrange
            _service.Push(InputMaps.Player | InputMaps.Ui);

            // Act
            _service.Push(InputMaps.Ui);

            // Assert
            _actions.Player.enabled.Should().BeFalse();
            _actions.UI.enabled.Should().BeTrue();
        }

        [Test]
        public void Push_None_DisablesAllMaps()
        {
            // Arrange
            _service.Push(InputMaps.Player | InputMaps.Ui);

            // Act
            _service.Push(InputMaps.None);

            // Assert
            _actions.Player.enabled.Should().BeFalse();
            _actions.UI.enabled.Should().BeFalse();
        }

        [Test]
        public void DisposeHandle_TopOfStack_RestoresPreviousSet()
        {
            // Arrange
            _service.Push(InputMaps.Player | InputMaps.Ui);
            var pause = _service.Push(InputMaps.Ui);

            // Act
            pause.Dispose();

            // Assert
            _actions.Player.enabled.Should().BeTrue();
            _actions.UI.enabled.Should().BeTrue();
        }

        [Test]
        public void DisposeHandle_LastHandle_DisablesAllMaps()
        {
            // Arrange
            var handle = _service.Push(InputMaps.Player | InputMaps.Ui);

            // Act
            handle.Dispose();

            // Assert
            _actions.Player.enabled.Should().BeFalse();
            _actions.UI.enabled.Should().BeFalse();
        }

        [Test]
        public void DisposeHandle_BelowTop_KeepsTopSetAndRemovesItFromStack()
        {
            // Arrange
            _service.Push(InputMaps.Ui);
            var middle = _service.Push(InputMaps.Player | InputMaps.Ui);
            var top = _service.Push(InputMaps.Player);

            // Act
            middle.Dispose();
            var isPlayerEnabledWithTop = _actions.Player.enabled;
            var isUiEnabledWithTop = _actions.UI.enabled;
            top.Dispose();

            // Assert
            isPlayerEnabledWithTop.Should().BeTrue();
            isUiEnabledWithTop.Should().BeFalse();
            _actions.Player.enabled.Should().BeFalse();
            _actions.UI.enabled.Should().BeTrue();
        }

        [Test]
        public void DisposeHandle_Twice_SecondCallIsIgnored()
        {
            // Arrange
            _service.Push(InputMaps.Player);
            _service.Push(InputMaps.Ui);
            var top = _service.Push(InputMaps.Player | InputMaps.Ui);
            top.Dispose();

            // Act
            top.Dispose();

            // Assert
            _actions.Player.enabled.Should().BeFalse();
            _actions.UI.enabled.Should().BeTrue();
        }

        [Test]
        public void Dispose_WithPushedMaps_DisablesAllMaps()
        {
            // Arrange
            _service.Push(InputMaps.Player | InputMaps.Ui);

            // Act
            _service.Dispose();

            // Assert
            _actions.Player.enabled.Should().BeFalse();
            _actions.UI.enabled.Should().BeFalse();
        }
    }
}
