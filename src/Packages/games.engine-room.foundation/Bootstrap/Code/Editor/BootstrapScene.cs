using System.Linq;
using UnityEditor;

namespace Bootstrap.Editor
{
    public static class BootstrapScene
    {
        public static string? FindPath()
        {
            return EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled && !string.IsNullOrEmpty(scene.path))?.path;
        }
    }
}
