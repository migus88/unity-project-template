using Core.Environment;
using OneOf;

namespace Core.Editor.Environment
{
    [GenerateOneOf]
    internal sealed partial class BuildEnvironmentSelection : OneOfBase<BuildEnvironmentSelection.Selected, BuildEnvironmentSelection.NotSelected>
    {
        public sealed record Selected(BuildEnvironment Environment);

        public sealed record NotSelected(string Reason);
    }
}
