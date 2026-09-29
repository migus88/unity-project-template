using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Core.Settings
{
    internal sealed record SettingsFileDto(int FormatVersion, CoreSettingsDto? Core, Dictionary<string, JToken?>? Sections);
}
