using System.Collections.Generic;

namespace Core.Platform
{
    public readonly record struct Presence(string Token, IReadOnlyDictionary<string, string>? Values);
}
