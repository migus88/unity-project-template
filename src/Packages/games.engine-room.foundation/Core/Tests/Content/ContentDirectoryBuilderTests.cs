using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Core.Editor;
using Core.Results;
using NUnit.Framework;
using TestUtils;

namespace Core.Tests.Content
{
    public sealed class ContentDirectoryBuilderTests
    {
        private static readonly ContentDirectoryBuilder.DescriptorSource Regular = new("Assets/Regular.asset", "Regular", false, "Assets/RegularContent.asset");
        private static readonly ContentDirectoryBuilder.DescriptorSource DevelopmentOnly = new("Assets/Tool.asset", "Tool", true, "Assets/ToolContent.asset");

        [Test]
        public void SelectSources_ReleaseBuild_ExcludesDevelopmentOnlyDescriptors()
        {
            // Arrange
            var descriptors = new[] { Regular, DevelopmentOnly };

            // Act
            var result = ContentDirectoryBuilder.SelectSources(descriptors, isDevelopmentBuild: false);

            // Assert
            result.Should().BeCase<List<ContentDirectoryBuilder.ContentSource>>()
                .Which.Select(source => source.Name).Should().Equal("Regular");
        }

        [Test]
        public void SelectSources_DevelopmentBuild_IncludesDevelopmentOnlyDescriptors()
        {
            // Arrange
            var descriptors = new[] { Regular, DevelopmentOnly };

            // Act
            var result = ContentDirectoryBuilder.SelectSources(descriptors, isDevelopmentBuild: true);

            // Assert
            result.Should().BeCase<List<ContentDirectoryBuilder.ContentSource>>()
                .Which.Should().Equal(
                new ContentDirectoryBuilder.ContentSource("Regular", "Assets/RegularContent.asset"),
                new ContentDirectoryBuilder.ContentSource("Tool", "Assets/ToolContent.asset"));
        }

        [Test]
        public void SelectSources_ReleaseBuildWithDevelopmentOnlyDuplicateName_ReturnsError()
        {
            // Arrange
            var duplicate = DevelopmentOnly with { DescriptorPath = "Assets/Duplicate.asset", ContentDirectoryName = "regular" };
            var descriptors = new[] { Regular, duplicate };

            // Act
            var result = ContentDirectoryBuilder.SelectSources(descriptors, isDevelopmentBuild: false);

            // Assert
            result.Should().BeCase<Error>();
        }
    }
}
