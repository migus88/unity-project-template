using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Content;
using Core.Domains;
using Core.Logging;
using Core.Results;
using OneOf;
using UnityEditor;
using UnityEditor.Build.Reporting;
using BuildCompression = UnityEngine.BuildCompression;
using Success = OneOf.Types.Success;

namespace Core.Editor
{
    public static class ContentDirectoryBuilder
    {
        public const string SharedDirectoryName = "Shared";

        internal const BuildContentOptions ContentOptions = BuildContentOptions.FailBuildWhenErrorsLogged | BuildContentOptions.UseArchive | BuildContentOptions.DisableWriteTypeTree | BuildContentOptions.SerializeUnityVersion;

        private const string CatalogFolder = "Assets/ContentCatalogBuild";
        private const string OutputRoot = "Assets/StreamingAssets/" + ContentDirectoryRegistry.RootFolderName;

        internal static readonly BuildCompression Compression = BuildCompression.LZ4;

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
            DeleteCatalogFolder();

            try
            {
                foreach (var plan in PlanDirectories(sources))
                {
                    var result = Build(plan);

                    if (result.IsT1)
                    {
                        return result;
                    }
                }

                return new Success();
            }
            finally
            {
                DeleteCatalogFolder();
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
            var rootDescriptorPaths = descriptors
                .Where(descriptor => descriptor.IsRootDescriptor)
                .GroupBy(descriptor => descriptor.TypeName)
                .ToDictionary(group => group.Key, group => group.First().DescriptorPath);

            foreach (var descriptor in descriptors)
            {
                var name = descriptor.ContentDirectoryName;

                if (!descriptor.IsRootDescriptor && rootDescriptorPaths.TryGetValue(descriptor.TypeName, out var rootDescriptorPath))
                {
                    Log.Info(LogTags.Content, $"Skipped content '{name}' of '{descriptor.DescriptorPath}': the root scope uses '{rootDescriptorPath}' for {descriptor.TypeName}.");
                    continue;
                }

                if (!IsValidDirectoryName(name))
                {
                    return new Error($"'{descriptor.DescriptorPath}' has an invalid content directory name '{name}'.");
                }

                if (!names.Add(name))
                {
                    return new Error($"Content directory name '{name}' of '{descriptor.DescriptorPath}' is used by another domain descriptor.");
                }

                if (descriptor.HasOwnContentDirectory && string.Equals(name, SharedDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return new Error($"'{descriptor.DescriptorPath}' has its own content directory named '{name}', which is reserved for the shared content directory.");
                }

                if (descriptor.ContentAssetPath == null)
                {
                    return new Error($"'{descriptor.DescriptorPath}' has no {nameof(DomainDescriptor.EditorContent)}.");
                }

                if (descriptor.IsDevelopmentOnly && !isDevelopmentBuild)
                {
                    Log.Info(LogTags.Content, $"Skipped development-only content '{name}' of '{descriptor.DescriptorPath}' in a release build.");
                    continue;
                }

                sources.Add(new ContentSource(name, descriptor.ContentAssetPath, descriptor.HasOwnContentDirectory));
            }

            return sources;
        }

        internal static List<DirectoryPlan> PlanDirectories(IReadOnlyList<ContentSource> sources)
        {
            var plans = new List<DirectoryPlan>();
            var sharedSources = sources.Where(source => !source.HasOwnDirectory).ToList();

            if (sharedSources.Count > 0)
            {
                plans.Add(new DirectoryPlan(SharedDirectoryName, sharedSources));
            }

            foreach (var source in sources.Where(source => source.HasOwnDirectory))
            {
                plans.Add(new DirectoryPlan(source.Name, [source]));
            }

            return plans;
        }

        private static List<DescriptorSource> FindDescriptors()
        {
            var descriptors = new List<DescriptorSource>();
            var rootDescriptors = new HashSet<DomainDescriptor>(RootDomainDescriptors.Collect());

            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(DomainDescriptor)}"))
            {
                var descriptorPath = AssetDatabase.GUIDToAssetPath(guid);
                var descriptor = AssetDatabase.LoadAssetAtPath<DomainDescriptor>(descriptorPath);
                var contentAssetPath = descriptor.EditorContent == null ? null : AssetDatabase.GetAssetPath(descriptor.EditorContent);
                descriptors.Add(new DescriptorSource(descriptorPath, descriptor.GetType().FullName ?? descriptor.GetType().Name, rootDescriptors.Contains(descriptor), descriptor.ContentDirectoryName, descriptor.IsDevelopmentOnly, descriptor.HasOwnContentDirectory, contentAssetPath));
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
            if (AssetDatabase.IsValidFolder(OutputRoot))
            {
                AssetDatabase.DeleteAsset(OutputRoot);
            }

            FileUtil.DeleteFileOrDirectory(OutputRoot);
            FileUtil.DeleteFileOrDirectory(OutputRoot + ".meta");
        }

        private static void DeleteCatalogFolder()
        {
            if (AssetDatabase.IsValidFolder(CatalogFolder))
            {
                AssetDatabase.DeleteAsset(CatalogFolder);
            }
        }

        private static OneOf<Success, Error> Build(DirectoryPlan plan)
        {
            var catalogPath = CreateCatalog(plan);
            var contentNames = string.Join(", ", plan.Sources.Select(source => source.Name));

            var outputPath = Path.Combine(OutputRoot, plan.Name);
            Directory.CreateDirectory(OutputRoot);

            var parameters = new BuildContentDirectoryParameters
            {
                name = plan.Name,
                outputPath = outputPath,
                rootAssetPaths = [catalogPath],
                options = ContentOptions,
                compression = Compression,
            };

            var report = BuildPipeline.BuildContentDirectory(parameters);

            if (report.summary.result != BuildResult.Succeeded)
            {
                return new Error($"Building content directory '{plan.Name}' ({contentNames}) failed: {report.summary.result}, {report.summary.totalErrors} error(s).");
            }

            Log.Info(LogTags.Content, $"Built content directory '{plan.Name}' ({contentNames}): {MeasureMegabytes(outputPath):F1} MB.");
            return new Success();
        }

        private static double MeasureMegabytes(string directory)
        {
            var bytes = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(file => !file.EndsWith(".meta", StringComparison.Ordinal))
                .Sum(file => new FileInfo(file).Length);
            return bytes / (1024.0 * 1024.0);
        }

        private static string CreateCatalog(DirectoryPlan plan)
        {
            if (!AssetDatabase.IsValidFolder(CatalogFolder))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(CatalogFolder));
            }

            var entries = plan.Sources
                .Select(source => new ContentCatalogEntry(source.Name, AssetDatabase.LoadAssetAtPath<DomainContent>(source.RootAssetPath)))
                .ToList();
            var catalogPath = $"{CatalogFolder}/{plan.Name}.asset";
            AssetDatabase.CreateAsset(ContentCatalog.Create(entries), catalogPath);
            AssetDatabase.SaveAssets();
            return catalogPath;
        }

        internal sealed record DescriptorSource(string DescriptorPath, string TypeName, bool IsRootDescriptor, string ContentDirectoryName, bool IsDevelopmentOnly, bool HasOwnContentDirectory, string? ContentAssetPath);

        internal sealed record ContentSource(string Name, string RootAssetPath, bool HasOwnDirectory);

        internal sealed record DirectoryPlan(string Name, IReadOnlyList<ContentSource> Sources);
    }
}
