#if UNITY_EDITOR || DEVELOPMENT_BUILD
using OneOf;

namespace Core.Cheats
{
    [GenerateOneOf]
    public sealed partial class CheatParse : OneOfBase<CheatParse.Parsed, CheatParse.Invalid>
    {
        public sealed record Parsed(CheatArguments Arguments);

        public readonly record struct Invalid(string Message);
    }
}
#endif
