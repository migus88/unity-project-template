using Core.Logging;
using UnityEditor;

namespace Core.Editor.Environment
{
    [InitializeOnLoad]
    internal static class BuildEnvironmentRestorer
    {
        static BuildEnvironmentRestorer()
        {
            EditorApplication.delayCall += RestoreOnLoad;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void RestoreAfterBuild()
        {
            EditorApplication.update -= RestoreWhenBuildEnds;
            EditorApplication.update += RestoreWhenBuildEnds;
        }

        public static void RestoreAll(bool isUnexpected)
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            foreach (var map in BuildEnvironmentMaps.LoadAll())
            {
                if (!BuildEnvironmentSlot.Restore(map))
                {
                    continue;
                }

                AssetDatabase.SaveAssetIfDirty(map.Active);
                var message = $"Build environment slot '{map.Active!.name}' restored to the Editor environment '{map.EditorEnvironment!.name}'.";

                if (isUnexpected)
                {
                    Log.Warn(LogTags.Environment, message);
                }
                else
                {
                    Log.Info(LogTags.Environment, message);
                }
            }
        }

        private static void RestoreOnLoad()
        {
            RestoreAll(isUnexpected: true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                RestoreAll(isUnexpected: true);
            }
        }

        private static void RestoreWhenBuildEnds()
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            EditorApplication.update -= RestoreWhenBuildEnds;
            RestoreAll(isUnexpected: true);
        }
    }
}
