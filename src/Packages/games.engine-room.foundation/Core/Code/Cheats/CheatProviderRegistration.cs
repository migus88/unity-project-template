#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using R3;
using VContainer.Unity;

namespace Core.Cheats
{
    internal sealed class CheatProviderRegistration<TProvider> : IStartable, IDisposable where TProvider : class, ICheatProvider
    {
        private DisposableBag _handles;

        private readonly CheatRegistry _registry;
        private readonly TProvider _provider;

        public CheatProviderRegistration(CheatRegistry registry, TProvider provider)
        {
            _registry = registry;
            _provider = provider;
        }

        public void Start()
        {
            foreach (var cheat in _provider.Cheats)
            {
                _registry.Add(cheat).AddTo(ref _handles);
            }
        }

        public void Dispose()
        {
            _handles.Dispose();
        }
    }
}
#endif
