using System.Collections.Generic;
using OneOf;

namespace Core.Editor.Environment
{
    [GenerateOneOf]
    internal sealed partial class BuildEnvironmentMapLookup : OneOfBase<BuildEnvironmentMapLookup.Found, BuildEnvironmentMapLookup.Missing, BuildEnvironmentMapLookup.Several>
    {
        public sealed record Found(BuildEnvironmentMap Map, string Path);

        public readonly record struct Missing;

        public sealed record Several(IReadOnlyList<string> Paths);
    }
}
