using System.Collections.Generic;
using UnityEditor;

namespace Core.Editor.Environment
{
    internal static class BuildEnvironmentMaps
    {
        public static BuildEnvironmentMapLookup Find()
        {
            var paths = FindPaths();

            if (paths.Count == 0)
            {
                return new BuildEnvironmentMapLookup.Missing();
            }

            if (paths.Count > 1)
            {
                return new BuildEnvironmentMapLookup.Several(paths);
            }

            return new BuildEnvironmentMapLookup.Found(AssetDatabase.LoadAssetAtPath<BuildEnvironmentMap>(paths[0]), paths[0]);
        }

        public static IReadOnlyList<BuildEnvironmentMap> LoadAll()
        {
            var maps = new List<BuildEnvironmentMap>();

            foreach (var path in FindPaths())
            {
                var map = AssetDatabase.LoadAssetAtPath<BuildEnvironmentMap>(path);

                if (map != null)
                {
                    maps.Add(map);
                }
            }

            return maps;
        }

        private static List<string> FindPaths()
        {
            var paths = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(BuildEnvironmentMap)}"))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            return paths;
        }
    }
}
