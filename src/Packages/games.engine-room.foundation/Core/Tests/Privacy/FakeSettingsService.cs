using System;
using System.Collections.Generic;
using System.Threading;
using Core.Results;
using Core.Settings;
using Cysharp.Threading.Tasks;
using OneOf;
using R3;
using Success = OneOf.Types.Success;

namespace Core.Tests.Privacy
{
    internal sealed class FakeSettingsService : ISettingsService
    {
        public ReadOnlyReactiveProperty<SettingsState> Current => throw new NotSupportedException();

        public int SaveCount { get; private set; }

        public OneOf<Success, Error> SaveResult { get; set; } = new Success();

        private readonly Dictionary<string, object> _sections = new();

        public void Apply(SettingsState state)
        {
            throw new NotSupportedException();
        }

        public T Read<T>(SettingsSection<T> section) where T : class
        {
            return _sections.TryGetValue(section.Key, out var data) ? (T)data : section.Default;
        }

        public void Write<T>(SettingsSection<T> section, T data) where T : class
        {
            _sections[section.Key] = data;
        }

        public UniTask<OneOf<Success, Error>> SaveAsync(CancellationToken ct)
        {
            SaveCount++;
            return UniTask.FromResult(SaveResult);
        }
    }
}
