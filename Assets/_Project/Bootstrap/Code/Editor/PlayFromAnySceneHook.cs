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
                    PrepareBoot(SceneManager.GetActiveScene().path);
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    ResetBoot();
                    SessionState.EraseBool(BootMode.TestKey);
                    break;
            }
        }

        private static void PrepareBoot(string activeScenePath)
        {
            ResetBoot();

            if (GetRootDescriptors().Any(descriptor => EditorScopeScene.IsScopeSceneOf(descriptor, activeScenePath)))
            {
                SessionState.SetString(BootMode.DebugScopeScenePathKey, activeScenePath);
                EditorSceneManager.playModeStartScene = LoadBootstrapScene();
                return;
            }

            if (FindAllDescriptors().Any(descriptor => EditorScopeScene.IsScopeSceneOf(descriptor, activeScenePath)))
            {
                Log.Warn(LogTags.Boot, $"'{activeScenePath}' belongs to a domain the root scope does not register, so it cannot be debug-run on its own. Booting normally.");
                EditorSceneManager.playModeStartScene = LoadBootstrapScene();
            }
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

            using var serializedScope = new SerializedObject(settings.RootLifetimeScope);
            var property = serializedScope.GetIterator();

            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is DomainDescriptor descriptor)
                {
                    descriptors.Add(descriptor);
                }
            }

            return descriptors;
        }

        private static IEnumerable<DomainDescriptor> FindAllDescriptors()
        {
            return AssetDatabase.FindAssets($"t:{nameof(DomainDescriptor)}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<DomainDescriptor>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(descriptor => descriptor != null);
        }
    }
}
