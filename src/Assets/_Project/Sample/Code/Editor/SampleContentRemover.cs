using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bootstrap.Editor;
using Bootstrap.Editor.GameModules;
using Core.Content;
using Core.Domains;
using Core.Editor;
using Core.Logging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Sample.Editor
{
    internal static class SampleContentRemover
    {
        private const string MenuPath = "Tools/Template/Remove Example Content";
        private const string DialogTitle = "Remove Example Content";
        private const string CreateGameModuleMenuPath = "Tools/Foundation/Create Game Module";
        private const string ContentOutputFolder = "Assets/StreamingAssets/" + ContentDirectoryRegistry.RootFolderName;

        private static readonly string[] ExamplePaths =
        {
            "Assets/_Project/Sample",
            "Assets/_Project/Domains/Gameplay",
            "Assets/_Project/Domains/MainMenu",
        };

        private static readonly string[] SearchFolders =
        {
            "Assets",
            CorePackage.Root,
        };

        [MenuItem(MenuPath)]
        private static void Remove()
        {
            var removedPaths = FindRemovedPaths();

            if (removedPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, "The example content is already gone.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(DialogTitle, BuildConfirmation(removedPaths, FindDependents(removedPaths)), "Delete", "Cancel"))
            {
                return;
            }

            if (!CloseRemovedScenes(removedPaths))
            {
                return;
            }

            RestoreDefaultSettings(removedPaths);
            RemoveBuildScenes(removedPaths);
            DeleteAssets(removedPaths);
            AssetDatabase.Refresh();
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool CanRemove()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static List<string> FindRemovedPaths()
        {
            var examplePaths = ExamplePaths.Where(AssetDatabase.IsValidFolder).ToList();
            var contentOutputs = FindContentOutputs(examplePaths);
            return examplePaths.Concat(contentOutputs).ToList();
        }

        private static IEnumerable<string> FindContentOutputs(List<string> examplePaths)
        {
            if (examplePaths.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            return AssetDatabase.FindAssets($"t:{nameof(DomainDescriptor)}", examplePaths.ToArray())
                .Select(guid => AssetDatabase.LoadAssetAtPath<DomainDescriptor>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(descriptor => descriptor != null && !string.IsNullOrEmpty(descriptor.ContentDirectoryName))
                .Select(descriptor => $"{ContentOutputFolder}/{descriptor.ContentDirectoryName}")
                .Where(AssetDatabase.IsValidFolder)
                .Distinct()
                .ToList();
        }

        private static List<string> FindDependents(List<string> removedPaths)
        {
            return AssetDatabase.FindAssets(string.Empty, SearchFolders.Distinct().ToArray())
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Where(path => !AssetDatabase.IsValidFolder(path) && !IsUnder(path, removedPaths))
                .Where(path => AssetDatabase.GetDependencies(path, false).Any(dependency => IsUnder(dependency, removedPaths)))
                .ToList();
        }

        private static string BuildConfirmation(List<string> removedPaths, List<string> dependents)
        {
            var message = new StringBuilder();
            message.AppendLine("This permanently deletes the example game and keeps the reusable template (Bootstrap, Core, Shared, Loading, Settings):");
            message.AppendLine();

            foreach (var path in removedPaths)
            {
                message.AppendLine($"- {path}");
            }

            message.AppendLine();
            message.AppendLine("It also switches the preloaded VContainerSettings back to the default root scope, so the game boots with no main flow, and removes their scenes from Build Settings. This tool deletes itself.");
            message.AppendLine();
            message.AppendLine($"To start your own game afterwards, use {CreateGameModuleMenuPath}.");

            if (dependents.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("These assets still reference the example content and will have missing references:");

                foreach (var path in dependents)
                {
                    message.AppendLine($"- {path}");
                }
            }

            return message.ToString();
        }

        private static bool CloseRemovedScenes(List<string> removedPaths)
        {
            var hasRemovedScene = false;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                hasRemovedScene |= IsUnder(SceneManager.GetSceneAt(i).path, removedPaths);
            }

            if (!hasRemovedScene)
            {
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            var bootstrapScenePath = BootstrapScene.FindPath();

            if (bootstrapScenePath == null || IsUnder(bootstrapScenePath, removedPaths))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return true;
            }

            EditorSceneManager.OpenScene(bootstrapScenePath, OpenSceneMode.Single);
            return true;
        }

        private static void RestoreDefaultSettings(List<string> removedPaths)
        {
            var settings = RootScopeAssets.FindActiveSettings();

            if (settings == null || IsUnder(AssetDatabase.GetAssetPath(settings), removedPaths))
            {
                RootScopeAssets.UseDefault();
            }
        }

        private static void RemoveBuildScenes(List<string> removedPaths)
        {
            var scenes = EditorBuildSettings.scenes;
            var keptScenes = scenes.Where(scene => !IsUnder(scene.path, removedPaths)).ToArray();

            if (keptScenes.Length != scenes.Length)
            {
                EditorBuildSettings.scenes = keptScenes;
            }
        }

        private static void DeleteAssets(List<string> removedPaths)
        {
            var failedPaths = new List<string>();

            if (!AssetDatabase.DeleteAssets(removedPaths.ToArray(), failedPaths))
            {
                Log.Error(LogTags.Template, $"Could not delete: {string.Join(", ", failedPaths)}.");
                return;
            }

            Log.Info(LogTags.Template, $"Removed the example content: {string.Join(", ", removedPaths)}. Use {CreateGameModuleMenuPath} to start your own game.");
        }

        private static bool IsUnder(string path, List<string> folders)
        {
            return !string.IsNullOrEmpty(path) && folders.Any(folder => path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal));
        }
    }
}
