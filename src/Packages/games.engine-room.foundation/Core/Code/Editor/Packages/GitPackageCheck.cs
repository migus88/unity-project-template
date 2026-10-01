using OneOf;

namespace Core.Editor.Packages
{
    [GenerateOneOf]
    public sealed partial class GitPackageCheck : OneOfBase<GitPackageCheck.Ready, GitPackageCheck.NotEmbedded, GitPackageCheck.NotGit>
    {
        public sealed record Ready(string Url, string EmbeddedVersion, string? Revision, string FolderPath)
        {
            public bool IsVersionMatch => GitDependency.IsSameVersion(Revision, EmbeddedVersion);
        }

        public readonly record struct NotEmbedded;

        public readonly record struct NotGit(string? Entry);
    }
}
