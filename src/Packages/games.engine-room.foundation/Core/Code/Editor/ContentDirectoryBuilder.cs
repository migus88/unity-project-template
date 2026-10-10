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
        private const string OutputRoot = "Assets/StreamingAssets/" + ContentDirectoryRegistry.RootFolderName;

        public static OneOf<Success, Error> BuildAll()
        {
            return BuildAll(EditorUserBuildSettings.development);
        }

        public static OneOf<Success, Error> BuildAll(bool isDevelopmentBuild)
        {
            if (!SelectSources(FindDescriptors(), isDevelopmentBuild).TryPickT0(out var sources, out var error))
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
            var isDevelopmentBuild = EditorUserBuildSettings.development;
            var buildKind = isDevelopmentBuild ? "development build, development-only domains included" : "release build, development-only domains left out";

            BuildAll(isDevelopmentBuild).Switch(
                _ => Log.Info(LogTags.Content, $"Content directories built into '{OutputRoot}' ({buildKind})."),
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

            var isDevelopmentBuild = (options.options & BuildOptions.Development) != 0;

            if (BuildAll(isDevelopmentBuild).TryPickT1(out var error, out _))
            {
                throw new BuildPlayerWindow.BuildMethodException(error.Message);
            }

            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        }

        internal static OneOf<List<ContentSource>, Error> SelectSources(IReadOnlyList<DescriptorSource> descriptors, bool isDevelopmentBuild)
        {
            var sources = new List<ContentSource>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var descriptor in descriptors)
            {
                var name = descriptor.ContentDirectoryName;

                if (!IsValidDirectoryName(name))
                {
                    return new Error($"'{descriptor.DescriptorPath}' has an invalid content directory name '{name}'.");
                }

                if (!names.Add(name))
                {
                    return new Error($"Content directory name '{name}' of '{descriptor.DescriptorPath}' is used by another domain descriptor.");
                }

                if (descriptor.ContentAssetPath == null)
                {
                    return new Error($"'{descriptor.DescriptorPath}' has no {nameof(DomainDescriptor.EditorContent)}.");
                }

                if (descriptor.IsDevelopmentOnly && !isDevelopmentBuild)
                {
                    Log.Info(LogTags.Content, $"Skipped development-only content directory '{name}' of '{descriptor.DescriptorPath}' in a release build.");
                    continue;
                }

                sources.Add(new ContentSource(name, descriptor.ContentAssetPath));
            }

            return sources;
        }

        private static List<DescriptorSource> FindDescriptors()
        {
            var descriptors = new List<DescriptorSource>();

            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(DomainDescriptor)}"))
            {
                var descriptorPath = AssetDatabase.GUIDToAssetPath(guid);
                var descriptor = AssetDatabase.LoadAssetAtPath<DomainDescriptor>(descriptorPath);
                var contentAssetPath = descriptor.EditorContent == null ? null : AssetDatabase.GetAssetPath(descriptor.EditorContent);
                descriptors.Add(new DescriptorSource(descriptorPath, descriptor.ContentDirectoryName, descriptor.IsDevelopmentOnly, contentAssetPath));
            }

            return descriptors;
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
                rootAssetPaths = [source.RootAssetPath],
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

        internal sealed record DescriptorSource(string DescriptorPath, string ContentDirectoryName, bool IsDevelopmentOnly, string? ContentAssetPath);

        internal sealed record ContentSource(string Name, string RootAssetPath);
    }
}
