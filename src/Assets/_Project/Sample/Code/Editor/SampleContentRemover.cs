using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Content;
using Core.Domains;
using Core.Logging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sample.Editor
{
    internal static class SampleContentRemover
    {
        private const string MenuPath = "Tools/Template/Remove Example Content";
        private const string DialogTitle = "Remove Example Content";
        private const string ProjectFolder = "Assets/_Project";
        private const string BootstrapScenePath = "Assets/_Project/Bootstrap/Scenes/Bootstrap.unity";
        private const string ContentOutputFolder = "Assets/StreamingAssets/" + ContentDirectoryRegistry.RootFolderName;

        private static readonly string[] ExamplePaths =
        {
            "Assets/_Project/Sample",
            "Assets/_Project/Domains/Gameplay",
            "Assets/_Project/Domains/MainMenu",
        };

        private static readonly string[] ReferenceHolderPaths =
        {
            "Assets/_Project/Bootstrap/Prefabs/RootLifetimeScope.prefab",
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

            ClearReferences(removedPaths);
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
            return AssetDatabase.FindAssets(string.Empty, new[] { ProjectFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Where(path => !AssetDatabase.IsValidFolder(path) && !IsUnder(path, removedPaths) && !ReferenceHolderPaths.Contains(path))
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
            message.AppendLine("It also clears their references from the root scope, so the game boots with no main flow, and removes their scenes from Build Settings. This tool deletes itself.");

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

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            return true;
        }

        private static void ClearReferences(List<string> removedPaths)
        {
            foreach (var holderPath in ReferenceHolderPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(holderPath);

                try
                {
                    var components = root.GetComponentsInChildren<Component>(true).Where(component => component != null);
                    var isChanged = false;

                    foreach (var component in components)
                    {
                        isChanged |= ClearReferences(component, removedPaths);
                    }

                    if (isChanged)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, holderPath);
                        Log.Info(LogTags.Template, $"Cleared example references in '{holderPath}'.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static bool ClearReferences(UnityEngine.Object target, List<string> removedPaths)
        {
            using var serializedTarget = new SerializedObject(target);
            var property = serializedTarget.GetIterator();

            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && IsUnder(AssetDatabase.GetAssetPath(property.objectReferenceValue), removedPaths))
                {
                    property.objectReferenceValue = null;
                }
            }

            return serializedTarget.ApplyModifiedPropertiesWithoutUndo();
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

            Log.Info(LogTags.Template, $"Removed the example content: {string.Join(", ", removedPaths)}.");
        }

        private static bool IsUnder(string path, List<string> folders)
        {
            return !string.IsNullOrEmpty(path) && folders.Any(folder => path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal));
        }
    }
}
