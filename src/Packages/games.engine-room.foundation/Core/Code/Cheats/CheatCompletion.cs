#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Core.Cheats
{
    internal readonly record struct CheatCompletion(string Line, bool IsCycle);
}
#endif
