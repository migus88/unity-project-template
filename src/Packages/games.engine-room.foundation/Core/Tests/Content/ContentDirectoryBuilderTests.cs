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
        private static readonly ContentDirectoryBuilder.DescriptorSource Regular = new("Assets/Regular.asset", "Game.RegularDescriptor", false, "Regular", false, false, "Assets/RegularContent.asset");
        private static readonly ContentDirectoryBuilder.DescriptorSource DevelopmentOnly = new("Assets/Tool.asset", "Game.ToolDescriptor", false, "Tool", true, false, "Assets/ToolContent.asset");

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
                new ContentDirectoryBuilder.ContentSource("Regular", "Assets/RegularContent.asset", false),
                new ContentDirectoryBuilder.ContentSource("Tool", "Assets/ToolContent.asset", false));
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

        [Test]
        public void SelectSources_OwnDirectoryNamedShared_ReturnsError()
        {
            // Arrange
            var ownShared = Regular with { ContentDirectoryName = "shared", HasOwnContentDirectory = true };

            // Act
            var result = ContentDirectoryBuilder.SelectSources(new[] { ownShared }, isDevelopmentBuild: true);

            // Assert
            result.Should().BeCase<Error>();
        }

        [Test]
        public void SelectSources_DescriptorShadowedByRootDescriptorOfSameType_SkipsShadowedDescriptor()
        {
            // Arrange
            var shadowed = Regular with { DescriptorPath = "Assets/Package/Regular.asset", ContentAssetPath = "Assets/Package/RegularContent.asset" };
            var root = Regular with { IsRootDescriptor = true };
            var descriptors = new[] { shadowed, root };

            // Act
            var result = ContentDirectoryBuilder.SelectSources(descriptors, isDevelopmentBuild: false);

            // Assert
            result.Should().BeCase<List<ContentDirectoryBuilder.ContentSource>>()
                .Which.Should().Equal(new ContentDirectoryBuilder.ContentSource("Regular", "Assets/RegularContent.asset", false));
        }

        [Test]
        public void SelectSources_TypeWithoutRootDescriptor_KeepsEveryDescriptor()
        {
            // Arrange
            var second = Regular with { DescriptorPath = "Assets/Second.asset", ContentDirectoryName = "Second", ContentAssetPath = "Assets/SecondContent.asset" };
            var rootOfOtherType = DevelopmentOnly with { IsDevelopmentOnly = false, IsRootDescriptor = true };
            var descriptors = new[] { Regular, second, rootOfOtherType };

            // Act
            var result = ContentDirectoryBuilder.SelectSources(descriptors, isDevelopmentBuild: false);

            // Assert
            result.Should().BeCase<List<ContentDirectoryBuilder.ContentSource>>()
                .Which.Select(source => source.Name).Should().Equal("Regular", "Second", "Tool");
        }

        [Test]
        public void PlanDirectories_MixedSources_PutsSharedSourcesInOneDirectoryAndOwnSourcesApart()
        {
            // Arrange
            var first = new ContentDirectoryBuilder.ContentSource("First", "Assets/First.asset", false);
            var own = new ContentDirectoryBuilder.ContentSource("Own", "Assets/Own.asset", true);
            var second = new ContentDirectoryBuilder.ContentSource("Second", "Assets/Second.asset", false);

            // Act
            var plans = ContentDirectoryBuilder.PlanDirectories(new[] { first, own, second });

            // Assert
            plans.Select(plan => plan.Name).Should().Equal(ContentDirectoryBuilder.SharedDirectoryName, "Own");
            plans[0].Sources.Should().Equal(first, second);
            plans[1].Sources.Should().Equal(own);
        }

        [Test]
        public void PlanDirectories_OnlyOwnDirectorySources_BuildsNoSharedDirectory()
        {
            // Arrange
            var own = new ContentDirectoryBuilder.ContentSource("Own", "Assets/Own.asset", true);

            // Act
            var plans = ContentDirectoryBuilder.PlanDirectories(new[] { own });

            // Assert
            plans.Select(plan => plan.Name).Should().Equal("Own");
        }
    }
}
