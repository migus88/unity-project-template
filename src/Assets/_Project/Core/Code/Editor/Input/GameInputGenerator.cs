using System;
using System.IO;
using System.Linq;
using Core.Logging;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Editor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Core.Editor.Input
{
    internal sealed class GameInputGenerator : AssetPostprocessor
    {
        public const string AssetPath = CorePackage.Root + "/Core/Input/GameInput.inputactions";
        public const string OutputPath = CorePackage.Root + "/Core/Code/Input/GameInput.cs";
        public const string ClassName = "GameInput";
        public const string Namespace = "Core.Input";

        [MenuItem("Tools/Input/Generate GameInput")]
        public static void Generate()
        {
            if (!CorePackage.IsWritable(OutputPath))
            {
                Log.Warn(LogTags.Input, $"'{OutputPath}' is in a read-only package, so it is not regenerated.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);

            if (asset == null)
            {
                Log.Error(LogTags.Input, $"No input actions asset at '{AssetPath}'.");
                return;
            }

            if (InputActionCodeGenerator.GenerateWrapperCode(ToPhysicalPath(OutputPath), asset, CreateOptions()))
            {
                AssetDatabase.ImportAsset(OutputPath);
                Log.Info(LogTags.Input, $"Generated '{OutputPath}'.");
            }
        }

        internal static string BuildSource(InputActionAsset asset)
        {
            return InputActionCodeGenerator.GenerateWrapperCode(asset, CreateOptions());
        }

        internal static string ToPhysicalPath(string assetPath)
        {
            var package = PackageInfo.FindForAssetPath(assetPath);

            if (package == null)
            {
                return Path.GetFullPath(assetPath);
            }

            return Path.Combine(package.resolvedPath, assetPath.Substring(package.assetPath.Length).TrimStart('/'));
        }

        private static InputActionCodeGenerator.Options CreateOptions()
        {
            return new InputActionCodeGenerator.Options
            {
                sourceAssetPath = AssetPath,
                namespaceName = Namespace,
                className = ClassName,
            };
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Concat(movedAssets).Any(path => string.Equals(path, AssetPath, StringComparison.Ordinal)))
            {
                EditorApplication.delayCall += Generate;
            }
        }
    }
}
