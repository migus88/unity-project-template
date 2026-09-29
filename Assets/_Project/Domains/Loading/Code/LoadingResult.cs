using OneOf;

namespace Loading
{
    [GenerateOneOf]
    public sealed partial class LoadingResult : OneOfBase<LoadingResult.Stopped>
    {
        public readonly record struct Stopped;
    }
}
