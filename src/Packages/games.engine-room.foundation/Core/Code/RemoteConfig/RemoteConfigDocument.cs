using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Core.RemoteConfig
{
    internal sealed record RemoteConfigDocument(int? Schema, Dictionary<string, JToken>? Flags)
    {
        public const int SupportedSchema = 1;
    }
}
