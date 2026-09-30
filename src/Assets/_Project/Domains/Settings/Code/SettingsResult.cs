using OneOf;

namespace Settings
{
    [GenerateOneOf]
    public sealed partial class SettingsResult : OneOfBase<SettingsResult.Closed>
    {
        public readonly record struct Closed;
    }
}
