using AwesomeAssertions;
using Core.Editor.Environment;
using Core.Environment;
using NUnit.Framework;
using UnityEditor;

namespace Core.Tests.Environment
{
    public sealed class EditorReportingOverrideTests
    {
        private bool _wasEnabled;

        [SetUp]
        public void SetUp()
        {
            _wasEnabled = EditorReportingOverride.IsEnabled;
        }

        [TearDown]
        public void TearDown()
        {
            if (_wasEnabled)
            {
                SessionState.SetBool(ReportingRule.EditorOverrideKey, true);
            }
            else
            {
                SessionState.EraseBool(ReportingRule.EditorOverrideKey);
            }
        }

        [Test]
        public void SetEnabled_True_ReadByRuntimeOverride()
        {
            // Arrange
            var runtimeOverride = new EditorSessionReportingOverride();

            // Act
            EditorReportingOverride.SetEnabled(true);

            // Assert
            runtimeOverride.IsEnabled.Should().BeTrue();
        }

        [Test]
        public void OnPlayModeStateChanged_EnteredEditMode_ClearsOverride()
        {
            // Arrange
            EditorReportingOverride.SetEnabled(true);

            // Act
            EditorReportingOverride.OnPlayModeStateChanged(PlayModeStateChange.EnteredEditMode);

            // Assert
            EditorReportingOverride.IsEnabled.Should().BeFalse();
        }

        [Test]
        public void OnPlayModeStateChanged_ExitingPlayMode_KeepsOverride()
        {
            // Arrange
            EditorReportingOverride.SetEnabled(true);

            // Act
            EditorReportingOverride.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);

            // Assert
            EditorReportingOverride.IsEnabled.Should().BeTrue();
        }
    }
}
