using System;
using UnityEditor;

namespace Core.Editor
{
    internal sealed class TestOnlyPluginImporterEnforcer : AssetPostprocessor
    {
        private const string InstalledPackagesPath = "Packages/nuget-packages/InstalledPackages/";

        private static readonly string[] TestOnlyPackageIds =
        {
            "AwesomeAssertions",
            "Castle.Core",
            "NSubstitute",
            "System.Diagnostics.EventLog",
            "System.Security.Principal.Windows",
        };

        private static readonly BuildTarget[] PlayerTargets =
        {
            BuildTarget.StandaloneWindows,
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneOSX,
            BuildTarget.StandaloneLinux64,
        };

        [InitializeOnLoadMethod]
        private static void EnforceAfterLoad()
        {
            EditorApplication.delayCall += EnforceAll;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (Array.Exists(importedAssets, IsTestOnlyPlugin))
            {
                EnforceAll();
            }
        }

        private static void EnforceAll()
        {
            foreach (var importer in PluginImporter.GetAllImporters())
            {
                if (IsTestOnlyPlugin(importer.assetPath) && !IsEditorOnly(importer))
                {
                    MakeEditorOnly(importer);
                }
            }
        }

        private static bool IsTestOnlyPlugin(string assetPath)
        {
            if (!assetPath.StartsWith(InstalledPackagesPath, StringComparison.Ordinal) || !assetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var packageFolder = assetPath.Substring(InstalledPackagesPath.Length);

            foreach (var packageId in TestOnlyPackageIds)
            {
                if (packageFolder.StartsWith(packageId + ".", StringComparison.OrdinalIgnoreCase) && packageFolder.Length > packageId.Length + 1 && char.IsDigit(packageFolder[packageId.Length + 1]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEditorOnly(PluginImporter importer)
        {
            if (importer.GetCompatibleWithAnyPlatform() || !importer.GetCompatibleWithEditor())
            {
                return false;
            }

            foreach (var target in PlayerTargets)
            {
                if (importer.GetCompatibleWithPlatform(target))
                {
                    return false;
                }
            }

            return true;
        }

        private static void MakeEditorOnly(PluginImporter importer)
        {
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(true);

            foreach (var target in PlayerTargets)
            {
                importer.SetCompatibleWithPlatform(target, false);
            }

            importer.SaveAndReimport();
        }
    }
}
