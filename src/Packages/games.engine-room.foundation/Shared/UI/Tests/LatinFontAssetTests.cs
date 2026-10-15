using System.Linq;
using AwesomeAssertions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Shared.UI.Tests
{
    public sealed class LatinFontAssetTests
    {
        private const string FontAssetPath = "Packages/games.engine-room.foundation/Shared/UI/Fonts/LiberationSans Latin SDF.asset";
        private const int AtlasSize = 512;

        [Test]
        public void LatinFont_Dependencies_IncludeNoOtherFontAssetOrFontFile()
        {
            // Act
            var dependencies = AssetDatabase.GetDependencies(FontAssetPath, true)
                .Where(path => path != FontAssetPath)
                .Where(IsFont)
                .ToArray();

            // Assert
            dependencies.Should().BeEmpty();
        }

        [Test]
        public void LatinFont_Atlas_IsOneStatic512Page()
        {
            // Act
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            // Assert
            fontAsset.atlasPopulationMode.Should().Be(AtlasPopulationMode.Static);
            fontAsset.atlasTextures.Should().ContainSingle();
            fontAsset.atlasTexture.width.Should().Be(AtlasSize);
            fontAsset.atlasTexture.height.Should().Be(AtlasSize);
        }

        private static bool IsFont(string path)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type == typeof(TMP_FontAsset) || type == typeof(Font);
        }
    }
}
