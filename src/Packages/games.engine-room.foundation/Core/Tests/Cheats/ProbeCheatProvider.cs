using System;
using System.Collections.Generic;
using Core.Cheats;

namespace Core.Tests.Cheats
{
    internal sealed class ProbeCheatProvider : ICheatProvider
    {
        public IReadOnlyList<ICheat> Cheats { get; } = new ICheat[]
        {
            new CheatCommand("alpha", "First.", Array.Empty<CheatParameter>(), _ => "a"),
            new CheatCommand("beta", "Second.", Array.Empty<CheatParameter>(), _ => "b"),
        };
    }
}
