using Core.Logging;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Bootstrap.Editor
{
    internal sealed class BootSceneBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var path = BootstrapScene.FindPath();

            if (path == null)
            {
                Log.Warn(LogTags.Boot, $"Build Settings have no enabled scene. Add the game's boot scene (Tools/Foundation/Create Boot Scene) or '{BootstrapScene.PackagePath}'.");
                return;
            }

            var count = BootstrapScene.CountGameObjectsAt(path);

            if (count > 0)
            {
                Log.Warn(LogTags.Boot, $"Boot scene '{path}' holds {count} GameObjects. Keep it empty and author app-lifetime objects in the root prefab.");
            }
        }
    }
}
