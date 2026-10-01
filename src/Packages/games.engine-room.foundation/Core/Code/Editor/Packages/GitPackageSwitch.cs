using System.IO;
using Core.Logging;
using UnityEditor;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Core.Editor.Packages
{
    public static class GitPackageSwitch
    {
        public const string MenuPath = "Tools/Foundation/Use Package From Git";
        public const string ManifestPath = "Packages/manifest.json";

        public static GitPackageCheck Inspect()
        {
            var package = PackageInfo.FindForAssetPath(CorePackage.Root);

            if (package == null || package.source != PackageSource.Embedded)
            {
                return new GitPackageCheck.NotEmbedded();
            }

            var entry = File.Exists(ManifestPath)
                ? GitDependency.FindEntry(File.ReadAllText(ManifestPath), CorePackage.Name)
                : null;

            if (entry == null || !GitDependency.IsGitUrl(entry))
            {
                return new GitPackageCheck.NotGit(entry);
            }

            return new GitPackageCheck.Ready(entry, package.version, GitDependency.GetRevision(entry), package.resolvedPath);
        }

        public static GitPackageCheck UseFromGit()
        {
            var check = Inspect();

            if (check.TryPickT0(out var ready, out _))
            {
                Switch(ready);
            }
            else
            {
                Log.Warn(LogTags.Packages, $"{CorePackage.Name} is not switched to git: {Describe(check)}");
            }

            return check;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanUseFromGitMenu()
        {
            return Inspect().IsT0;
        }

        [MenuItem(MenuPath)]
        private static void UseFromGitMenu()
        {
            if (!Inspect().TryPickT0(out var ready, out _))
            {
                return;
            }

            var message = $"Delete the embedded '{CorePackage.Root}' folder and use the manifest entry\n{ready.Url}\ninstead?";

            if (!ready.IsVersionMatch)
            {
                message += $"\n\nWarning: the embedded package is version {ready.EmbeddedVersion}, but the manifest asks for '{ready.Revision ?? "the default branch"}'. Local changes that are not in that revision are lost.";
            }

            if (EditorUtility.DisplayDialog("Use Package From Git", message, "Delete and resolve", "Cancel"))
            {
                Switch(ready);
            }
        }

        private static void Switch(GitPackageCheck.Ready ready)
        {
            if (!ready.IsVersionMatch)
            {
                Log.Warn(LogTags.Packages, $"Embedded {CorePackage.Name} is version {ready.EmbeddedVersion}, the manifest asks for '{ready.Revision ?? "the default branch"}'.");
            }

            Directory.Delete(ready.FolderPath, true);
            Log.Info(LogTags.Packages, $"Deleted '{CorePackage.Root}', resolving '{ready.Url}'.");
            Client.Resolve();
        }

        private static string Describe(GitPackageCheck check)
        {
            return check.Match(
                ready => ready.Url,
                _ => "the package is not embedded.",
                notGit => $"the manifest entry '{notGit.Entry ?? "(missing)"}' is not a git URL.");
        }
    }
}
