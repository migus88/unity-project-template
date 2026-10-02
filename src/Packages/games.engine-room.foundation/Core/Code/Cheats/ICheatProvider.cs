#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Core.Cheats
{
    public interface ICheatProvider
    {
        IReadOnlyList<ICheat> Cheats { get; }
    }
}
#endif
