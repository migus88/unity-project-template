namespace Core.Privacy
{
    internal sealed record PrivacySettingsDto(string? Consent, int? PolicyVersion, string? InstallId);
}
