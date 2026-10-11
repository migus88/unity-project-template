namespace Core.Privacy
{
    public readonly record struct ConsentSnapshot(ConsentState State, int? PolicyVersion, string? InstallId);
}
