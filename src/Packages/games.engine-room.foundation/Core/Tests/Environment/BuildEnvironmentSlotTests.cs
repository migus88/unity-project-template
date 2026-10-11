using AwesomeAssertions;
using Core.Editor.Environment;
using NUnit.Framework;
using UnityEditor;

namespace Core.Tests.Environment
{
    public sealed class BuildEnvironmentSlotTests
    {
        private TestBuildEnvironments _environments = null!;

        [SetUp]
        public void SetUp()
        {
            _environments = new TestBuildEnvironments();
        }

        [TearDown]
        public void TearDown()
        {
            _environments.Dispose();
        }

        [Test]
        public void Restore_SlotHoldsAnotherEnvironment_PutsEditorEnvironmentBack()
        {
            // Arrange
            var editorEnvironment = _environments.Create("development");
            var map = _environments.CreateMap(_environments.CreateSlot(_environments.Create("production")), editorEnvironment);

            // Act
            var isRestored = BuildEnvironmentSlot.Restore(map);

            // Assert
            isRestored.Should().BeTrue();
            map.Active!.Environment.Should().BeSameAs(editorEnvironment);
        }

        [Test]
        public void Restore_SlotHoldsEditorEnvironment_LeavesSlotUntouched()
        {
            // Arrange
            var editorEnvironment = _environments.Create("development");
            var slot = _environments.CreateSlot(editorEnvironment);
            var map = _environments.CreateMap(slot, editorEnvironment);
            EditorUtility.ClearDirty(slot);

            // Act
            var isRestored = BuildEnvironmentSlot.Restore(map);

            // Assert
            isRestored.Should().BeFalse();
            EditorUtility.IsDirty(slot).Should().BeFalse();
        }
    }
}
