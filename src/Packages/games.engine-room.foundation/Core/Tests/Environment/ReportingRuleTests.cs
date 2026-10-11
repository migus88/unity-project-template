using AwesomeAssertions;
using Core.Environment;
using NSubstitute;
using NUnit.Framework;

namespace Core.Tests.Environment
{
    public sealed class ReportingRuleTests
    {
        [Test]
        public void IsReportingAllowed_Player_True()
        {
            // Arrange
            var rule = new ReportingRule(false, CreateOverride(false));

            // Act
            var isAllowed = rule.IsReportingAllowed;

            // Assert
            isAllowed.Should().BeTrue();
        }

        [Test]
        public void IsReportingAllowed_EditorWithoutOverride_False()
        {
            // Arrange
            var rule = new ReportingRule(true, CreateOverride(false));

            // Act
            var isAllowed = rule.IsReportingAllowed;

            // Assert
            isAllowed.Should().BeFalse();
        }

        [Test]
        public void IsReportingAllowed_EditorWithOverride_True()
        {
            // Arrange
            var rule = new ReportingRule(true, CreateOverride(true));

            // Act
            var isAllowed = rule.IsReportingAllowed;

            // Assert
            isAllowed.Should().BeTrue();
        }

        private static IReportingOverride CreateOverride(bool isEnabled)
        {
            var editorOverride = Substitute.For<IReportingOverride>();
            editorOverride.IsEnabled.Returns(isEnabled);
            return editorOverride;
        }
    }
}
