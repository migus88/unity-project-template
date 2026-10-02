using System;
using System.IO;
using System.Linq;
using Core.Editor;
using UnityEditor;

namespace Bootstrap.Editor
{
    public static class BootstrapScene
    {
        public const string PackagePath = CorePackage.Root + "/Bootstrap/Scenes/Bootstrap.unity";

        private const string GameObjectHeader = "--- !u!1 &";
        private const string PrefabInstanceHeader = "--- !u!1001 &";

        public static string? FindPath()
        {
            return EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled && !string.IsNullOrEmpty(scene.path))?.path;
        }

        public static string FindBootPath()
        {
            return FindPath() ?? PackagePath;
        }

        public static EditorBuildSettingsScene[] PlaceFirst(EditorBuildSettingsScene[] scenes, string path)
        {
            var otherScenes = scenes.Where(scene => scene.path != path && scene.path != PackagePath);
            return otherScenes.Prepend(new EditorBuildSettingsScene(path, true)).ToArray();
        }

        public static int CountGameObjectsAt(string scenePath)
        {
            return CountGameObjects(File.ReadAllText(FileUtil.GetPhysicalPath(scenePath)));
        }

        public static int CountGameObjects(string sceneText)
        {
            return sceneText.Split('\n')
                .Count(line => line.StartsWith(GameObjectHeader, StringComparison.Ordinal) || line.StartsWith(PrefabInstanceHeader, StringComparison.Ordinal));
        }
    }
}
