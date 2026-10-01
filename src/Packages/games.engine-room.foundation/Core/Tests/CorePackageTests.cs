using AwesomeAssertions;
using Core.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Core.Tests
{
    public sealed class CorePackageTests
    {
        [TestCase("Core/Code/Core.asmdef")]
        [TestCase("Core/Input/GameInput.inputactions")]
        [TestCase("Core/Audio/GameAudioMixer.mixer")]
        [TestCase("Bootstrap/Scenes/Bootstrap.unity")]
        [TestCase("Bootstrap/Prefabs/RootLifetimeScope.prefab")]
        public void Root_KnownAsset_Exists(string relativePath)
        {
            // Act
            var guid = AssetDatabase.AssetPathToGUID($"{CorePackage.Root}/{relativePath}", AssetPathToGUIDOptions.OnlyExistingAssets);

            // Assert
            guid.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void IsWritable_AssetsPath_ReturnsTrue()
        {
            // Act
            var isWritable = CorePackage.IsWritable("Assets/Game/Table.asset");

            // Assert
            isWritable.Should().BeTrue();
        }

        [Test]
        public void IsWritable_RegistryPackagePath_ReturnsFalse()
        {
            // Act
            var isWritable = CorePackage.IsWritable("Packages/com.unity.inputsystem/package.json");

            // Assert
            isWritable.Should().BeFalse();
        }

        [Test]
        public void IsWritable_RootPath_IsTrueOnlyForEmbeddedOrLocalPackage()
        {
            // Arrange
            var source = PackageInfo.FindForAssetPath(CorePackage.Root).source;
            var isEditable = source == PackageSource.Embedded || source == PackageSource.Local;

            // Act
            var isWritable = CorePackage.IsWritable(CorePackage.Root + "/Core/Code/Core.asmdef");

            // Assert
            isWritable.Should().Be(isEditable);
        }
    }
}
