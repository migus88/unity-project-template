#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Core.Cheats
{
    internal readonly record struct CheatSuggestions(IReadOnlyList<string> Values, int Selected);
}
#endif
