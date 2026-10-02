using System.Collections.Generic;
using System.Threading;
using Core.Cheats;
using Cysharp.Threading.Tasks;

namespace Core.Tests.Cheats
{
    internal sealed class RecordingCheat : ICheat
    {
        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<CheatParameter> Parameters { get; }

        public CheatArguments? LastArguments { get; private set; }

        public int Calls { get; private set; }

        private readonly string _reply;

        public RecordingCheat(string name, params CheatParameter[] parameters)
            : this(name, "done", parameters)
        {
        }

        public RecordingCheat(string name, string reply, params CheatParameter[] parameters)
        {
            Name = name;
            Description = $"Description of {name}.";
            Parameters = parameters;
            _reply = reply;
        }

        public UniTask<string> ExecuteAsync(CheatArguments arguments, CancellationToken ct)
        {
            Calls++;
            LastArguments = arguments;
            return UniTask.FromResult(_reply);
        }
    }
}
