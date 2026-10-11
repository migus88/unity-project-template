namespace Core.RemoteConfig
{
    internal sealed record RemoteConfigCacheDto(int? Version, string? Environment, string? ETag, string? Document)
    {
        public const int CurrentVersion = 1;
        public const string FileName = "remote-config.json";
    }
}
