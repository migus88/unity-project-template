using Newtonsoft.Json.Linq;

namespace Core.Save
{
    internal sealed record SaveSectionDto(int Version, JObject? Data);
}
