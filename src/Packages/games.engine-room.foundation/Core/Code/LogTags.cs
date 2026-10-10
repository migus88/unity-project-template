using Core.Logging;

namespace Core
{
    internal static class LogTags
    {
        public static readonly LogTag Analytics = new("Analytics");
        public static readonly LogTag Cheats = new("Cheats");
        public static readonly LogTag Content = new("Content");
        public static readonly LogTag Localization = new("Localization");
        public static readonly LogTag Save = new("Save");
        public static readonly LogTag Settings = new("Settings");
    }
}
