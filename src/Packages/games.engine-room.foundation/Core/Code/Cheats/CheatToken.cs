#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Core.Cheats
{
    public readonly record struct CheatToken(string Value, int Start, int End, bool IsQuoted);
}
#endif
