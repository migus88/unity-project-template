using System;
using System.Collections.Generic;
using System.IO;
using Core.Content;
using Core.Domains;
using Core.Logging;
using Core.Results;
using OneOf;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Success = OneOf.Types.Success;

namespace Core.Editor
{
    public static class ContentDirectoryBuilder
    {
        public const string OutputRoot = "Assets/StreamingAssets/" + ContentDirectoryRegistry.RootFolderName;

        private const string DomainsFolder = "Assets/_Project/Domains";

        public static OneOf<Success, Error> BuildAll()
        {
            if (!FindSources().TryPickT0(out var sources, out var error))
            {
                return error;
            }

            DeleteOutput();

            try
            {
                foreach (var source in sources)
                {
                    var result = Build(source);

                    if (result.IsT1)
                    {
                        return result;
                    }
                }

                return new Success();
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("Build/Content Directories")]
        private static void BuildFromMenu()
        {
            BuildAll().Switch(
                _ => Log.Info(LogTags.Content, $"Content directories built into '{OutputRoot}'."),
                error => Log.Error(LogTags.Content, error.Message));
        }

        [InitializeOnLoadMethod]
        private static void RegisterBuildPlayerHandler()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayerWithContent);
        }

        private static void BuildPlayerWithContent(BuildPlayerOptions options)
        {
            if (options.target != EditorUserBuildSettings.activeBuildTarget)
            {
                throw new BuildPlayerWindow.BuildMethodException($"Content directories are built for the active build target ({EditorUserBuildSettings.activeBuildTarget}), but the player targets {options.target}. Switch the active build target first.");
            }

            if (BuildAll().TryPickT1(out var error, out _))
            {
                throw new BuildPlayerWindow.BuildMethodException(error.Message);
            }

            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        }

        private static OneOf<List<ContentSource>, Error> FindSources()
        {
            var sources = new List<ContentSource>();

            if (!AssetDatabase.IsValidFolder(DomainsFolder))
            {
                return sources;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(DomainDescriptor)}", new[] { DomainsFolder }))
            {
                var descriptorPath = AssetDatabase.GUIDToAssetPath(guid);
                var descriptor = AssetDatabase.LoadAssetAtPath<DomainDescriptor>(descriptorPath);
                var name = descriptor.ContentDirectoryName;

                if (!IsValidDirectoryName(name))
                {
                    return new Error($"'{descriptorPath}' has an invalid content directory name '{name}'.");
                }

                if (!names.Add(name))
                {
                    return new Error($"Content directory name '{name}' of '{descriptorPath}' is used by another domain descriptor.");
                }

                if (descriptor.EditorContent == null)
                {
                    return new Error($"'{descriptorPath}' has no {nameof(DomainDescriptor.EditorContent)}.");
                }

                sources.Add(new ContentSource(name, AssetDatabase.GetAssetPath(descriptor.EditorContent)));
            }

            return sources;
        }

        private static bool IsValidDirectoryName(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && name != "."
                && name != ".."
                && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static void DeleteOutput()
        {
            FileUtil.DeleteFileOrDirectory(OutputRoot);
            FileUtil.DeleteFileOrDirectory(OutputRoot + ".meta");
        }

        private static OneOf<Success, Error> Build(ContentSource source)
        {
            var parameters = new BuildContentDirectoryParameters
            {
                name = source.Name,
                outputPath = Path.Combine(OutputRoot, source.Name),
                rootAssetPaths = new[] { source.RootAssetPath },
                options = BuildContentOptions.FailBuildWhenErrorsLogged,
            };

            var report = BuildPipeline.BuildContentDirectory(parameters);

            if (report.summary.result != BuildResult.Succeeded)
            {
                return new Error($"Building content directory '{source.Name}' from '{source.RootAssetPath}' failed: {report.summary.result}, {report.summary.totalErrors} error(s).");
            }

            Log.Info(LogTags.Content, $"Built content directory '{source.Name}' from '{source.RootAssetPath}'.");
            return new Success();
        }

        private sealed record ContentSource(string Name, string RootAssetPath);
    }
}
