using System;
using System.IO;
using Bootstrap.Editor.GameModules;
using Core.Logging;
using Core.Results;
using OneOf;
using UnityEditor;
using Success = OneOf.Types.Success;

namespace Bootstrap.Editor
{
    public static class GameBootScene
    {
        public const string FileName = "Boot.unity";

        private const string MenuPath = "Tools/Foundation/Create Boot Scene";

        public static string GetPath(string moduleFolder)
        {
            return $"{moduleFolder}/Scenes/{FileName}";
        }

        public static OneOf<Success, Error> CreateForActiveModule()
        {
            var settings = RootScopeAssets.FindActiveSettings();
            var settingsPath = settings == null ? string.Empty : AssetDatabase.GetAssetPath(settings);

            if (!settingsPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return new Error("The preloaded VContainerSettings is not a game module's. Create a game module first (Tools/Foundation/Create Game Module).");
            }

            var moduleFolder = Path.GetDirectoryName(settingsPath)!.Replace('\\', '/');
            return Ensure(moduleFolder);
        }

        public static OneOf<Success, Error> Ensure(string moduleFolder)
        {
            var path = GetPath(moduleFolder);

            if (AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets).Length == 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                AssetDatabase.Refresh();

                if (!AssetDatabase.CopyAsset(BootstrapScene.PackagePath, path))
                {
                    return new Error($"Could not copy '{BootstrapScene.PackagePath}' to '{path}'.");
                }

                Log.Info(LogTags.Boot, $"Created boot scene '{path}'.");
            }

            EditorBuildSettings.scenes = BootstrapScene.PlaceFirst(EditorBuildSettings.scenes, path);
            AssetDatabase.SaveAssets();
            Log.Info(LogTags.Boot, $"'{path}' is the first scene in Build Settings.");
            return new Success();
        }

        [MenuItem(MenuPath)]
        private static void CreateFromMenu()
        {
            CreateForActiveModule().Switch(
                _ => { },
                error => Log.Error(LogTags.Boot, error.Message));
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool CanCreateFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }
    }
}
