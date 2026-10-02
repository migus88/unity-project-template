#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Cheats
{
    public interface ICheat
    {
        string Name { get; }

        string Description { get; }

        IReadOnlyList<CheatParameter> Parameters { get; }

        UniTask<string> ExecuteAsync(CheatArguments arguments, CancellationToken ct);
    }
}
#endif
