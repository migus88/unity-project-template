#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using VContainer.Unity;

namespace Core.Cheats
{
    internal sealed class CheatRegistration<TCheat> : IStartable, IDisposable where TCheat : class, ICheat
    {
        private IDisposable? _handle;

        private readonly CheatRegistry _registry;
        private readonly TCheat _cheat;

        public CheatRegistration(CheatRegistry registry, TCheat cheat)
        {
            _registry = registry;
            _cheat = cheat;
        }

        public void Start()
        {
            _handle = _registry.Add(_cheat);
        }

        public void Dispose()
        {
            _handle?.Dispose();
            _handle = null;
        }
    }
}
#endif
