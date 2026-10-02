#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Cheats
{
    internal sealed class ClearConsoleCheat : ICheat
    {
        public const string CheatName = "clear";

        public string Name => CheatName;

        public string Description => "Clears the console output.";

        public IReadOnlyList<CheatParameter> Parameters => Array.Empty<CheatParameter>();

        private readonly CheatConsoleModel _model;

        public ClearConsoleCheat(CheatConsoleModel model)
        {
            _model = model;
        }

        public UniTask<string> ExecuteAsync(CheatArguments arguments, CancellationToken ct)
        {
            _model.ClearOutput();
            return UniTask.FromResult(string.Empty);
        }
    }
}
#endif
