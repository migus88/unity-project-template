using System.Linq;
using Core.Editor;
using Core.Logging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VContainer.Unity;

namespace Bootstrap.Editor.GameModules
{
    public static class RootScopeAssets
    {
        public const string BasePrefabPath = CorePackage.Root + "/Bootstrap/Prefabs/RootLifetimeScope.prefab";
        public const string DefaultSettingsPath = CorePackage.Root + "/Bootstrap/Settings/VContainerSettings.asset";

        private const string GameModuleField = "_gameModule";

        public static VContainerSettings? FindActiveSettings()
        {
            return PlayerSettings.GetPreloadedAssets().OfType<VContainerSettings>().FirstOrDefault();
        }

        public static RootLifetimeScope CreateVariant(GameModule module, string path)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            var previewScene = EditorSceneManager.NewPreviewScene();

            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, previewScene);
                SetGameModule(instance.GetComponent<RootLifetimeScope>(), module);
                var variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
                Log.Info(LogTags.GameModule, $"Created root scope variant '{path}'.");
                return variant.GetComponent<RootLifetimeScope>();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        public static VContainerSettings CreateSettings(RootLifetimeScope rootScope, string path)
        {
            var settings = ScriptableObject.CreateInstance<VContainerSettings>();
            settings.RootLifetimeScope = rootScope;
            AssetDatabase.CreateAsset(settings, path);
            Log.Info(LogTags.GameModule, $"Created '{path}'.");
            return settings;
        }

        public static void Use(VContainerSettings settings)
        {
            var preloadedAssets = PlayerSettings.GetPreloadedAssets()
                .Where(asset => asset != null && asset is not VContainerSettings)
                .Append(settings)
                .ToArray();

            PlayerSettings.SetPreloadedAssets(preloadedAssets);
            AssetDatabase.SaveAssets();
            Log.Info(LogTags.GameModule, $"Preloaded VContainerSettings set to '{AssetDatabase.GetAssetPath(settings)}'.");
        }

        public static void UseDefault()
        {
            Use(AssetDatabase.LoadAssetAtPath<VContainerSettings>(DefaultSettingsPath));
        }

        private static void SetGameModule(RootLifetimeScope scope, GameModule module)
        {
            using var serializedScope = new SerializedObject(scope);
            serializedScope.FindProperty(GameModuleField).objectReferenceValue = module;
            serializedScope.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
