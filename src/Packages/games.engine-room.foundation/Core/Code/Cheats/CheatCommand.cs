#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Cheats
{
    public sealed class CheatCommand : ICheat
    {
        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<CheatParameter> Parameters { get; }

        private readonly Func<CheatArguments, CancellationToken, UniTask<string>> _executeAsync;

        public CheatCommand(string name, string description, IReadOnlyList<CheatParameter> parameters, Func<CheatArguments, CancellationToken, UniTask<string>> executeAsync)
        {
            Name = name;
            Description = description;
            Parameters = parameters;
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        }

        public CheatCommand(string name, string description, IReadOnlyList<CheatParameter> parameters, Func<CheatArguments, string> execute)
            : this(name, description, parameters, ToAsync(execute))
        {
        }

        public UniTask<string> ExecuteAsync(CheatArguments arguments, CancellationToken ct)
        {
            return _executeAsync(arguments, ct);
        }

        private static Func<CheatArguments, CancellationToken, UniTask<string>> ToAsync(Func<CheatArguments, string> execute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            return (arguments, _) => UniTask.FromResult(execute(arguments));
        }
    }
}
#endif
