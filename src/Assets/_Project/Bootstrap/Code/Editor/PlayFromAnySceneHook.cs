using System.Collections.Generic;
using System.Linq;
using Core.Domains;
using Core.Logging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Bootstrap.Editor
{
    [InitializeOnLoad]
    internal static class PlayFromAnySceneHook
    {
        private const string BootstrapScenePath = "Assets/_Project/Bootstrap/Scenes/Bootstrap.unity";

        static PlayFromAnySceneHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    PrepareBoot(SceneManager.GetActiveScene().path, GetLoadedScenePaths());
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    EditorApplication.LockReloadAssemblies();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    EditorApplication.UnlockReloadAssemblies();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    ResetBoot();
                    SessionState.EraseBool(BootMode.TestKey);
                    VContainerSettings.LoadInstanceFromPreloadAssets();
                    break;
            }
        }

        private static void PrepareBoot(string activeScenePath, List<string> loadedScenePaths)
        {
            ResetBoot();
            var rootDescriptors = GetRootDescriptors();
            var debugScenePath = IsScopeScene(rootDescriptors, activeScenePath)
                ? activeScenePath
                : loadedScenePaths.FirstOrDefault(path => IsScopeScene(rootDescriptors, path));

            if (debugScenePath != null)
            {
                SessionState.SetString(BootMode.DebugScopeScenePathKey, debugScenePath);
                EditorSceneManager.playModeStartScene = LoadBootstrapScene();
                return;
            }

            var allDescriptors = FindAllDescriptors().ToList();
            var otherScopeScenePath = loadedScenePaths.FirstOrDefault(path => IsScopeScene(allDescriptors, path));

            if (otherScopeScenePath != null)
            {
                Log.Warn(LogTags.Boot, $"'{otherScopeScenePath}' belongs to a domain the root scope does not register, so it cannot be debug-run on its own. Booting normally.");
                EditorSceneManager.playModeStartScene = LoadBootstrapScene();
            }
        }

        private static bool IsScopeScene(List<DomainDescriptor> descriptors, string scenePath)
        {
            return descriptors.Any(descriptor => EditorScopeScene.IsScopeSceneOf(descriptor, scenePath));
        }

        private static List<string> GetLoadedScenePaths()
        {
            var paths = new List<string>();

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                {
                    paths.Add(scene.path);
                }
            }

            return paths;
        }

        private static void ResetBoot()
        {
            SessionState.EraseString(BootMode.DebugScopeScenePathKey);
            EditorSceneManager.playModeStartScene = null;
        }

        private static SceneAsset LoadBootstrapScene()
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
        }

        private static List<DomainDescriptor> GetRootDescriptors()
        {
            var descriptors = new List<DomainDescriptor>();
            var settings = PlayerSettings.GetPreloadedAssets().OfType<VContainerSettings>().FirstOrDefault();

            if (settings == null || settings.RootLifetimeScope == null)
            {
                return descriptors;
            }

            CollectDescriptors(settings.RootLifetimeScope, descriptors);
            return descriptors;
        }

        private static void CollectDescriptors(UnityEngine.Object target, List<DomainDescriptor> descriptors)
        {
            using var serializedTarget = new SerializedObject(target);
            var property = serializedTarget.GetIterator();

            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                switch (property.objectReferenceValue)
                {
                    case DomainDescriptor descriptor:
                        descriptors.Add(descriptor);
                        break;
                    case GameModule module:
                        CollectDescriptors(module, descriptors);
                        break;
                }
            }
        }

        private static IEnumerable<DomainDescriptor> FindAllDescriptors()
        {
            return AssetDatabase.FindAssets($"t:{nameof(DomainDescriptor)}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<DomainDescriptor>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(descriptor => descriptor != null);
        }
    }
}
