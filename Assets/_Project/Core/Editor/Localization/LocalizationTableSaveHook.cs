using System;
using System.Collections.Generic;
using Core.Localization;
using Core.Logging;
using UnityEditor;

namespace Core.Editor.Localization
{
    internal sealed class LocalizationTableSaveHook : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            var tables = new List<LocalizationTable>();

            foreach (var path in paths)
            {
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(LocalizationTable))
                {
                    tables.Add(AssetDatabase.LoadAssetAtPath<LocalizationTable>(path));
                }
            }

            if (tables.Count > 0)
            {
                EditorApplication.delayCall += () => Generate(tables);
            }

            return paths;
        }

        private static void Generate(List<LocalizationTable> tables)
        {
            foreach (var table in tables)
            {
                if (table == null)
                {
                    continue;
                }

                TextKeyGenerator.Generate(table).Switch(
                    _ => { },
                    error => Log.Error(LogTags.Localization, error.Message));
            }
        }
    }
}
