using Core.Logging;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;

namespace Core.Editor.Environment
{
    internal sealed class BuildEnvironmentBuildHook : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildEnvironmentRestorer.RestoreAfterBuild();
            var isDevelopment = (report.summary.options & BuildOptions.Development) != 0;

            BuildEnvironmentMaps.Find().Switch(
                found => Apply(found.Map, found.Path, BuildProfile.GetActiveBuildProfile(), isDevelopment),
                _ => Log.Info(LogTags.Environment, "No build environment map in the project; the build uses no build environment."),
                several => throw new BuildFailedException($"Found {several.Paths.Count} build environment maps ({string.Join(", ", several.Paths)}). Keep exactly one."));
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            BuildEnvironmentRestorer.RestoreAll(isUnexpected: false);
        }

        internal static void Apply(BuildEnvironmentMap map, string mapPath, BuildProfile? activeProfile, bool isDevelopment)
        {
            if (map.Active == null)
            {
                throw new BuildFailedException($"The build environment map '{mapPath}' has no Active slot. Assign the ActiveBuildEnvironment asset the root prefab references.");
            }

            var slot = map.Active;

            BuildEnvironmentSelector.Select(map.Mappings, activeProfile, isDevelopment, mapPath).Switch(
                selected =>
                {
                    if (BuildEnvironmentSlot.Assign(slot, selected.Environment))
                    {
                        AssetDatabase.SaveAssetIfDirty(slot);
                    }

                    Log.Info(LogTags.Environment, $"Build environment: '{selected.Environment.EnvironmentName}' ({selected.Environment.name}).");
                },
                notSelected => throw new BuildFailedException(notSelected.Reason));
        }
    }
}
