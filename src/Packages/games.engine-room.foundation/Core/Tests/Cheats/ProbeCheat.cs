using System;
using System.Collections.Generic;
using System.Threading;
using Core.Cheats;
using Cysharp.Threading.Tasks;

namespace Core.Tests.Cheats
{
    internal sealed class ProbeCheat : ICheat
    {
        public const string CheatName = "probe";

        public string Name => CheatName;

        public string Description => "Probes.";

        public IReadOnlyList<CheatParameter> Parameters => Array.Empty<CheatParameter>();

        public UniTask<string> ExecuteAsync(CheatArguments arguments, CancellationToken ct)
        {
            return UniTask.FromResult("probed");
        }
    }
}
