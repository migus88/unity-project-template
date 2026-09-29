using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Core.Logging;
using UnityEditor;

namespace Core.Editor.Localization
{
    internal sealed class LocalizationTablePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var movedTablePaths = movedAssets.Where(IsTablePath).ToList();
            var isAssetDeleted = deletedAssets.Any(IsAssetPath);

            if (movedTablePaths.Count > 0 || isAssetDeleted)
            {
                EditorApplication.delayCall += () => Refresh(movedTablePaths, isAssetDeleted);
            }
        }

        private static void Refresh(List<string> movedTablePaths, bool isAssetDeleted)
        {
            foreach (var path in movedTablePaths)
            {
                var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(path);

                if (table == null)
                {
                    continue;
                }

                TextKeyGenerator.Generate(table).Switch(
                    _ => { },
                    error => Log.Error(LogTags.Localization, error.Message));
            }

            if (isAssetDeleted)
            {
                TextKeyGenerator.SweepStaleOutputs();
            }
        }

        private static bool IsTablePath(string path)
        {
            return IsAssetPath(path) && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(LocalizationTable);
        }

        private static bool IsAssetPath(string path)
        {
            return path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
        }
    }
}
