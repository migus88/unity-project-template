#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Core.Cheats
{
    public enum CheatValueKind
    {
        None = 0,
        Int = 1,
        Float = 2,
        Bool = 3,
        Enum = 4,
        Text = 5,
        Choice = 6,
    }
}
#endif
