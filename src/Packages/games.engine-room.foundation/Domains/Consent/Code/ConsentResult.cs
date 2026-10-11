using OneOf;

namespace Consent
{
    [GenerateOneOf]
    public sealed partial class ConsentResult : OneOfBase<ConsentResult.Granted, ConsentResult.Denied>
    {
        public readonly record struct Granted;

        public readonly record struct Denied;
    }
}
