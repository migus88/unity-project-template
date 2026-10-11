using System.Collections.Generic;
using Core.Environment;
using UnityEditor.Build.Profile;

namespace Core.Editor.Environment
{
    internal static class BuildEnvironmentSelector
    {
        public static BuildEnvironmentSelection Select(IReadOnlyList<BuildEnvironmentMapping> mappings, BuildProfile? activeProfile, bool isDevelopment, string mapPath)
        {
            if (activeProfile == null)
            {
                return NotSelected($"No build profile is active (a classic platform is). Create and activate a build profile, then map it in '{mapPath}'. Build scripts must activate the profile they build (BuildProfile.SetActiveBuildProfile).");
            }

            BuildEnvironmentMapping? match = null;

            foreach (var mapping in mappings)
            {
                if (mapping.Profile != activeProfile)
                {
                    continue;
                }

                if (match != null)
                {
                    return NotSelected($"The build profile '{activeProfile.name}' is mapped more than once in '{mapPath}'. Keep one row per build profile.");
                }

                match = mapping;
            }

            if (match == null)
            {
                return NotSelected($"The active build profile '{activeProfile.name}' has no build environment. Add a row for it in '{mapPath}'.");
            }

            var kind = isDevelopment ? "development" : "release";
            var environment = isDevelopment ? match.Value.DevelopmentEnvironment : match.Value.ReleaseEnvironment;

            if (environment == null)
            {
                return NotSelected($"The build profile '{activeProfile.name}' has no {kind} environment in '{mapPath}'.");
            }

            var problems = BuildEnvironmentValidation.Validate(environment);

            if (problems.Count > 0)
            {
                return NotSelected($"The {kind} environment '{environment.name}' for the build profile '{activeProfile.name}' is invalid: {string.Join(" ", problems)}");
            }

            return new BuildEnvironmentSelection.Selected(environment);
        }

        private static BuildEnvironmentSelection NotSelected(string reason)
        {
            return new BuildEnvironmentSelection.NotSelected(reason);
        }
    }
}
