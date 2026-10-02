using System;
using System.Collections.Generic;
using System.Threading;
using Core.Cheats;
using Cysharp.Threading.Tasks;

namespace Core.Tests.Cheats
{
    internal sealed class ThrowingCheat : ICheat
    {
        public string Name => "boom";

        public string Description => "Throws.";

        public IReadOnlyList<CheatParameter> Parameters => Array.Empty<CheatParameter>();

        public UniTask<string> ExecuteAsync(CheatArguments arguments, CancellationToken ct)
        {
            throw new InvalidOperationException("kaboom");
        }
    }
}
