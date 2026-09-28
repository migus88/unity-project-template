using OneOf;

namespace Settings
{
    [GenerateOneOf]
    public partial class SettingsResult : OneOfBase<SettingsResult.Closed>
    {
        public readonly record struct Closed;
    }
}
